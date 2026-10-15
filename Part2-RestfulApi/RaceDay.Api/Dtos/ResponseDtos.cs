using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public record AccountResponse(int ID, string Role, string FirstName, string LastName, string Email, string? ContactNumber);

public record OrganiserResponse(int OrganiserID, string FirstName, string LastName, string Email, string? ContactNumber, DateTime CreatedAt);

public record ParticipantResponse(int ParticipantID, string FirstName, string LastName, string Email, DateTime CreatedAt);

public record CategoryResponse(int CategoryID, int EventID, string CategoryName, decimal? Distance);

public record EventResponse(
    int EventID, int OrganiserID, string OrganiserName, string EventName, string? Description,
    DateOnly EventDate, string? Location, decimal? Distance, EventType EventType,
    List<CategoryResponse> Categories);

public record EntryResponse(
    int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate,
    int CategoryID, string CategoryName, DateTime EntryDate);

public record ResultResponse(
    int ResultID, int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, int CategoryID, string CategoryName,
    TimeSpan? FinishTime, int? Position, string Status);

public record CategoryResultsResponse(int CategoryID, string CategoryName, List<ResultResponse> Results);
