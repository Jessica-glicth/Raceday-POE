using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[Authorize(Roles = "Participant")]
public class ParticipantController : AppController
{
    public ParticipantController(ApiClient api) : base(api) { }

    [HttpPost]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var response = await Api.SendAsync<EntryDto>(HttpMethod.Post, $"api/categories/{categoryId}/entries");

        if (response.Ok)
        {
            FlashSuccess("You're enrolled! You can see all your entries below.");
            return RedirectToAction(nameof(Entries));
        }

        FlashError(response.StatusCode == 409 ? "You are already enrolled in that category." : response.Message);
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    [HttpGet]
    public async Task<IActionResult> Entries()
    {
        var response = await Api.GetAsync<List<EntryDto>>("api/participants/me/entries");
        if (!response.Ok || response.Data == null) return Failure(response);
        return View(response.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var response = await Api.SendAsync(HttpMethod.Delete, $"api/entries/{id}");

        if (response.Ok) FlashSuccess("Your enrolment has been withdrawn.");
        else FlashError(response.StatusCode == 404 ? "That enrolment no longer exists." : response.Message);

        return RedirectToAction(nameof(Entries));
    }

    [HttpGet]
    public async Task<IActionResult> Results()
    {
        var response = await Api.GetAsync<List<ResultDto>>("api/participants/me/results");
        if (!response.Ok || response.Data == null) return Failure(response);
        return View(response.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var response = await Api.GetAsync<ParticipantDto>("api/participants/me");
        if (!response.Ok || response.Data == null) return Failure(response);

        var p = response.Data;
        return View(new ParticipantProfileViewModel
        {
            FirstName = p.FirstName,
            LastName = p.LastName,
            Email = p.Email,
            HasProfilePicture = p.HasProfilePicture
        });
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ParticipantProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var response = await Api.SendAsync<ParticipantDto>(HttpMethod.Put, "api/participants/me",
            new { firstName = model.FirstName.Trim(), lastName = model.LastName.Trim() });

        if (!response.Ok || response.Data == null)
        {
            ModelState.AddModelError(string.Empty, response.Message);
            return View(model);
        }

        await RefreshDisplayNameAsync(response.Data.FirstName, response.Data.LastName);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture == null || picture.Length == 0)
        {
            FlashError("Please choose an image to upload.");
            return RedirectToAction(nameof(Profile));
        }

        var problem = ValidateImage(picture);
        if (problem != null)
        {
            FlashError(problem);
            return RedirectToAction(nameof(Profile));
        }

        var response = await Api.UploadAsync<ParticipantDto>("api/participants/me/picture", picture);
        if (response.Ok) FlashSuccess("Your profile picture has been updated.");
        else FlashError(response.Detail ?? response.Message);

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> RemovePicture()
    {
        var response = await Api.SendAsync(HttpMethod.Delete, "api/participants/me/picture");
        if (response.Ok) FlashSuccess("Your profile picture has been removed.");
        else FlashError(response.Message);

        return RedirectToAction(nameof(Profile));
    }
}
