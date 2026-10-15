namespace RaceDay.Api.Models;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static bool TryParse(string? value, out string role)
    {
        role = string.Empty;
        if (string.Equals(value?.Trim(), Organiser, StringComparison.OrdinalIgnoreCase)) { role = Organiser; return true; }
        if (string.Equals(value?.Trim(), Participant, StringComparison.OrdinalIgnoreCase)) { role = Participant; return true; }
        return false;
    }
}
