namespace RaceDay.Mvc.Models;

// These mirror the JSON returned by the Part 2 API. The MVC app holds no database entities of its own.

public record AccountDto(int Id, string Role, string FirstName, string LastName, string Email, string? ContactNumber);

public record OrganiserDto(int OrganiserID, string FirstName, string LastName, string Email, string? ContactNumber, DateTime CreatedAt);

public record ParticipantDto(int ParticipantID, string FirstName, string LastName, string Email, bool HasProfilePicture, DateTime CreatedAt);

public record CategoryDto(int CategoryID, int EventID, string CategoryName, decimal? Distance);

public record EventDto(
    int EventID, int OrganiserID, string OrganiserName, string EventName, string? Description,
    DateOnly EventDate, string? Location, decimal? Distance, string EventType, bool HasBanner,
    List<CategoryDto> Categories);

public record OrganiserEventDto(
    int EventID, string EventName, DateOnly EventDate, string? Location, decimal? Distance,
    string EventType, bool HasBanner, int CategoryCount, int EnrolmentCount, int ResultsCount);

public record EntryDto(
    int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate, string? EventLocation,
    int CategoryID, string CategoryName, DateTime EntryDate,
    string Status, TimeSpan? FinishTime, int? Position);

public record ResultDto(
    int ResultID, int EventEntryID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate, int CategoryID, string CategoryName,
    TimeSpan? FinishTime, int? Position, int TotalFinishers, string Status);

public record CategoryResultsDto(int CategoryID, string CategoryName, List<ResultDto> Results);
