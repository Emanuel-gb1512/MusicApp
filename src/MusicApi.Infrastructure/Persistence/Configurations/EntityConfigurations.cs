using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicApi.Domain.Entities;

namespace MusicApi.Infrastructure.Persistence.Configurations;

// Cada configuración refleja EXACTAMENTE la tabla creada por los scripts SQL.

public sealed class SchemaVersionConfiguration : IEntityTypeConfiguration<SchemaVersion>
{
    public void Configure(EntityTypeBuilder<SchemaVersion> builder)
    {
        builder.ToTable("SchemaVersions");
        builder.HasKey(x => x.ScriptNumber);
        builder.Property(x => x.ScriptNumber).ValueGeneratedNever();
        builder.Property(x => x.ScriptName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AppliedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(x => x.AppliedBy).HasMaxLength(128).IsRequired();
    }
}

public sealed class ConnectedProviderConfiguration : IEntityTypeConfiguration<ConnectedProvider>
{
    public void Configure(EntityTypeBuilder<ConnectedProvider> builder)
    {
        builder.ToTable("ConnectedProviders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("Genres");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
    }
}
