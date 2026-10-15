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
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists all categories under an event.</summary>
    [HttpGet("events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryResponse>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId)) return NotFound();

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventID == eventId)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
        return categories.Select(c => c.ToResponse()).ToList();
    }

    /// <summary>Adds a new age or distance category to an event owned by the logged-in Organiser.</summary>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventID == eventId && c.CategoryName == name))
            return Conflict(new ProblemDetails { Title = "This event already has a category with that name", Status = StatusCodes.Status409Conflict });

        var category = new Category { EventID = eventId, CategoryName = name, Distance = request.Distance };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, category.ToResponse());
    }

    /// <summary>Updates an existing category (Organiser who owns the event only).</summary>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound();
        if (category.Event.OrganiserID != User.GetUserId()) return Forbid();

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventID == category.EventID && c.CategoryName == name && c.CategoryID != id))
            return Conflict(new ProblemDetails { Title = "This event already has a category with that name", Status = StatusCodes.Status409Conflict });

        category.CategoryName = name;
        category.Distance = request.Distance;
        await _db.SaveChangesAsync();

        return category.ToResponse();
    }

    /// <summary>Removes a category (and its entries and results) from an event (Organiser who owns the event only).</summary>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.EventEntries).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound();
        if (category.Event.OrganiserID != User.GetUserId()) return Forbid();

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Enrols the logged-in Participant in this category (creates an EventEntry).</summary>
    [HttpPost("categories/{id:int}/entries")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EntryResponse>> Enrol(int id)
    {
        var participantId = User.GetUserId();

        if (!await _db.Participants.AnyAsync(p => p.ParticipantID == participantId)) return Unauthorized();
        if (!await _db.Categories.AnyAsync(c => c.CategoryID == id)) return NotFound();

        if (await _db.EventEntries.AnyAsync(e => e.ParticipantID == participantId && e.CategoryID == id))
            return Conflict(new ProblemDetails { Title = "You are already entered in this category", Status = StatusCodes.Status409Conflict });

        var entry = new EventEntry { ParticipantID = participantId, CategoryID = id };
        _db.EventEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails { Title = "You are already entered in this category", Status = StatusCodes.Status409Conflict });
        }

        var created = await _db.EventEntries.AsNoTracking().WithDetails().FirstAsync(e => e.EventEntryID == entry.EventEntryID);
        return StatusCode(StatusCodes.Status201Created, created.ToResponse());
    }
}
