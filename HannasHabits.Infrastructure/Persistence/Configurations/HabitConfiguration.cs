using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        // The check constraint mirrors the Domain rule "at least one day" for rows that bypass the application.
        builder.ToTable("Habits", table => table.HasCheckConstraint(
            "CK_Habits_Schedule",
            $"\"Schedule\" BETWEEN {HabitScheduleConverter.MinMask} AND {HabitScheduleConverter.MaxMask}"));
        
        builder.HasKey(h => h.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(h => h.Id).ValueGeneratedNever();
        
        builder.Property(h => h.Title)
            .HasConversion<HabitTitleConverter>()
            .IsRequired()
            .HasMaxLength(HabitTitle.MaxLength);

        builder.Property(h => h.Description)
            .HasMaxLength(Habit.DescriptionMaxLength);

        builder.Property(h => h.Schedule)
            .HasConversion<HabitScheduleConverter>()
            .IsRequired();
        
        // Referential integrity to the Identity user without the Domain knowing about Identity:
        // no navigation on either side, the relationship exists only in the persistence model.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.Records)
            .WithOne()
            .HasForeignKey(h => h.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}