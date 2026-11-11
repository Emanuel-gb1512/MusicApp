using Microsoft.AspNetCore.Mvc;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Comprobación de estado: sirve para saber si la API está viva.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>Devuelve el estado actual del servicio.</summary>
    /// <response code="200">La API está funcionando.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse("TONYX", "MusicApi", "ok", DateTime.UtcNow));
    }
}

/// <summary>Respuesta del endpoint de salud.</summary>
public sealed record HealthResponse(string App, string Service, string Status, DateTime Utc);