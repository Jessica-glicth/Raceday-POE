using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists all events. Optional filters: date, location, eventType.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetAll(
        [FromQuery] DateTime? date, [FromQuery] string? location, [FromQuery] EventType? eventType)
    {
        IQueryable<Event> query = _db.Events.AsNoTracking().WithDetails();

        if (date.HasValue)
        {
            var d = DateOnly.FromDateTime(date.Value);
            query = query.Where(e => e.EventDate == d);
        }
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(e => e.Location != null && e.Location.Contains(location));
        if (eventType.HasValue)
            query = query.Where(e => e.EventType == eventType.Value);

        var events = await query.ToListAsync();
        return events.OrderBy(e => e.EventDate).Select(e => e.ToResponse()).ToList();
    }

    /// <summary>Gets full details for a single event, including its categories.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().WithDetails().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        return ev.ToResponse();
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create(EventRequest request)
    {
        var ev = new Event
        {
            OrganiserID = User.GetUserId(),
            EventName = request.EventName.Trim(),
            Description = request.Description,
            EventDate = request.EventDate!.Value,
            Location = request.Location,
            Distance = request.Distance,
            EventType = request.EventType!.Value
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var created = await _db.Events.AsNoTracking().WithDetails().FirstAsync(e => e.EventID == ev.EventID);
        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, created.ToResponse());
    }

    /// <summary>Updates an event owned by the logged-in Organiser.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.WithDetails().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location;
        ev.Distance = request.Distance;
        ev.EventType = request.EventType!.Value;
        await _db.SaveChangesAsync();

        return ev.ToResponse();
    }

    /// <summary>Deletes an event (and its categories, entries and results) owned by the logged-in Organiser.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.EventEntries).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        _db.Events.Remove(ev);   // cascades to categories, entries and results
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
