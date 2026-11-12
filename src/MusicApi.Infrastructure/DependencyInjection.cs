using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicApi.Application.Interfaces;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure;

/// <summary>
/// Registra todos los servicios de la capa Infrastructure.
/// Se llama una sola vez desde Program.cs de la API.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

        return services;
    }
}
