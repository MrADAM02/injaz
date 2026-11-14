using Injaz.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Injaz.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        // Emails are normalized to lower case by the domain, so a plain unique index is case-insensitive in practice.
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.AvatarUrl).HasMaxLength(User.AvatarUrlMaxLength);

        builder.Property(u => u.Locale)
            .HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<Locale>(v, true))
            .HasMaxLength(8);
    }
}
