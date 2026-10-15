using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

public static class TestHelpers
{
    public const string Password = "Password123!";

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Registers a new account (which also logs it in) and returns a client that holds its session cookie.</summary>
    public static async Task<(HttpClient Client, int Id, string Email)> RegisterAsync(RaceDayFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            role,
            firstName = "Test",
            lastName = role,
            email,
            password = Password
        });
        response.EnsureSuccessStatusCode();

        var json = await response.JsonAsync();
        return (client, json.GetProperty("id").GetInt32(), email);
    }

    public static async Task<int> CreateEventAsync(HttpClient organiser, string name = "Test Event")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = name,
            description = "A test event",
            eventDate = "2026-12-01",
            location = "Cape Town",
            distance = 10.0,
            eventType = "Run"
        });
        response.EnsureSuccessStatusCode();
        return (await response.JsonAsync()).GetProperty("eventID").GetInt32();
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "10km")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryName = name, distance = 10.0 });
        response.EnsureSuccessStatusCode();
        return (await response.JsonAsync()).GetProperty("categoryID").GetInt32();
    }

    /// <summary>Enrols the participant in a category and returns the new EventEntryID.</summary>
    public static async Task<int> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/entries", null);
        response.EnsureSuccessStatusCode();
        return (await response.JsonAsync()).GetProperty("eventEntryID").GetInt32();
    }
}
