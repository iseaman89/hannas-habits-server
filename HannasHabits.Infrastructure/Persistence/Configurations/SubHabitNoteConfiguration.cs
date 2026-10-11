using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class SubHabitNoteConfiguration : IEntityTypeConfiguration<SubHabitNote>
{
    public void Configure(EntityTypeBuilder<SubHabitNote> builder)
    {
        // The check constraints mirror the Domain rules for rows that bypass the application. char_length counts
        // characters, the Domain counts UTF-16 units: the database is never stricter than the Domain.
        builder.ToTable("SubHabitNotes", table =>
        {
            table.HasCheckConstraint(
                "CK_SubHabitNotes_Date",
                $"\"Date\" BETWEEN DATE '{SubHabitNote.MinDate:yyyy-MM-dd}' AND DATE '{SubHabitNote.MaxDate:yyyy-MM-dd}'");

            table.HasCheckConstraint(
                "CK_SubHabitNotes_Text",
                $"char_length(btrim(\"Text\")) BETWEEN 1 AND {SubHabitNote.TextMaxLength}");
        });

        builder.HasKey(n => n.Id);

        // The Id is assigned by EntityBase, never by the database (see HabitRecordConfiguration).
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Date).IsRequired();

        builder.Property(n => n.Text)
            .IsRequired()
            .HasMaxLength(SubHabitNote.TextMaxLength);

        // A note only references its part by id (another aggregate); the part's notes go with the part - and so with
        // the habit, whose sub-habits cascade.
        builder.HasOne<SubHabit>()
            .WithMany()
            .HasForeignKey(n => n.SubHabitId)
            .OnDelete(DeleteBehavior.Cascade);

        // One note per part and day; also serves the range read.
        builder.HasIndex(n => new { n.SubHabitId, n.Date })
            .IsUnique();
    }
}
