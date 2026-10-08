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

        // The Id is assigned by EntityBase, never by the database. Without this EF treats a new entity that only
        // reaches the context through a tracked parent's collection as already existing (UPDATE instead of INSERT).
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Date)
            .IsRequired();

        builder.HasIndex(r => new { r.HabitId, r.Date })
            .IsUnique();
    }
}