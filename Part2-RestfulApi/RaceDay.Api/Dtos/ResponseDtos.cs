using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public record AccountResponse(int ID, string Role, string FirstName, string LastName, string Email, string? ContactNumber);

public record OrganiserResponse(int OrganiserID, string FirstName, string LastName, string Email, string? ContactNumber, DateTime CreatedAt);

public record ParticipantResponse(int ParticipantID, string FirstName, string LastName, string Email, bool HasProfilePicture, DateTime CreatedAt);

public record CategoryResponse(int CategoryID, int EventID, string CategoryName, decimal? Distance);

public record EventResponse(
    int EventID, int OrganiserID, string OrganiserName, string EventName, string? Description,
    DateOnly EventDate, string? Location, decimal? Distance, EventType EventType, bool HasBanner,
    List<CategoryResponse> Categories);

/// <summary>Summary row for the Organiser dashboard / event management page.</summary>
public record OrganiserEventResponse(
    int EventID, string EventName, DateOnly EventDate, string? Location, decimal? Distance,
    EventType EventType, bool HasBanner, int CategoryCount, int EnrolmentCount, int ResultsCount);

/// <summary>Status is Confirmed (upcoming), Awaiting Results (event passed, no result yet) or Completed (result captured).</summary>
public record EntryResponse(
    int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate, string? EventLocation,
    int CategoryID, string CategoryName, DateTime EntryDate,
    string Status, TimeSpan? FinishTime, int? Position);

public record ResultResponse(
    int ResultID, int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate, int CategoryID, string CategoryName,
    TimeSpan? FinishTime, int? Position, int TotalFinishers, string Status);

public record CategoryResultsResponse(int CategoryID, string CategoryName, List<ResultResponse> Results);
