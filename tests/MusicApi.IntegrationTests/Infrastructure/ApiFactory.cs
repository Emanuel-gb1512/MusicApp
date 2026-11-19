using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicApi.Api;
using MusicApi.Application.Interfaces;
using Xunit;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>
/// Arranca la API completa para las pruebas, contra la base TonyxDb_Test.
/// Se comparte entre TODAS las pruebas (una sola instancia).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<ApiAssemblyMarker>
{
    public CapturingEmailSender Emails { get; } = new();

    public ApiFactory()
    {
        TestDatabase.RecreateAsync().GetAwaiter().GetResult();

        // La API lee su configuración al arrancar, así que se define con variables de entorno
        // (tienen prioridad sobre appsettings y sobre los user-secrets de tu equipo).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "tonyx-pruebas-de-integracion-clave-de-firma-0123456789-abcdef");
        Environment.SetEnvironmentVariable("Email__Provider", "Disabled");
        Environment.SetEnvironmentVariable("Google__ClientIds__0", "pruebas.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("Jamendo__ClientId", "");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Los correos se capturan en memoria, y Google y Jamendo se simulan.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();

            services.RemoveAll<IMusicProvider>();
            services.AddSingleton<IMusicProvider, FakeJamendoProvider>();
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}

/// <summary>Todas las pruebas de integración comparten una sola API y una sola base de datos.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}