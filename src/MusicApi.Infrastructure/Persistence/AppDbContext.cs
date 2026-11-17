using Microsoft.EntityFrameworkCore;
using MusicApi.Domain.Entities;

namespace MusicApi.Infrastructure.Persistence;

/// <summary>
/// Puente entre las entidades de C# y las tablas de TonyxDb.
/// El esquema lo definen los scripts SQL de la carpeta database/, no EF Core.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Versionado y catálogo de proveedores / géneros
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();
    public DbSet<ConnectedProvider> ConnectedProviders => Set<ConnectedProvider>();
    public DbSet<Genre> Genres => Set<Genre>();

    // Identidad y sesiones (script 002)
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Cuentas vinculadas a proveedores externos (script 003)
    public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carga automáticamente todas las clases IEntityTypeConfiguration de este proyecto.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}