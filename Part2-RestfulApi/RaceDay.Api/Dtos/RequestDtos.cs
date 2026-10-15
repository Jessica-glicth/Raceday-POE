using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    /// <summary>"Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(100)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = string.Empty;
    /// <summary>Optional contact number (used for Organisers).</summary>
    [MaxLength(20)] public string? ContactNumber { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateOrganiserRequest
{
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [MaxLength(20)] public string? ContactNumber { get; set; }
}

public class UpdateParticipantRequest
{
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
}

public class EventRequest
{
    [Required, MaxLength(100)] public string EventName { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [Required] public DateOnly? EventDate { get; set; }
    [MaxLength(100)] public string? Location { get; set; }
    [Range(0.01, 9999.99)] public decimal? Distance { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public EventType? EventType { get; set; }
}

public class CategoryRequest
{
    [Required, MaxLength(50)] public string CategoryName { get; set; } = string.Empty;
    [Range(0.01, 999.99)] public decimal? Distance { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. "01:45:30".</summary>
    [Required] public TimeSpan? FinishTime { get; set; }
}
