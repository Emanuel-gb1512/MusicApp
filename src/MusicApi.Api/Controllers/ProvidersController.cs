using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Models;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Providers;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Catálogo de proveedores de música: qué servicios conoce TONYX y qué permite cada uno.
/// La app lo usa para decidir qué botones mostrar (buscar, descargar, abrir en la app oficial...).
/// </summary>
[ApiController]
[Authorize]
[Route("api/providers")]
public class ProvidersController(IProviderCatalogService catalog) : ControllerBase
{
    /// <summary>Lista todos los proveedores y sus capacidades oficiales.</summary>
    /// <response code="200">Lista de proveedores.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ProviderInfo>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await catalog.GetProvidersAsync(cancellationToken));
    }

    /// <summary>Devuelve un proveedor por su código (spotify, youtube, jamendo...).</summary>
    /// <param name="code">Código del proveedor. No distingue mayúsculas.</param>
    /// <response code="200">El proveedor.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    /// <response code="404">No existe un proveedor con ese código.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ProviderInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderInfo>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var provider = await catalog.GetProviderAsync(code, cancellationToken);

        return provider is null
            ? NotFound(new ApiError("provider_not_found", "No existe un proveedor con ese código."))
            : Ok(provider);
    }
}