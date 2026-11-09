using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[Authorize(Roles = "Organiser")]
public class OrganiserController : AppController
{
    public OrganiserController(ApiClient api) : base(api) { }

    // ---------- Dashboard & events list ----------

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var response = await Api.GetAsync<List<OrganiserEventDto>>("api/organisers/me/events");
        if (!response.Ok || response.Data == null) return Failure(response);
        return View(new DashboardViewModel { Events = response.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var response = await Api.GetAsync<List<OrganiserEventDto>>("api/organisers/me/events");
        if (!response.Ok || response.Data == null) return Failure(response);
        return View(response.Data);
    }

    // ---------- Create / edit / delete event ----------

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel());

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        var imageProblem = ValidateImage(model.BannerFile);
        if (imageProblem != null) ModelState.AddModelError(nameof(model.BannerFile), imageProblem);
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await Api.SendAsync<EventDto>(HttpMethod.Post, "api/events", ToRequest(model));
        if (!created.Ok || created.Data == null)
        {
            ModelState.AddModelError(string.Empty, created.Message);
            return View("EventForm", model);
        }

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<EventDto>($"api/events/{created.Data.EventID}/banner", model.BannerFile);
            if (!upload.Ok)
            {
                FlashError($"The event was created, but the banner could not be uploaded: {upload.Detail ?? upload.Message}");
                return RedirectToAction(nameof(EditEvent), new { id = created.Data.EventID });
            }
        }

        FlashSuccess($"\"{created.Data.EventName}\" has been created. Now add some categories.");
        return RedirectToAction(nameof(Categories), new { id = created.Data.EventID });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        return View("EventForm", new EventFormViewModel
        {
            EventID = ev.EventID,
            EventName = ev.EventName,
            Description = ev.Description,
            EventDate = ev.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = ev.Location,
            Distance = ev.Distance,
            EventType = ev.EventType,
            HasBanner = ev.HasBanner
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventID = id;

        var imageProblem = ValidateImage(model.BannerFile);
        if (imageProblem != null) ModelState.AddModelError(nameof(model.BannerFile), imageProblem);
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await Api.SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", ToRequest(model));
        if (!updated.Ok)
        {
            if (updated.StatusCode is 403 or 404) return Failure(updated);
            ModelState.AddModelError(string.Empty, updated.Message);
            return View("EventForm", model);
        }

        var bannerMessage = string.Empty;

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<EventDto>($"api/events/{id}/banner", model.BannerFile);
            if (!upload.Ok) bannerMessage = $" The banner could not be uploaded: {upload.Detail ?? upload.Message}";
        }
        else if (model.RemoveBanner && model.HasBanner)
        {
            var removed = await Api.SendAsync(HttpMethod.Delete, $"api/events/{id}/banner");
            if (!removed.Ok) bannerMessage = $" The banner could not be removed: {removed.Message}";
        }

        if (bannerMessage.Length > 0) FlashError("Event details saved." + bannerMessage);
        else FlashSuccess("Event updated.");

        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/events/{id}");

        if (response.Ok) FlashSuccess("The event and everything linked to it has been deleted.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;
        return View(new CategoriesViewModel { Event = ev });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(int id, string? categoryName, decimal? distance)
    {
        var name = categoryName?.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > 50)
        {
            FlashError("Please enter a category name of up to 50 characters.");
            return RedirectToAction(nameof(Categories), new { id });
        }
        if (distance is < 0.01m or > 999.99m)
        {
            FlashError("Distance must be between 0.01 and 999.99 km, or left empty for an age category.");
            return RedirectToAction(nameof(Categories), new { id });
        }

        var response = await Api.SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{id}/categories",
            new { categoryName = name, distance });

        if (response.Ok) FlashSuccess($"Category \"{name}\" added.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Categories), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int categoryId)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/categories/{categoryId}");

        if (response.Ok) FlashSuccess("Category removed.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Categories), new { id });
    }

    // ---------- Enrolments ----------

    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var entries = await Api.GetAsync<List<EntryDto>>($"api/events/{id}/entries");
        if (!entries.Ok || entries.Data == null) return Failure(entries);

        return View(new EnrolmentsViewModel { Event = ev, Entries = entries.Data });
    }

    // ---------- Results ----------

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var entries = await Api.GetAsync<List<EntryDto>>($"api/events/{id}/entries");
        if (!entries.Ok || entries.Data == null) return Failure(entries);

        return View(new ResultsEntryViewModel { Event = ev, Entries = entries.Data });
    }

    [HttpPost]
    public async Task<IActionResult> CaptureResult(int id, int entryId, string? finishTime)
    {
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss" };
        if (string.IsNullOrWhiteSpace(finishTime) ||
            !TimeSpan.TryParseExact(finishTime.Trim(), formats, CultureInfo.InvariantCulture, out var time) ||
            time <= TimeSpan.Zero)
        {
            FlashError("Please enter the finish time as hours:minutes:seconds, for example 01:45:30.");
            return RedirectToAction(nameof(Results), new { id });
        }

        var response = await Api.SendAsync<ResultDto>(HttpMethod.Post, $"api/entries/{entryId}/result",
            new { finishTime = time.ToString("c", CultureInfo.InvariantCulture) });

        if (response.Ok)
        {
            var position = response.Data?.Position;
            FlashSuccess(position.HasValue
                ? $"Result saved and published. Current position: {position}."
                : "Result saved and published.");
        }
        else
        {
            FlashError(response.StatusCode == 409 ? "A result has already been recorded for that participant." : response.Message);
        }

        return RedirectToAction(nameof(Results), new { id });
    }

    // ---------- Profile ----------

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var response = await Api.GetAsync<OrganiserDto>("api/organisers/me");
        if (!response.Ok || response.Data == null) return Failure(response);

        var o = response.Data;
        return View(new OrganiserProfileViewModel
        {
            FirstName = o.FirstName,
            LastName = o.LastName,
            Email = o.Email,
            ContactNumber = o.ContactNumber
        });
    }

    [HttpPost]
    public async Task<IActionResult> Profile(OrganiserProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var response = await Api.SendAsync<OrganiserDto>(HttpMethod.Put, "api/organisers/me", new
        {
            firstName = model.FirstName.Trim(),
            lastName = model.LastName.Trim(),
            contactNumber = model.ContactNumber
        });

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty, response.Message);
            return View(model);
        }

        await RefreshDisplayNameAsync(response.Data.FirstName, response.Data.LastName);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    // ---------- Helpers ----------

    /// <summary>Loads an event and makes sure the logged-in Organiser owns it.</summary>
    private async Task<(EventDto? Event, IActionResult? Failure)> LoadOwnedEventAsync(int id)
    {
        var response = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (!response.Ok || response.Data == null) return (null, Failure(response));

        if (response.Data.OrganiserID != CurrentUserId)
            return (null, RedirectToAction("AccessDenied", "Account"));

        return (response.Data, null);
    }

    private static object ToRequest(EventFormViewModel m) => new
    {
        eventName = m.EventName.Trim(),
        description = m.Description,
        eventDate = m.EventDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        location = m.Location,
        distance = m.Distance,
        eventType = m.EventType
    };
}
