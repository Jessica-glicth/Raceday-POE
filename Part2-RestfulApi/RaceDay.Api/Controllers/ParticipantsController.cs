using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/participants/me")]
[Authorize(Roles = Roles.Participant)]
public class ParticipantsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public ParticipantsController(RaceDayDbContext db) => _db = db;

    /// <summary>Gets the profile of the logged-in Participant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ParticipantResponse>> Get()
    {
        var p = await _db.Participants.FindAsync(User.GetUserId());
        if (p == null) return Unauthorized();
        return new ParticipantResponse(p.ParticipantID, p.FirstName, p.LastName, p.Email, p.CreatedAt);
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

        return new ParticipantResponse(p.ParticipantID, p.FirstName, p.LastName, p.Email, p.CreatedAt);
    }

    /// <summary>Lists the logged-in Participant's event entries (past and upcoming).</summary>
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

    /// <summary>Gets the logged-in Participant's own result history.</summary>
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

        return results.OrderByDescending(r => r.EventEntry.Category.Event.EventDate).Select(r => r.ToResponse()).ToList();
    }
}
