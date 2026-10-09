using HannasHabits.Application.Auth;
using HannasHabits.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

// Only what we add to Identity's AspNetUsers; Identity's own columns are configured by IdentityDbContext.
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName)
            .HasMaxLength(AuthLimits.NameMaxLength);

        builder.Property(u => u.LastName)
            .HasMaxLength(AuthLimits.NameMaxLength);
    }
}
