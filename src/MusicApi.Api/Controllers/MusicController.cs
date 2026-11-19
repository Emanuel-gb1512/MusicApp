using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Models;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Music;
using MusicApi.Application.Providers;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Búsqueda de música y datos de reproducción, a través de los proveedores conectados.
/// </summary>
[ApiController]
[Authorize]
[Route("api/music")]
public class MusicController(
    IMusicSearchService music,
    IValidator<MusicSearchRequest> searchValidator) : ControllerBase
{
    // Los identificadores de los proveedores son números o textos cortos sin símbolos raros.
    private static readonly Regex ExternalIdShape = new("^[A-Za-z0-9_.:-]{1,100}$", RegexOptions.Compiled);

    /// <summary>
    /// Busca pistas, álbumes y artistas. Sin "provider", busca en todos los proveedores listos;
    /// si alguno falla, el resto de resultados llega igual y el fallo aparece en "errors".
    /// </summary>
    /// <param name="q">Texto a buscar (hasta 100 caracteres).</param>
    /// <param name="provider">Código del proveedor (jamendo...). Opcional.</param>
    /// <param name="limit">Máximo de pistas, de 1 a 50. Por defecto 20.</param>
    /// <response code="200">Resultados por proveedor.</response>
    /// <response code="400">Datos inválidos, o el proveedor no permite buscar.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    /// <response code="404">No existe un proveedor con ese código.</response>
    /// <response code="502">El proveedor indicado no respondió.</response>
    /// <response code="503">El proveedor indicado no está configurado.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(MusicSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MusicSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] string? provider,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 20)
    {
        var request = new MusicSearchRequest(q, provider, limit);

        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await music.SearchAsync(request.Q!, request.Provider, request.Limit, cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result.Outcome);
    }

    /// <summary>
    /// Devuelve una pista con sus datos de reproducción: dirección de streaming, licencia y,
    /// solo si el artista lo permite, dirección de descarga.
    /// </summary>
    /// <param name="provider">Código del proveedor (jamendo...).</param>
    /// <param name="externalId">Identificador de la pista en el proveedor.</param>
    /// <response code="200">La pista.</response>
    /// <response code="400">El identificador no es válido, o el proveedor no es de música.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    /// <response code="404">No existe el proveedor o la pista.</response>
    /// <response code="502">El proveedor no respondió.</response>
    /// <response code="503">El proveedor no está configurado.</response>
    [HttpGet("tracks/{provider}/{externalId}")]
    [ProducesResponseType(typeof(ProviderTrack), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProviderTrack>> GetTrack(
        string provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        if (!ExternalIdShape.IsMatch(externalId))
        {
            return BadRequest(new ApiError("invalid_external_id", "El identificador de la pista no es válido."));
        }

        var result = await music.GetTrackAsync(provider, externalId, cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result.Outcome);
    }

    private ActionResult ToError(MusicOutcome outcome) => outcome switch
    {
        MusicOutcome.ProviderNotFound => NotFound(
            new ApiError("provider_not_found", "No existe un proveedor con ese código.")),

        MusicOutcome.ProviderNotSupported => BadRequest(
            new ApiError("provider_not_supported", "Ese proveedor no permite esta operación.")),

        MusicOutcome.ProviderNotConfigured => StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new ApiError("provider_not_configured", "Ese proveedor no está configurado en el servidor.")),

        MusicOutcome.TrackNotFound => NotFound(
            new ApiError("track_not_found", "No se encontró la pista.")),

        MusicOutcome.ProviderUnavailable => StatusCode(
            StatusCodes.Status502BadGateway,
            new ApiError("provider_unavailable", "El proveedor no respondió. Inténtalo de nuevo en un momento.")),

        _ => StatusCode(
            StatusCodes.Status500InternalServerError,
            new ApiError("unexpected_error", "Ocurrió un error inesperado."))
    };
}