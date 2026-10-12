using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class SubHabitConfiguration : IEntityTypeConfiguration<SubHabit>
{
    public void Configure(EntityTypeBuilder<SubHabit> builder)
    {
        // The check constraint mirrors the Domain rule ("at least one weekday, only the seven") for rows that bypass
        // the application.
        builder.ToTable("SubHabits", table => table.HasCheckConstraint(
            "CK_SubHabits_Weekdays", $"\"Weekdays\" BETWEEN 1 AND {WeekdaySet.AllDaysMask}"));

        builder.HasKey(s => s.Id);

        // The Id is assigned by EntityBase, never by the database. Without this EF treats a new entity that only
        // reaches the context through a tracked parent's collection as already existing (UPDATE instead of INSERT).
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Title)
            .HasConversion<HabitTitleConverter>()
            .IsRequired()
            .HasMaxLength(HabitTitle.MaxLength);

        builder.Property(s => s.Weekdays)
            .HasConversion<WeekdaySetConverter>()
            .IsRequired();

        builder.HasMany(s => s.Records)
            .WithOne()
            .HasForeignKey(r => r.SubHabitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
