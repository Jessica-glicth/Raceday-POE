using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/organisers/me")]
[Authorize(Roles = Roles.Organiser)]
public class OrganisersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public OrganisersController(RaceDayDbContext db) => _db = db;

    /// <summary>Gets the profile of the logged-in Organiser.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganiserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganiserResponse>> Get()
    {
        var o = await _db.Organisers.FindAsync(User.GetUserId());
        if (o == null) return Unauthorized();
        return new OrganiserResponse(o.OrganiserID, o.FirstName, o.LastName, o.Email, o.ContactNumber, o.CreatedAt);
    }

    /// <summary>Updates the logged-in Organiser's name and contact details.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(OrganiserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganiserResponse>> Update(UpdateOrganiserRequest request)
    {
        var o = await _db.Organisers.FindAsync(User.GetUserId());
        if (o == null) return Unauthorized();

        o.FirstName = request.FirstName.Trim();
        o.LastName = request.LastName.Trim();
        o.ContactNumber = request.ContactNumber;
        await _db.SaveChangesAsync();

        return new OrganiserResponse(o.OrganiserID, o.FirstName, o.LastName, o.Email, o.ContactNumber, o.CreatedAt);
    }

    /// <summary>Lists the logged-in Organiser's own events with category, enrolment and result counts (dashboard data).</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(List<OrganiserEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<OrganiserEventResponse>>> GetMyEvents()
    {
        var id = User.GetUserId();

        var events = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserID == id)
            .Select(e => new OrganiserEventResponse(
                e.EventID, e.EventName, e.EventDate, e.Location, e.Distance, e.EventType,
                e.BannerBlobName != null,
                e.Categories.Count,
                e.Categories.SelectMany(c => c.EventEntries).Count(),
                e.Categories.SelectMany(c => c.EventEntries).Count(en => en.Result != null)))
            .ToListAsync();

        return events.OrderBy(e => e.EventDate).ToList();
    }
}
