using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitRecordConfiguration : IEntityTypeConfiguration<HabitRecord>
{
    public void Configure(EntityTypeBuilder<HabitRecord> builder)
    {
        builder.ToTable("HabitRecords");
        
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Date)
            .IsRequired();

        builder.HasIndex(r => new { r.HabitId, r.Date })
            .IsUnique();
    }
}