using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public static class Mappings
{
    public static CategoryResponse ToResponse(this Category c)
        => new(c.CategoryID, c.EventID, c.CategoryName, c.Distance);

    /// <summary>Requires Organiser and Categories to be loaded.</summary>
    public static EventResponse ToResponse(this Event e)
        => new(e.EventID, e.OrganiserID,
               $"{e.Organiser.FirstName} {e.Organiser.LastName}",
               e.EventName, e.Description, e.EventDate, e.Location, e.Distance, e.EventType,
               e.Categories.OrderBy(c => c.CategoryName).Select(c => c.ToResponse()).ToList());

    /// <summary>Requires Participant and Category.Event to be loaded.</summary>
    public static EntryResponse ToResponse(this EventEntry en)
        => new(en.EventEntryID, en.ParticipantID,
               $"{en.Participant.FirstName} {en.Participant.LastName}",
               en.Category.EventID, en.Category.Event.EventName, en.Category.Event.EventDate,
               en.CategoryID, en.Category.CategoryName, en.EntryDate);

    /// <summary>Requires EventEntry.Participant and EventEntry.Category.Event to be loaded.</summary>
    public static ResultResponse ToResponse(this Result r)
        => new(r.ResultID, r.EventEntryID, r.EventEntry.ParticipantID,
               $"{r.EventEntry.Participant.FirstName} {r.EventEntry.Participant.LastName}",
               r.EventEntry.Category.EventID, r.EventEntry.Category.Event.EventName,
               r.EventEntry.CategoryID, r.EventEntry.Category.CategoryName,
               r.FinishTime, r.Position, r.Status);

    public static IQueryable<EventEntry> WithDetails(this IQueryable<EventEntry> q)
        => q.Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c.Event);

    public static IQueryable<Result> WithDetails(this IQueryable<Result> q)
        => q.Include(r => r.EventEntry).ThenInclude(e => e.Participant)
            .Include(r => r.EventEntry).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    public static IQueryable<Event> WithDetails(this IQueryable<Event> q)
        => q.Include(e => e.Organiser).Include(e => e.Categories);
}
