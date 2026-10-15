using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class Result
{
    public int ResultID { get; set; }

    public int EventEntryID { get; set; }
    public EventEntry EventEntry { get; set; } = null!;

    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position within the category (1 = fastest). Recalculated whenever a result is captured.</summary>
    public int? Position { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Finished";
}
