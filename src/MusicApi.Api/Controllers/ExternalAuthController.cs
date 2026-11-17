using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Models;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Domain.Enums;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Inicio de sesión con proveedores externos.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class ExternalAuthController(
    IGoogleTokenValidator googleValidator,
    IExternalLoginService externalLogin,
    IValidator<GoogleLoginRequest> googleRequestValidator) : ControllerBase
{
    /// <summary>
    /// Inicia sesión con Google. La app envía el ID token que le entregó Google; la API lo verifica,
    /// crea la cuenta si es la primera vez y devuelve los tokens de TONYX.
    /// </summary>
    /// <response code="200">Sesión iniciada con una cuenta existente.</response>
    /// <response code="201">Cuenta nueva creada con Google.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="401">El token de Google no es válido.</response>
    /// <response code="403">El correo no está verificado por Google, o la cuenta está suspendida o no disponible.</response>
    /// <response code="503">El inicio de sesión con Google no está configurado en el servidor.</response>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthResponse>> Google(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await googleRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!googleValidator.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiError("google_not_configured", "El inicio de sesión con Google no está configurado."));
        }

        var identity = await googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Unauthorized(new ApiError(
                "invalid_external_token",
                "No se pudo verificar tu cuenta de Google."));
        }

        var result = await externalLogin.LoginAsync(
            ExternalProvider.Google,
            identity,
            request.Device,
            new ClientInfo(
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        if (result.Succeeded)
        {
            return result.IsNewUser
                ? StatusCode(StatusCodes.Status201Created, result.Response)
                : Ok(result.Response);
        }

        return result.Error switch
        {
            ExternalLoginError.EmailNotVerified => StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiError("email_not_verified_by_provider", "Google no ha verificado el correo de esta cuenta.")),

            ExternalLoginError.AccountSuspended => StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiError("account_suspended", "Esta cuenta está suspendida.")),

            ExternalLoginError.AccountUnavailable => StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiError("account_unavailable", "Esta cuenta no está disponible.")),

            _ => Unauthorized(new ApiError(
                "invalid_external_token",
                "No se pudo verificar tu cuenta de Google."))
        };
    }
}