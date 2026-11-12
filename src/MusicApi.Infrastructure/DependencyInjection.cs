using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure.Persistence;
using MusicApi.Infrastructure.Security;

namespace MusicApi.Infrastructure;

/// <summary>
/// Registra todos los servicios de la capa Infrastructure.
/// Se llama una sola vez desde Program.cs de la API.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

        // Seguridad: no guardan estado, por eso son singleton.
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
