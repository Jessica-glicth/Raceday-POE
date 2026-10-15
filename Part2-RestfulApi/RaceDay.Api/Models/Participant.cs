using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class Participant
{
    public int ParticipantID { get; set; }

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<EventEntry> EventEntries { get; set; } = new List<EventEntry>();
}
