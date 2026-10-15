using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryID { get; set; }

    public int EventID { get; set; }
    public Event Event { get; set; } = null!;

    [Required, MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    public decimal? Distance { get; set; }

    public ICollection<EventEntry> EventEntries { get; set; } = new List<EventEntry>();
}
