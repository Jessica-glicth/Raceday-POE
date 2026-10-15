using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Gets all results for an event (leaderboard), grouped by category.</summary>
    [HttpGet("events/{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryResultsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryResultsResponse>>> GetForEvent(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == id)) return NotFound();

        var results = await _db.Results.AsNoTracking().WithDetails()
            .Where(r => r.EventEntry.Category.EventID == id)
            .ToListAsync();

        return results
            .Select(r => r.ToResponse())
            .GroupBy(r => new { r.CategoryID, r.CategoryName })
            .OrderBy(g => g.Key.CategoryName)
            .Select(g => new CategoryResultsResponse(
                g.Key.CategoryID, g.Key.CategoryName,
                g.OrderBy(r => r.FinishTime).ToList()))
            .ToList();
    }

    /// <summary>Gets the results for a single category, ordered by finish time.</summary>
    [HttpGet("categories/{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultResponse>>> GetForCategory(int id)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryID == id)) return NotFound();

        var results = await _db.Results.AsNoTracking().WithDetails()
            .Where(r => r.EventEntry.CategoryID == id)
            .ToListAsync();

        return results.OrderBy(r => r.FinishTime).Select(r => r.ToResponse()).ToList();
    }
}
