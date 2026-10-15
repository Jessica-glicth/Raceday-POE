using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds the same sample data as the Part 1 SQL script.
/// Every seeded account uses the password: Password123!
/// </summary>
public static class DbSeeder
{
    public const string SeedPassword = "Password123!";

    public static void Seed(RaceDayDbContext db, IPasswordService hasher)
    {
        if (db.Organisers.Any() || db.Participants.Any()) return;

        var hash = hasher.Hash(SeedPassword);

        var thabo = new Organiser { FirstName = "Thabo", LastName = "Nkosi", Email = "thabo.nkosi@raceday.co.za", PasswordHash = hash, ContactNumber = "0821234567" };
        var sarah = new Organiser { FirstName = "Sarah", LastName = "van der Merwe", Email = "sarah.vdm@raceday.co.za", PasswordHash = hash, ContactNumber = "0837654321" };

        var lindiwe = new Participant { FirstName = "Lindiwe", LastName = "Dube", Email = "lindiwe.dube@example.com", PasswordHash = hash };
        var james = new Participant { FirstName = "James", LastName = "Botha", Email = "james.botha@example.com", PasswordHash = hash };

        var cycleTour = new Event { Organiser = thabo, EventName = "Cape Town Cycle Tour", Description = "Scenic cycle around the Cape Peninsula.", EventDate = new DateOnly(2026, 3, 8), Location = "Cape Town", Distance = 109m, EventType = EventType.Cycle };
        var soweto = new Event { Organiser = thabo, EventName = "Soweto Marathon", Description = "Annual marathon through Soweto.", EventDate = new DateOnly(2026, 11, 1), Location = "Soweto", Distance = 42.2m, EventType = EventType.Run };
        var parkRun = new Event { Organiser = sarah, EventName = "Durban Park Run Series", Description = "Friendly weekly 5km park run.", EventDate = new DateOnly(2026, 6, 14), Location = "Durban", Distance = 5m, EventType = EventType.Run };

        var c109 = new Category { Event = cycleTour, CategoryName = "109km Cycle", Distance = 109m };
        var c35 = new Category { Event = cycleTour, CategoryName = "35km Cycle", Distance = 35m };
        var marathon = new Category { Event = soweto, CategoryName = "42.2km Marathon", Distance = 42.2m };
        var half = new Category { Event = soweto, CategoryName = "21.1km Half Marathon", Distance = 21.1m };
        var fiveK = new Category { Event = parkRun, CategoryName = "5km Park Run", Distance = 5m };

        var entry1 = new EventEntry { Participant = lindiwe, Category = c109 };
        var entry2 = new EventEntry { Participant = lindiwe, Category = marathon };   // intentionally no result yet
        var entry3 = new EventEntry { Participant = james, Category = fiveK };

        var result1 = new Result { EventEntry = entry1, FinishTime = new TimeSpan(3, 45, 12), Position = 1, Status = "Finished" };
        var result3 = new Result { EventEntry = entry3, FinishTime = new TimeSpan(0, 22, 47), Position = 1, Status = "Finished" };

        db.AddRange(thabo, sarah, lindiwe, james,
                    cycleTour, soweto, parkRun,
                    c109, c35, marathon, half, fiveK,
                    entry1, entry2, entry3,
                    result1, result3);
        db.SaveChanges();
    }
}
