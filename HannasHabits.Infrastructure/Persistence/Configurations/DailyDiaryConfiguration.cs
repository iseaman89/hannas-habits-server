using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class DailyDiaryConfiguration : IEntityTypeConfiguration<DailyDiary>
{
    public void Configure(EntityTypeBuilder<DailyDiary> builder)
    {
        builder.ToTable("DailyDiary");
        
        builder.HasKey(d => d.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Text)
            .IsRequired()
            .HasMaxLength(2000);
        
        builder.HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();
    }
}