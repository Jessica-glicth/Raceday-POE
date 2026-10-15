using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEntry> EventEntries => Set<EventEntry>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Organiser>(e =>
        {
            e.ToTable("Organiser");
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Participant>(e =>
        {
            e.ToTable("Participant");
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("Event");
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Distance).HasPrecision(6, 2);
            e.HasOne(x => x.Organiser)
             .WithMany(o => o.Events)
             .HasForeignKey(x => x.OrganiserID)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.Property(x => x.Distance).HasPrecision(5, 2);
            e.HasOne(x => x.Event)
             .WithMany(ev => ev.Categories)
             .HasForeignKey(x => x.EventID)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EventID, x.CategoryName }).IsUnique();
        });

        b.Entity<EventEntry>(e =>
        {
            e.ToTable("EventEntry");
            e.HasOne(x => x.Participant)
             .WithMany(p => p.EventEntries)
             .HasForeignKey(x => x.ParticipantID)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
             .WithMany(c => c.EventEntries)
             .HasForeignKey(x => x.CategoryID)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID }).IsUnique();
        });

        b.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasOne(x => x.EventEntry)
             .WithOne(en => en.Result)
             .HasForeignKey<Result>(x => x.EventEntryID)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EventEntryID).IsUnique();
        });
    }
}
