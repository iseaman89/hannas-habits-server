using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class HabitDayTagConfiguration : IEntityTypeConfiguration<HabitDayTag>
{
    public void Configure(EntityTypeBuilder<HabitDayTag> builder)
    {
        builder.ToTable("HabitDayTags");

        builder.HasKey(t => t.Id);

        // The Id is assigned by EntityBase, never by the database (see HabitRecordConfiguration).
        builder.Property(t => t.Id).ValueGeneratedNever();

        // A day entry only references the tag by id (another aggregate). Deleting the tag takes it off its days - the
        // database does it, so it also holds for tags removed without loading their days.
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // A tag only once per day.
        builder.HasIndex(t => new { t.HabitDayEntryId, t.TagId })
            .IsUnique();

        // "Where is this tag used" (use counts, delete, merge) starts from the tag.
        builder.HasIndex(t => t.TagId);
    }
}
