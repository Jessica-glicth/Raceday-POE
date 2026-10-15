namespace RaceDay.Api.Models;

public class EventEntry
{
    public int EventEntryID { get; set; }

    public int ParticipantID { get; set; }
    public Participant Participant { get; set; } = null!;

    public int CategoryID { get; set; }
    public Category Category { get; set; } = null!;

    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    public Result? Result { get; set; }
}
