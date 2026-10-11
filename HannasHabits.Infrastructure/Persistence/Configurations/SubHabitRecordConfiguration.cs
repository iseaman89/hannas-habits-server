using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class SubHabitRecordConfiguration : IEntityTypeConfiguration<SubHabitRecord>
{
    public void Configure(EntityTypeBuilder<SubHabitRecord> builder)
    {
        builder.ToTable("SubHabitRecords");

        builder.HasKey(r => r.Id);

        // The Id is assigned by EntityBase, never by the database (see HabitRecordConfiguration).
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Date).IsRequired();

        // A sub-habit is done on a day only once; also serves the range read (sub-habit + date range).
        builder.HasIndex(r => new { r.SubHabitId, r.Date })
            .IsUnique();
    }
}
