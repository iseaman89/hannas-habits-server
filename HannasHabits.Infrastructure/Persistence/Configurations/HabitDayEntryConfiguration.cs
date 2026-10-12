using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitDayEntryConfiguration : IEntityTypeConfiguration<HabitDayEntry>
{
    public void Configure(EntityTypeBuilder<HabitDayEntry> builder)
    {
        // The check constraint mirrors the Domain rule for rows that bypass the application.
        builder.ToTable("HabitDayEntries", table => table.HasCheckConstraint(
            "CK_HabitDayEntries_Date",
            $"\"Date\" BETWEEN DATE '{HabitDayEntry.MinDate:yyyy-MM-dd}' AND DATE '{HabitDayEntry.MaxDate:yyyy-MM-dd}'"));

        builder.HasKey(e => e.Id);

        // The Id is assigned by EntityBase, never by the database. Without this EF treats a new entity that only
        // reaches the context through a tracked parent's collection as already existing (UPDATE instead of INSERT).
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Date).IsRequired();

        // An entry only references its habit by id (another aggregate); the habit's days go with the habit.
        builder.HasOne<Habit>()
            .WithMany()
            .HasForeignKey(e => e.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tags)
            .WithOne()
            .HasForeignKey(t => t.HabitDayEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // One entry per habit and day; also serves the range read (habit + date range).
        builder.HasIndex(e => new { e.HabitId, e.Date })
            .IsUnique();
    }
}
