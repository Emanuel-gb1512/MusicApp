using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure.Auth;
using MusicApi.Infrastructure.Messaging;
using MusicApi.Infrastructure.Persistence;
using MusicApi.Infrastructure.Providers;
using MusicApi.Infrastructure.Providers.Jamendo;
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
        JwtOptions jwtOptions,
        EmailOptions emailOptions,
        GoogleOptions googleOptions,
        JamendoOptions jamendoOptions)
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

        // Correo: una cola en memoria y un servicio en segundo plano que la vacía.
        services.AddSingleton(emailOptions);
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());

        if (emailOptions.IsProvider("Smtp"))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else if (emailOptions.IsProvider("Log"))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, NullEmailSender>();
        }

        services.AddHostedService<EmailDispatchService>();

        // Inicio de sesión con proveedores externos.
        services.AddSingleton(googleOptions);
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Autenticación y cuenta: usan la base de datos, por eso son scoped (uno por petición).
        services.AddScoped<SessionIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountSecurityService, AccountSecurityService>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        // Proveedores de música: cada adaptador se registra como IMusicProvider.
        services.AddSingleton(jamendoOptions);

        services.AddHttpClient<JamendoProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.jamendo.com/v3.0/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TONYX/0.1");
        });

        services.AddTransient<IMusicProvider>(sp => sp.GetRequiredService<JamendoProvider>());

        services.AddScoped<IProviderCatalogService, ProviderCatalogService>();
        services.AddScoped<IMusicSearchService, MusicSearchService>();

        return services;
    }
}