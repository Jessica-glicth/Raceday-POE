using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<EventsController> _logger;

    public EventsController(RaceDayDbContext db, IBlobStorageService storage, ILogger<EventsController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Lists events. Optional filters: date, location, eventType, upcoming (only events today or later).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetAll(
        [FromQuery] DateTime? date, [FromQuery] string? location, [FromQuery] EventType? eventType, [FromQuery] bool? upcoming)
    {
        IQueryable<Event> query = _db.Events.AsNoTracking().WithDetails();

        if (date.HasValue)
        {
            var d = DateOnly.FromDateTime(date.Value);
            query = query.Where(e => e.EventDate == d);
        }
        if (upcoming == true)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(e => e.EventDate >= today);
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

    /// <summary>Deletes an event (and its categories, entries, results and banner) owned by the logged-in Organiser.</summary>
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

        var bannerBlob = ev.BannerBlobName;
        _db.Events.Remove(ev);   // cascades to categories, entries and results
        await _db.SaveChangesAsync();

        await _storage.TryDeleteAsync(bannerBlob, _logger);
        return NoContent();
    }

    /// <summary>Uploads (or replaces) the event banner image in Azure Blob Storage. Organiser who owns the event only.</summary>
    /// <remarks>Send multipart/form-data with a single file field named "file". JPG, PNG, GIF or WEBP, max 5 MB.</remarks>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventResponse>> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.WithDetails().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        var error = ImageUpload.Validate(file, out var extension, out var contentType);
        if (error != null)
        {
            ModelState.AddModelError("file", error);
            return ValidationProblem(ModelState);
        }

        var newBlobName = $"banners/event-{id}-{Guid.NewGuid():N}{extension}";
        try
        {
            await using var stream = file.OpenReadStream();
            await _storage.UploadAsync(newBlobName, stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banner upload failed for event {EventId}", id);
            return this.StorageUnavailable();
        }

        var oldBlobName = ev.BannerBlobName;
        ev.BannerBlobName = newBlobName;
        await _db.SaveChangesAsync();

        await _storage.TryDeleteAsync(oldBlobName, _logger);
        return ev.ToResponse();
    }

    /// <summary>Downloads the event banner image (served through the API from Azure Blob Storage).</summary>
    [HttpGet("{id:int}/banner")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBanner(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev?.BannerBlobName == null) return NotFound();

        BlobContent? blob;
        try
        {
            blob = await _storage.DownloadAsync(ev.BannerBlobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banner download failed for event {EventId}", id);
            return this.StorageUnavailable();
        }

        if (blob == null) return NotFound();
        return File(blob.Content, blob.ContentType);
    }

    /// <summary>Removes the event banner. Organiser who owns the event only.</summary>
    [HttpDelete("{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound();
        if (ev.OrganiserID != User.GetUserId()) return Forbid();

        var blobName = ev.BannerBlobName;
        ev.BannerBlobName = null;
        await _db.SaveChangesAsync();

        await _storage.TryDeleteAsync(blobName, _logger);
        return NoContent();
    }
}
