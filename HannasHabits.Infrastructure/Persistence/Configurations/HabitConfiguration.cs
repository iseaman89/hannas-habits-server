using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.ToTable("Habits");
        
        builder.HasKey(h => h.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(h => h.Id).ValueGeneratedNever();
        
        builder.Property(h => h.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(h => h.Description)
            .HasMaxLength(500);
        
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