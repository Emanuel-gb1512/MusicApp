using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicApi.Domain.Entities;

namespace MusicApi.Infrastructure.Persistence.Configurations;

// Cada configuración refleja EXACTAMENTE las tablas del script 002.
// Los indices NO se declaran aqui: ya existen en la base por los scripts SQL.

internal static class PropertyBuilderExtensions
{
    /// <summary>Columna datetime2(3) con valor por defecto de SQL Server (UTC ahora).</summary>
    public static PropertyBuilder<DateTime> AsUtcDefaultNow(this PropertyBuilder<DateTime> property) =>
        property.HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

    /// <summary>Columna datetime2(3) sin valor por defecto.</summary>
    public static PropertyBuilder<DateTime> AsUtcDate(this PropertyBuilder<DateTime> property) =>
        property.HasColumnType("datetime2(3)");

    /// <summary>Columna datetime2(3) que admite NULL.</summary>
    public static PropertyBuilder<DateTime?> AsUtcDate(this PropertyBuilder<DateTime?> property) =>
        property.HasColumnType("datetime2(3)");
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.LockoutEndUtc).AsUtcDate();
        builder.Property(x => x.LastLoginAtUtc).AsUtcDate();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.UpdatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.CountryCode).HasColumnType("char(2)");
        builder.Property(x => x.LanguageCode).HasMaxLength(10);
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.UpdatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("UserDevices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.DeviceIdentifier).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AppVersion).HasMaxLength(30);
        builder.Property(x => x.LastSeenAtUtc).AsUtcDate();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.UpdatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.LastActivityAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.ExpiresAtUtc).AsUtcDate();
        builder.Property(x => x.RevokedAtUtc).AsUtcDate();
        builder.Property(x => x.RevokedReason).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(300);

        // Sin cascada desde Users (la cascada llega por UserDevices).
        builder.HasOne(x => x.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Device)
            .WithMany(d => d.Sessions)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.ExpiresAtUtc).AsUtcDate();
        builder.Property(x => x.UsedAtUtc).AsUtcDate();
        builder.Property(x => x.RevokedAtUtc).AsUtcDate();

        builder.HasOne(x => x.Session)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ReplacedByToken)
            .WithMany()
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.ExpiresAtUtc).AsUtcDate();
        builder.Property(x => x.UsedAtUtc).AsUtcDate();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.ExpiresAtUtc).AsUtcDate();
        builder.Property(x => x.UsedAtUtc).AsUtcDate();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
