using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class DailyDiaryConfiguration : IEntityTypeConfiguration<DailyDiary>
{
    public void Configure(EntityTypeBuilder<DailyDiary> builder)
    {
        // The check constraints mirror the Domain rules for rows that bypass the application (NULL passes: all optional).
        builder.ToTable("DailyDiary", table =>
        {
            table.HasCheckConstraint("CK_DailyDiary_Mood",
                $"\"Mood\" BETWEEN {(int)Mood.Terrible} AND {(int)Mood.Excellent}");
            table.HasCheckConstraint("CK_DailyDiary_Body",
                $"\"Body\" BETWEEN {Percentage.MinValue} AND {Percentage.MaxValue}");
            table.HasCheckConstraint("CK_DailyDiary_Mind",
                $"\"Mind\" BETWEEN {Percentage.MinValue} AND {Percentage.MaxValue}");
        });

        builder.HasKey(d => d.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Mood is stored as its number (1-5, higher = better); NULL = no mood chosen.
        builder.Property(d => d.Mood);

        builder.Property(d => d.Body).HasConversion<PercentageConverter>();
        builder.Property(d => d.Mind).HasConversion<PercentageConverter>();

        builder.Property(d => d.Highlight)
            .HasMaxLength(DiaryContent.HighlightMaxLength);

        // Short ordered lines: a native text[] keeps the order and needs no table of its own (see StringListConverter).
        builder.Property(d => d.Grateful)
            .HasConversion<StringListConverter, ReadOnlyListComparer<string>>()
            .IsRequired();
        builder.Property(d => d.Learned)
            .HasConversion<StringListConverter, ReadOnlyListComparer<string>>()
            .IsRequired();

        // Tasks are only ever read and written together with their day, never queried across days, so one JSON array
        // is enough. A table of its own would only pay off for questions like "all my open tasks".
        builder.Property(d => d.Tasks)
            .HasConversion<DiaryTasksConverter, ReadOnlyListComparer<DiaryTask>>()
            .HasColumnType("jsonb")
            .IsRequired();

        // Referential integrity to the Identity user without the Domain knowing about Identity:
        // no navigation on either side, the relationship exists only in the persistence model.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One entry per user and day; also serves the calendar's range query (user + date range).
        builder.HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();
    }
}
