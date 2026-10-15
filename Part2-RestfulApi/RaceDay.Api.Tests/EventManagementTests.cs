using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class EventManagementTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EventManagementTests(RaceDayFactory factory) => _factory = factory;

    private static object ValidEvent(string name = "Updated Event") => new
    {
        eventName = name,
        description = "desc",
        eventDate = "2027-01-15",
        location = "Durban",
        distance = 21.1,
        eventType = "Walk"
    };

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", ValidEvent("Brand New Event"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.JsonAsync();
        Assert.Equal("Brand New Event", json.GetProperty("eventName").GetString());
        Assert.Equal("Walk", json.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_CreateEvent_MissingName_Returns400()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events",
            new { eventDate = "2027-01-15", eventType = "Run" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_InvalidEventType_Returns400()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events",
            new { eventName = "Bad type", eventDate = "2027-01-15", eventType = "Swim" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_Returns200()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent("Renamed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed", (await response.JsonAsync()).GetProperty("eventName").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{eventId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_Returns204_ThenEventIs404()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        await TestHelpers.CreateCategoryAsync(organiser, eventId);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{eventId}")).StatusCode);
    }

    [Fact]
    public async Task UpdateOrDelete_NonExistentEvent_Returns404()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.NotFound, (await organiser.PutAsJsonAsync("/api/events/999999", ValidEvent())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.DeleteAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task Anyone_CanViewEvent_WithCategories()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        await TestHelpers.CreateCategoryAsync(organiser, eventId, "Under 20");

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = (await response.JsonAsync()).GetProperty("categories");
        Assert.Equal(1, categories.GetArrayLength());
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanManageCategories()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId, "Senior");

        var update = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}", new { categoryName = "Senior 10km", distance = 10.0 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/categories");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var delete = await organiser.DeleteAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_DuplicateCategoryName_Returns409()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        await TestHelpers.CreateCategoryAsync(organiser, eventId, "Open");

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryName = "Open" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryName = "Sneaky" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Profiles_CanBeViewedAndUpdated_ByTheirOwnRole()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var orgUpdate = await organiser.PutAsJsonAsync("/api/organisers/me",
            new { firstName = "New", lastName = "Name", contactNumber = "0821112222" });
        Assert.Equal(HttpStatusCode.OK, orgUpdate.StatusCode);
        Assert.Equal("New", (await (await organiser.GetAsync("/api/organisers/me")).JsonAsync()).GetProperty("firstName").GetString());

        var partUpdate = await participant.PutAsJsonAsync("/api/participants/me", new { firstName = "Pat", lastName = "Runner" });
        Assert.Equal(HttpStatusCode.OK, partUpdate.StatusCode);
        Assert.Equal("Pat", (await (await participant.GetAsync("/api/participants/me")).JsonAsync()).GetProperty("firstName").GetString());
    }
}
