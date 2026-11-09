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
[Route("api/participants/me")]
[Authorize(Roles = Roles.Participant)]
public class ParticipantsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<ParticipantsController> _logger;

    public ParticipantsController(RaceDayDbContext db, IBlobStorageService storage, ILogger<ParticipantsController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    private static ParticipantResponse ToResponse(Participant p)
        => new(p.ParticipantID, p.FirstName, p.LastName, p.Email, p.ProfilePictureBlobName != null, p.CreatedAt);

    /// <summary>Gets the profile of the logged-in Participant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ParticipantResponse>> Get()
    {
        var p = await _db.Participants.FindAsync(User.GetUserId());
        if (p == null) return Unauthorized();
        return ToResponse(p);
    }

    /// <summary>Updates the logged-in Participant's first and last name.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ParticipantResponse>> Update(UpdateParticipantRequest request)
    {
        var p = await _db.Participants.FindAsync(User.GetUserId());
        if (p == null) return Unauthorized();

        p.FirstName = request.FirstName.Trim();
        p.LastName = request.LastName.Trim();
        await _db.SaveChangesAsync();

        return ToResponse(p);
    }

    /// <summary>Uploads (or replaces) the logged-in Participant's profile picture in Azure Blob Storage.</summary>
    /// <remarks>Send multipart/form-data with a single file field named "file". JPG, PNG, GIF or WEBP, max 5 MB.</remarks>
    [HttpPost("picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ParticipantResponse>> UploadPicture(IFormFile file)
    {
        var id = User.GetUserId();
        var p = await _db.Participants.FindAsync(id);
        if (p == null) return Unauthorized();

        var error = ImageUpload.Validate(file, out var extension, out var contentType);
        if (error != null)
        {
            ModelState.AddModelError("file", error);
            return ValidationProblem(ModelState);
        }

        var newBlobName = $"profiles/participant-{id}-{Guid.NewGuid():N}{extension}";
        try
        {
            await using var stream = file.OpenReadStream();
            await _storage.UploadAsync(newBlobName, stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile picture upload failed for participant {ParticipantId}", id);
            return this.StorageUnavailable();
        }

        var oldBlobName = p.ProfilePictureBlobName;
        p.ProfilePictureBlobName = newBlobName;
        await _db.SaveChangesAsync();

        await _storage.TryDeleteAsync(oldBlobName, _logger);
        return ToResponse(p);
    }

    /// <summary>Downloads the logged-in Participant's profile picture.</summary>
    [HttpGet("picture")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPicture()
    {
        var userId = User.GetUserId();
        var p = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.ParticipantID == userId);
        if (p?.ProfilePictureBlobName == null) return NotFound();

        BlobContent? blob;
        try
        {
            blob = await _storage.DownloadAsync(p.ProfilePictureBlobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile picture download failed for participant {ParticipantId}", userId);
            return this.StorageUnavailable();
        }

        if (blob == null) return NotFound();
        return File(blob.Content, blob.ContentType);
    }

    /// <summary>Removes the logged-in Participant's profile picture.</summary>
    [HttpDelete("picture")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePicture()
    {
        var p = await _db.Participants.FindAsync(User.GetUserId());
        if (p == null) return Unauthorized();

        var blobName = p.ProfilePictureBlobName;
        p.ProfilePictureBlobName = null;
        await _db.SaveChangesAsync();

        await _storage.TryDeleteAsync(blobName, _logger);
        return NoContent();
    }

    /// <summary>Lists the logged-in Participant's event entries with their status (Confirmed, Awaiting Results, Completed).</summary>
    [HttpGet("entries")]
    [ProducesResponseType(typeof(List<EntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EntryResponse>>> GetEntries()
    {
        var id = User.GetUserId();
        var entries = await _db.EventEntries.AsNoTracking().WithDetails()
            .Where(e => e.ParticipantID == id)
            .ToListAsync();

        return entries.OrderBy(e => e.Category.Event.EventDate).Select(e => e.ToResponse()).ToList();
    }

    /// <summary>Gets the logged-in Participant's race history, including finishing position and total finishers per category.</summary>
    [HttpGet("results")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultResponse>>> GetResults()
    {
        var id = User.GetUserId();
        var results = await _db.Results.AsNoTracking().WithDetails()
            .Where(r => r.EventEntry.ParticipantID == id)
            .ToListAsync();

        var categoryIds = results.Select(r => r.EventEntry.CategoryID).Distinct().ToList();
        var totals = await _db.Results.AsNoTracking()
            .Where(r => categoryIds.Contains(r.EventEntry.CategoryID) && r.FinishTime != null)
            .GroupBy(r => r.EventEntry.CategoryID)
            .Select(g => new { CategoryID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryID, x => x.Count);

        return results
            .OrderByDescending(r => r.EventEntry.Category.Event.EventDate)
            .Select(r => r.ToResponse(totals.TryGetValue(r.EventEntry.CategoryID, out var total) ? total : 0))
            .ToList();
    }
}
