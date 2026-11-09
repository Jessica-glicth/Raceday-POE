using System.Globalization;

namespace RaceDay.Mvc.Helpers;

/// <summary>Small formatting helpers used by the views so every page shows dates, times and statuses the same way.</summary>
public static class Fmt
{
    public static string Date(DateOnly d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Time(TimeSpan? t)
        => t.HasValue ? t.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "-";

    public static string Distance(decimal? d)
        => d.HasValue ? d.Value.ToString("0.##", CultureInfo.InvariantCulture) + " km" : "-";

    public static bool IsUpcoming(DateOnly d) => d >= DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Colour-coded enrolment status: green = Confirmed, amber = Awaiting Results, blue = Completed.</summary>
    public static string StatusClass(string status) => status switch
    {
        "Confirmed" => "status-confirmed",
        "Awaiting Results" => "status-awaiting",
        "Completed" => "status-completed",
        _ => "status-other"
    };

    public static string TypeClass(string eventType) => "type-" + eventType.ToLowerInvariant();

    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 13) return n + "th";
        return (n % 10) switch
        {
            1 => n + "st",
            2 => n + "nd",
            3 => n + "rd",
            _ => n + "th"
        };
    }
}
