using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class Event
{
    public int EventID { get; set; }

    public int OrganiserID { get; set; }
    public Organiser Organiser { get; set; } = null!;

    [Required, MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateOnly EventDate { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public decimal? Distance { get; set; }

    public EventType EventType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
