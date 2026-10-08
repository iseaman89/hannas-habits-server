using HannasHabits.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    // Hex of a SHA-256 hash.
    public const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);

        // The Id is assigned by RefreshToken.Create, never by the database.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength);

        // Every refresh looks the token up by its hash.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasIndex(t => t.UserId);

        // Postgres' system column xmin changes with every update of the row. As concurrency token it makes two
        // refreshes that rotate the same token fail on the second save instead of both succeeding.
        builder.Property<uint>("xmin").IsRowVersion();

        // Deleting a user deletes their sessions; same "relationship only in the persistence model" idea as Habits.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
