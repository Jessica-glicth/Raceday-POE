using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models;

public record ErrorViewModel(string Title, string Message);

// ---------- Account ----------

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please choose a role.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose Organiser or Participant.")]
    public string Role { get; set; } = "Participant";

    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50, ErrorMessage = "First name can be at most 50 characters.")]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50, ErrorMessage = "Last name can be at most 50 characters.")]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }
}

// ---------- Public pages ----------

public class HomeIndexViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public DateTime? Date { get; set; }
    public bool IncludePast { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Location) || !string.IsNullOrWhiteSpace(EventType) || Date.HasValue || IncludePast;
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryResultsDto> Results { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
    public bool IsOwner { get; set; }
}

// ---------- Organiser ----------

public class DashboardViewModel
{
    public List<OrganiserEventDto> Events { get; set; } = new();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public int UpcomingCount => Events.Count(e => e.EventDate >= Today);
    public int ResultsCaptured => Events.Sum(e => e.ResultsCount);
    public OrganiserEventDto? NextEvent => Events.Where(e => e.EventDate >= Today).OrderBy(e => e.EventDate).FirstOrDefault();
}

public class EventFormViewModel
{
    public int? EventID { get; set; }

    [Required(ErrorMessage = "Please enter the event name.")]
    [StringLength(100, ErrorMessage = "The name can be at most 100 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [StringLength(100, ErrorMessage = "The location can be at most 100 characters.")]
    public string? Location { get; set; }

    [Range(0.01, 9999.99, ErrorMessage = "Distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Required(ErrorMessage = "Please choose an event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Please choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? BannerFile { get; set; }

    public bool HasBanner { get; set; }
    public bool RemoveBanner { get; set; }
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = null!;
}

public class EnrolmentsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<EntryDto> Entries { get; set; } = new();
}

public class ResultsEntryViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<EntryDto> Entries { get; set; } = new();
}

public class OrganiserProfileViewModel
{
    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }
}

// ---------- Participant ----------

public class ParticipantProfileViewModel
{
    [Required(ErrorMessage = "Please enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name.")]
    [StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public bool HasProfilePicture { get; set; }
}
