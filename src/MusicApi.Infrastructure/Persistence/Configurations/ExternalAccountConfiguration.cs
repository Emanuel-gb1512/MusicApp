using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicApi.Domain.Entities;

namespace MusicApi.Infrastructure.Persistence.Configurations;

// Refleja EXACTAMENTE la tabla ExternalAccounts del script 003.
// Los indices NO se declaran aqui: ya existen en la base por los scripts SQL.
public sealed class ExternalAccountConfiguration : IEntityTypeConfiguration<ExternalAccount>
{
    public void Configure(EntityTypeBuilder<ExternalAccount> builder)
    {
        builder.ToTable("ExternalAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.ExternalUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(150);
        builder.Property(x => x.AccessTokenEncrypted).HasColumnType("varbinary(max)");
        builder.Property(x => x.RefreshTokenEncrypted).HasColumnType("varbinary(max)");
        builder.Property(x => x.TokenExpiresAtUtc).AsUtcDate();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.Property(x => x.LinkedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.UnlinkedAtUtc).AsUtcDate();
        builder.Property(x => x.LastSyncAtUtc).AsUtcDate();
        builder.Property(x => x.CreatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.UpdatedAtUtc).AsUtcDefaultNow();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConnectedProvider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}