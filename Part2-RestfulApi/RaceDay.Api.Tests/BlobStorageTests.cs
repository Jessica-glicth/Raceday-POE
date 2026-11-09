using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

/// <summary>Image upload endpoints (Azure Blob Storage, faked in memory for the tests).</summary>
public class BlobStorageTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public BlobStorageTests(RaceDayFactory factory) => _factory = factory;

    // PNG signature followed by a few filler bytes - enough to pass the API's real-file-type check.
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_AndAnyoneCanDownloadIt()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var upload = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True((await upload.JsonAsync()).GetProperty("hasBanner").GetBoolean());

        var download = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Organiser_UploadBanner_FileThatIsNotAnImage_Returns400()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var fake = System.Text.Encoding.UTF8.GetBytes("this is definitely not a png");
        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(fake, "banner.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UploadBanner_DisallowedExtension_Returns400()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await participant.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);

        var response = await other.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_UploadBanner_Returns401()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await _factory.CreateClient().PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBanner_WhenNoneUploaded_Returns404()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner")).StatusCode);
    }

    [Fact]
    public async Task Organiser_ReplacingBanner_RemovesTheOldImage()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var before = _factory.Storage.Count;

        await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "one.png"));
        await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "two.png"));

        Assert.Equal(before + 1, _factory.Storage.Count);   // only one banner stored for the event
    }

    [Fact]
    public async Task Organiser_CanDeleteBanner()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{eventId}/banner")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{eventId}/banner")).StatusCode);
    }

    [Fact]
    public async Task DeletingAnEvent_AlsoRemovesItsBanner()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var before = _factory.Storage.Count;
        await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png"));

        await organiser.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(before, _factory.Storage.Count);
    }

    [Fact]
    public async Task Participant_CanUploadAndViewOwnProfilePicture()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(_factory, "Participant");

        var upload = await participant.PostAsync("/api/participants/me/picture", FileContent(Png, "me.png"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True((await upload.JsonAsync()).GetProperty("hasProfilePicture").GetBoolean());

        var download = await participant.GetAsync("/api/participants/me/picture");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProfilePicture_Endpoints_RejectOrganisersAndAnonymousUsers()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await organiser.PostAsync("/api/participants/me/picture", FileContent(Png, "me.png"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().GetAsync("/api/participants/me/picture")).StatusCode);
    }
}
