using Microsoft.AspNetCore.Mvc;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Comprobaciones de estado: sirven para saber si la API está viva
/// y si su configuración se cargó correctamente.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Devuelve el estado actual del servicio.</summary>
    /// <response code="200">La API está funcionando.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse("TONYX", "MusicApi", "ok", DateTime.UtcNow));
    }

    /// <summary>
    /// Indica si la configuración se cargó (solo en Development).
    /// Nunca devuelve valores secretos, solo si existen.
    /// </summary>
    /// <response code="200">Resumen de la configuración cargada.</response>
    /// <response code="404">No disponible fuera de Development.</response>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ConfigCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ConfigCheckResponse> GetConfig()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var appName = configuration["App:Name"] ?? "(sin nombre)";
        var connectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString("DefaultConnection"));

        return Ok(new ConfigCheckResponse(
            environment.EnvironmentName,
            appName,
            connectionConfigured));
    }
}

/// <summary>Respuesta del endpoint de salud.</summary>
public sealed record HealthResponse(string App, string Service, string Status, DateTime Utc);

/// <summary>Respuesta del endpoint de comprobación de configuración.</summary>
public sealed record ConfigCheckResponse(
    string Environment,
    string AppName,
    bool ConnectionStringConfigured);