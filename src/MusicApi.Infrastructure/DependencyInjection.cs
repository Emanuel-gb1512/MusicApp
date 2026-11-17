using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure.Auth;
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

        // Reloj del sistema (se puede sustituir en las pruebas).
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

        // Seguridad: no guardan estado, por eso son singleton.
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Autenticación y cuenta: usan la base de datos, por eso son scoped (uno por petición).
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();
        services.AddScoped<IAccountService, AccountService>();

        return services;
    }
}