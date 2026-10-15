using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class EnrolmentAndResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EnrolmentAndResultTests(RaceDayFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, int EventId, int CategoryId)> SetUpEventAsync()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);
        return (organiser, eventId, categoryId);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (_, eventId, categoryId) = await SetUpEventAsync();
        var (participant, participantId, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var response = await participant.PostAsync($"/api/categories/{categoryId}/entries", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await response.JsonAsync();
        Assert.Equal(participantId, entry.GetProperty("participantID").GetInt32());
        Assert.Equal(categoryId, entry.GetProperty("categoryID").GetInt32());
        Assert.Equal(eventId, entry.GetProperty("eventID").GetInt32());

        var mine = await (await participant.GetAsync("/api/participants/me/entries")).JsonAsync();
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(categoryId, mine[0].GetProperty("categoryID").GetInt32());
    }

    [Fact]
    public async Task Participant_EnrolTwiceInSameCategory_Returns409()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        await TestHelpers.EnrolAsync(participant, categoryId);

        var second = await participant.PostAsync($"/api/categories/{categoryId}/entries", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolInMissingCategory_Returns404()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        Assert.Equal(HttpStatusCode.NotFound, (await participant.PostAsync("/api/categories/999999/entries", null)).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (organiser, eventId, categoryId) = await SetUpEventAsync();
        var (p1, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var (p2, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        await TestHelpers.EnrolAsync(p1, categoryId);
        await TestHelpers.EnrolAsync(p2, categoryId);

        var response = await organiser.GetAsync($"/api/events/{eventId}/entries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.JsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var (_, eventId, _) = await SetUpEventAsync();
        var (otherOrganiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.Forbidden, (await otherOrganiser.GetAsync($"/api/events/{eventId}/entries")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanWithdrawOwnEntry_ButNotSomeoneElses()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var (owner, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var entryId = await TestHelpers.EnrolAsync(owner, categoryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/entries/{entryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/entries/{entryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/entries/{entryId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewIt()
    {
        var (organiser, _, categoryId) = await SetUpEventAsync();
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var entryId = await TestHelpers.EnrolAsync(participant, categoryId);

        var capture = await organiser.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:45:10" });

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        var result = await capture.JsonAsync();
        Assert.Equal("00:45:10", result.GetProperty("finishTime").GetString());
        Assert.Equal(1, result.GetProperty("position").GetInt32());

        var mine = await (await participant.GetAsync("/api/participants/me/results")).JsonAsync();
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(entryId, mine[0].GetProperty("eventEntryID").GetInt32());
    }

    [Fact]
    public async Task Organiser_CaptureResultTwice_Returns409()
    {
        var (organiser, _, categoryId) = await SetUpEventAsync();
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var entryId = await TestHelpers.EnrolAsync(participant, categoryId);
        await organiser.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:45:10" });

        var second = await organiser.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:40:00" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Organiser_CaptureResult_ForUnknownEntry_Returns404()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/entries/999999/result", new { finishTime = "00:45:10" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResult_ForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var (otherOrganiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var entryId = await TestHelpers.EnrolAsync(participant, categoryId);

        var response = await otherOrganiser.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:45:10" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CaptureResult_InvalidTime_Returns400()
    {
        var (organiser, _, categoryId) = await SetUpEventAsync();
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var entryId = await TestHelpers.EnrolAsync(participant, categoryId);

        var response = await organiser.PostAsJsonAsync($"/api/entries/{entryId}/result", new { finishTime = "00:00:00" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CategoryResults_AreOrderedByFinishTime_WithCorrectPositions()
    {
        var (organiser, eventId, categoryId) = await SetUpEventAsync();
        var (slow, slowId, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var (fast, fastId, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var slowEntry = await TestHelpers.EnrolAsync(slow, categoryId);
        var fastEntry = await TestHelpers.EnrolAsync(fast, categoryId);

        await organiser.PostAsJsonAsync($"/api/entries/{slowEntry}/result", new { finishTime = "00:30:00" });
        await organiser.PostAsJsonAsync($"/api/entries/{fastEntry}/result", new { finishTime = "00:20:00" });

        // Results are public, so an unauthenticated client can read them.
        var anonymous = _factory.CreateClient();
        var results = await (await anonymous.GetAsync($"/api/categories/{categoryId}/results")).JsonAsync();

        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal(fastId, results[0].GetProperty("participantID").GetInt32());
        Assert.Equal(1, results[0].GetProperty("position").GetInt32());
        Assert.Equal(slowId, results[1].GetProperty("participantID").GetInt32());
        Assert.Equal(2, results[1].GetProperty("position").GetInt32());

        var eventResults = await (await anonymous.GetAsync($"/api/events/{eventId}/results")).JsonAsync();
        Assert.Equal(1, eventResults.GetArrayLength());                                   // one category group
        Assert.Equal(2, eventResults[0].GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task Results_ForMissingEventOrCategory_Return404()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/events/999999/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/categories/999999/results")).StatusCode);
    }
}
