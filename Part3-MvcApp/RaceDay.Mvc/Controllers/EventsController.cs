using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class EventsController : AppController
{
    public EventsController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (!ev.Ok || ev.Data == null) return Failure(ev);

        var model = new EventDetailsViewModel
        {
            Event = ev.Data,
            IsOwner = User.IsInRole("Organiser") && ev.Data.OrganiserID == CurrentUserId
        };

        // Results are optional on this page - if they can't be loaded the rest of the page still works.
        var results = await Api.GetAsync<List<CategoryResultsDto>>($"api/events/{id}/results");
        if (results.Ok && results.Data != null) model.Results = results.Data;

        if (User.IsInRole("Participant"))
        {
            var mine = await Api.GetAsync<List<EntryDto>>("api/participants/me/entries");
            if (mine.Ok && mine.Data != null)
                model.EnrolledCategoryIds = mine.Data.Select(e => e.CategoryID).ToHashSet();
        }

        return View(model);
    }
}
