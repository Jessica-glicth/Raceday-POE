using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
public class EntriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public EntriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists all enrolments for an event (Organiser who owns the event only).</summary>
    [HttpGet("events/{id:int}/entries")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EntryResponse>>> GetForEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        var entries = await _db.EventEntries.AsNoTracking().WithDetails()
            .Where(e => e.Category.EventID == id)
            .ToListAsync();

        return entries.OrderBy(e => e.Category.CategoryName).ThenBy(e => e.Participant.LastName)
                      .Select(e => e.ToResponse()).ToList();
    }

    /// <summary>Withdraws an entry. Allowed for the Participant who owns it, or the Organiser who owns the event.</summary>
    [HttpDelete("entries/{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var entry = await _db.EventEntries
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EventEntryID == id);
        if (entry == null) return NotFound();

        var userId = User.GetUserId();
        var allowed = User.IsInRole(Roles.Participant)
            ? entry.ParticipantID == userId
            : User.IsInRole(Roles.Organiser) && entry.Category.Event.OrganiserID == userId;
        if (!allowed) return Forbid();

        _db.EventEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Captures the finish time for an entry (Organiser who owns the event only). The finishing position is calculated automatically.</summary>
    [HttpPost("entries/{id:int}/result")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(int id, ResultRequest request)
    {
        var finish = request.FinishTime!.Value;
        if (finish <= TimeSpan.Zero || finish >= TimeSpan.FromHours(24))
        {
            ModelState.AddModelError(nameof(request.FinishTime), "Finish time must be greater than 00:00:00 and less than 24 hours.");
            return ValidationProblem(ModelState);
        }

        var entry = await _db.EventEntries
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EventEntryID == id);
        if (entry == null) return NotFound();
        if (entry.Category.Event.OrganiserID != User.GetUserId()) return Forbid();

        if (entry.Result != null)
            return Conflict(new ProblemDetails { Title = "A result has already been recorded for this entry", Status = StatusCodes.Status409Conflict });

        var result = new Result { EventEntryID = id, FinishTime = finish, Status = "Finished" };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        await RecalculatePositionsAsync(entry.CategoryID);

        var created = await _db.Results.AsNoTracking().WithDetails().FirstAsync(r => r.ResultID == result.ResultID);
        return StatusCode(StatusCodes.Status201Created, created.ToResponse());
    }

    /// <summary>Ranks every finished result in the category by finish time (1 = fastest).</summary>
    private async Task RecalculatePositionsAsync(int categoryId)
    {
        var results = await _db.Results
            .Where(r => r.EventEntry.CategoryID == categoryId && r.FinishTime != null)
            .ToListAsync();

        var ordered = results.OrderBy(r => r.FinishTime).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Position = i + 1;

        await _db.SaveChangesAsync();
    }
}
