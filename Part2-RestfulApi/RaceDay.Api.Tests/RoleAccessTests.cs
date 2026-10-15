using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

/// <summary>Verifies role-based access: the wrong role (or no login) is always rejected.</summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    private static readonly object NewEvent = new { eventName = "Nope", eventDate = "2027-01-01", eventType = "Run" };

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync("/api/events", NewEvent)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotCreateEvent_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/events", NewEvent)).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUseOrganiserProfile_Returns403()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync("/api/organisers/me")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantProfile_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/participants/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/participants/me/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/participants/me/results")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolInEvent_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);

        var response = await organiser.PostAsync($"/api/categories/{categoryId}/entries", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotManageCategories_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryName = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PutAsJsonAsync($"/api/categories/{categoryId}", new { categoryName = "Y" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvents_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PutAsJsonAsync($"/api/events/{eventId}", NewEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/events/{eventId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEntries_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/events/{eventId}/entries")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);
        var entryId = await TestHelpers.EnrolAsync(participant, categoryId);

        var response = await participant.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:30:00" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
