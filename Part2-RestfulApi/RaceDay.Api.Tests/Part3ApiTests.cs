using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Endpoints added or extended for the MVC front end: upcoming filter, entry status, finisher totals, dashboard data.</summary>
public class Part3ApiTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public Part3ApiTests(RaceDayFactory factory) => _factory = factory;

    private static async Task<int> CreateEventOnAsync(HttpClient organiser, DateOnly date, string name)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = name,
            description = "test",
            eventDate = date.ToString("yyyy-MM-dd"),
            location = "Cape Town",
            distance = 10.0,
            eventType = "Run"
        });
        response.EnsureSuccessStatusCode();
        return (await response.JsonAsync()).GetProperty("eventID").GetInt32();
    }

    private static DateOnly Future => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
    private static DateOnly Past => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

    [Fact]
    public async Task Events_UpcomingFilter_ExcludesPastEvents()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var pastId = await CreateEventOnAsync(organiser, Past, "Past event");
        var futureId = await CreateEventOnAsync(organiser, Future, "Future event");

        var all = await (await _factory.CreateClient().GetAsync("/api/events?upcoming=true")).JsonAsync();
        var ids = all.EnumerateArray().Select(e => e.GetProperty("eventID").GetInt32()).ToList();

        Assert.Contains(futureId, ids);
        Assert.DoesNotContain(pastId, ids);
    }

    [Fact]
    public async Task EntryStatus_IsConfirmed_AwaitingResults_ThenCompleted()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var futureEvent = await CreateEventOnAsync(organiser, Future, "Upcoming race");
        var pastEvent = await CreateEventOnAsync(organiser, Past, "Finished race");
        var futureCategory = await TestHelpers.CreateCategoryAsync(organiser, futureEvent);
        var pastCategory = await TestHelpers.CreateCategoryAsync(organiser, pastEvent);

        await TestHelpers.EnrolAsync(participant, futureCategory);
        var pastEntry = await TestHelpers.EnrolAsync(participant, pastCategory);

        var before = await (await participant.GetAsync("/api/participants/me/entries")).JsonAsync();
        Assert.Equal("Awaiting Results", StatusOf(before, pastCategory));
        Assert.Equal("Confirmed", StatusOf(before, futureCategory));

        await organiser.PostAsJsonAsync($"/api/entries/{pastEntry}/result", new { finishTime = "00:50:00" });

        var after = await (await participant.GetAsync("/api/participants/me/entries")).JsonAsync();
        Assert.Equal("Completed", StatusOf(after, pastCategory));
    }

    private static string StatusOf(JsonElement entries, int categoryId)
        => entries.EnumerateArray().First(e => e.GetProperty("categoryID").GetInt32() == categoryId)
                  .GetProperty("status").GetString()!;

    [Fact]
    public async Task ParticipantResults_IncludePositionAndTotalFinishers()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (first, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var (second, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var eventId = await CreateEventOnAsync(organiser, Past, "Totals race");
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);
        var firstEntry = await TestHelpers.EnrolAsync(first, categoryId);
        var secondEntry = await TestHelpers.EnrolAsync(second, categoryId);

        await organiser.PostAsJsonAsync($"/api/entries/{firstEntry}/result", new { finishTime = "00:40:00" });
        await organiser.PostAsJsonAsync($"/api/entries/{secondEntry}/result", new { finishTime = "00:35:00" });

        var results = await (await first.GetAsync("/api/participants/me/results")).JsonAsync();

        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal(2, results[0].GetProperty("position").GetInt32());        // slower than the second runner
        Assert.Equal(2, results[0].GetProperty("totalFinishers").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanListOwnEvents_WithEnrolmentCounts()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var mine = await CreateEventOnAsync(organiser, Future, "Mine");
        await CreateEventOnAsync(other, Future, "Not mine");
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, mine);
        await TestHelpers.EnrolAsync(participant, categoryId);

        var list = await (await organiser.GetAsync("/api/organisers/me/events")).JsonAsync();

        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(mine, list[0].GetProperty("eventID").GetInt32());
        Assert.Equal(1, list[0].GetProperty("enrolmentCount").GetInt32());
        Assert.Equal(1, list[0].GetProperty("categoryCount").GetInt32());
    }

    [Fact]
    public async Task OrganiserEventList_IsNotAvailableToParticipants()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync("/api/organisers/me/events")).StatusCode);
    }
}
