using Microsoft.EntityFrameworkCore;
using MusicApi.Domain.Entities;

namespace MusicApi.Infrastructure.Persistence;

/// <summary>
/// Puente entre las entidades de C# y las tablas de TonyxDb.
/// El esquema lo definen los scripts SQL de la carpeta database/, no EF Core.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();
    public DbSet<ConnectedProvider> ConnectedProviders => Set<ConnectedProvider>();
    public DbSet<Genre> Genres => Set<Genre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carga automáticamente todas las clases IEntityTypeConfiguration de este proyecto.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
