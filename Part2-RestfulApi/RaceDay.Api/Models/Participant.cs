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

    /// <summary>Name of the profile picture blob in Azure Blob Storage (null when none uploaded).</summary>
    [MaxLength(200)]
    public string? ProfilePictureBlobName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<EventEntry> EventEntries { get; set; } = new List<EventEntry>();
}
