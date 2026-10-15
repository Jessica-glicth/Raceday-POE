using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public AuthTests(RaceDayFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Register_ValidAccount_Returns201_WithoutPassword(string role)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.com";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { role, firstName = "Ada", lastName = "Lovelace", email, password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(role, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(_factory, "Participant");

        // Same email, even with the other role, must be rejected.
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { role = "Organiser", firstName = "Dup", lastName = "User", email, password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { role = "Admin", firstName = "Bad", lastName = "Role", email = $"{Guid.NewGuid():N}@test.com", password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { role = "Participant", firstName = "Short", lastName = "Pass", email = $"{Guid.NewGuid():N}@test.com", password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(_factory, "Participant");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var participant = await db.Participants.SingleAsync(p => p.Email == email);

        Assert.NotEqual(TestHelpers.Password, participant.PasswordHash);
        Assert.DoesNotContain(TestHelpers.Password, participant.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_AndStartsSession()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var client = _factory.CreateClient();   // fresh client, no session yet
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestHelpers.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var profile = await client.GetAsync("/api/participants/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@nowhere.com", password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/participants/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/organisers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/participants/me/entries")).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/participants/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/participants/me")).StatusCode);
    }

    [Fact]
    public async Task PublicEndpoints_AreAvailableWithoutLogin()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/events")).StatusCode);
    }
}
