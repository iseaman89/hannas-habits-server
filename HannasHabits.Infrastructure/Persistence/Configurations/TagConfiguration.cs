using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);

        // The Id is assigned by EntityBase, never by the database.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasConversion<TagNameConverter>()
            .IsRequired()
            .HasMaxLength(TagName.MaxLength);

        // The case-folded name, computed by the Domain (not by the database, whose collation would make "krank" and
        // "Krank" the same or not depending on how it was set up). Unique per user below.
        builder.Property(t => t.NameKey)
            .IsRequired()
            .HasMaxLength(TagName.MaxLength);

        // Referential integrity to the Identity user without the Domain knowing about Identity:
        // no navigation on either side, the relationship exists only in the persistence model.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One tag per name and user, ignoring case; also serves "the user's tags" and the lookup by name.
        builder.HasIndex(t => new { t.UserId, t.NameKey })
            .IsUnique();
    }
}
