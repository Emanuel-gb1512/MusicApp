using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Extensions;
using MusicApi.Api.Models;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Renovación de tokens, cierre de sesión y gestión de las sesiones del usuario.
/// </summary>
[ApiController]
[Authorize]
[Route("api/auth")]
public class AccountController(
    ITokenRefreshService refreshService,
    IAccountService accountService,
    IValidator<RefreshRequest> refreshValidator) : ControllerBase
{
    /// <summary>Cambia un refresh token por un par nuevo de tokens. El token anterior deja de servir.</summary>
    /// <response code="200">Tokens nuevos.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="401">El refresh token no es válido, caducó o ya se usó.</response>
    /// <response code="403">La cuenta está suspendida.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await refreshService.RefreshAsync(request.RefreshToken, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Response);
        }

        return result.Error == RefreshError.AccountSuspended
            ? StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiError("account_suspended", "Esta cuenta está suspendida."))
            : Unauthorized(new ApiError(
                "invalid_refresh_token",
                "La sesión no es válida. Inicia sesión de nuevo."));
    }

    /// <summary>Devuelve los datos del usuario autenticado.</summary>
    /// <response code="200">Datos del usuario.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummary>> Me(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var user = await accountService.GetUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    /// <summary>Cierra la sesión actual.</summary>
    /// <response code="204">Sesión cerrada.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return Unauthorized();
        }

        await accountService.RevokeSessionAsync(userId, sessionId, "logout", cancellationToken);
        return NoContent();
    }

    /// <summary>Cierra todas las sesiones del usuario, excepto la actual.</summary>
    /// <response code="200">Cuántas sesiones se cerraron.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    [HttpPost("logout-others")]
    [ProducesResponseType(typeof(RevokeOthersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RevokeOthersResponse>> LogoutOthers(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return Unauthorized();
        }

        var count = await accountService.RevokeOtherSessionsAsync(userId, sessionId, cancellationToken);
        return Ok(new RevokeOthersResponse(count));
    }

    /// <summary>Lista las sesiones activas del usuario (una por dispositivo conectado).</summary>
    /// <response code="200">Lista de sesiones.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SessionInfo>>> GetSessions(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return Unauthorized();
        }

        return Ok(await accountService.GetSessionsAsync(userId, sessionId, cancellationToken));
    }

    /// <summary>Cierra una sesión concreta del usuario.</summary>
    /// <param name="sessionId">Identificador de la sesión (el campo sessionId de la lista).</param>
    /// <response code="204">Sesión cerrada.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    /// <response code="404">La sesión no existe, no es tuya o ya estaba cerrada.</response>
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var revoked = await accountService.RevokeSessionAsync(userId, sessionId, "revoked_by_user", cancellationToken);

        return revoked
            ? NoContent()
            : NotFound(new ApiError("session_not_found", "La sesión no existe o ya estaba cerrada."));
    }
}