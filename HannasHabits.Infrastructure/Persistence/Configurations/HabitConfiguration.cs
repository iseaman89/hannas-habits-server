using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.ToTable("Habits");
        
        builder.HasKey(h => h.Id);
        
        builder.Property(h => h.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(h => h.Description)
            .HasMaxLength(500);
        
        builder.HasMany(h => h.Records)
            .WithOne()
            .HasForeignKey(h => h.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}