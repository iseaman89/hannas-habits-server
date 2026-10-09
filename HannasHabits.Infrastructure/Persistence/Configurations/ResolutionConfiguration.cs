using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class ResolutionConfiguration : IEntityTypeConfiguration<Resolution>
{
    public void Configure(EntityTypeBuilder<Resolution> builder)
    {
        // The check constraint mirrors the Domain rule for rows that bypass the application.
        builder.ToTable("Resolutions", table => table.HasCheckConstraint(
            "CK_Resolutions_Year",
            $"\"Year\" BETWEEN {Resolution.MinYear} AND {Resolution.MaxYear}"));

        builder.HasKey(r => r.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Year).IsRequired();

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(Resolution.TitleMaxLength);

        builder.Property(r => r.Kept).IsRequired();

        // Referential integrity to the Identity user without the Domain knowing about Identity:
        // no navigation on either side, the relationship exists only in the persistence model.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A resolution only references its habit by id (another aggregate). Deleting the habit keeps the resolution and
        // clears the link - the database does it, so it also holds for habits removed without loading their resolutions.
        builder.HasOne<Habit>()
            .WithMany()
            .HasForeignKey(r => r.HabitId)
            .OnDelete(DeleteBehavior.SetNull);

        // "A user's resolutions of a year" is the only way they are read.
        builder.HasIndex(r => new { r.UserId, r.Year });
    }
}
