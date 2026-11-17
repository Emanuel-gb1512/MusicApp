using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Extensions;
using MusicApi.Api.Models;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Verificación de correo y recuperación de contraseña.
/// </summary>
[ApiController]
[Authorize]
[Route("api/auth")]
public class AccountSecurityController(
    IAccountSecurityService security,
    IValidator<VerifyEmailRequest> verifyValidator,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator) : ControllerBase
{
    private static readonly ApiError InvalidToken =
        new("invalid_token", "El código no es válido o ya caducó.");

    /// <summary>Envía al correo del usuario un código para verificarlo.</summary>
    /// <response code="202">Correo en camino.</response>
    /// <response code="401">Falta el token o no es válido.</response>
    /// <response code="409">El correo ya estaba verificado.</response>
    /// <response code="429">Hay que esperar un minuto antes de pedir otro código.</response>
    [HttpPost("verify-email/send")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendVerificationEmail(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var outcome = await security.SendVerificationEmailAsync(userId, cancellationToken);

        return outcome switch
        {
            SendVerificationOutcome.Queued => Accepted(),

            SendVerificationOutcome.AlreadyVerified => Conflict(
                new ApiError("email_already_verified", "Tu correo ya está verificado.")),

            SendVerificationOutcome.TooSoon => StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ApiError("too_many_requests", "Espera un minuto antes de pedir otro código.")),

            _ => Unauthorized()
        };
    }

    /// <summary>Confirma el correo con el código recibido.</summary>
    /// <response code="204">Correo verificado.</response>
    /// <response code="400">Datos inválidos, o el código no es válido o caducó.</response>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var validation = await verifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        return await security.VerifyEmailAsync(request.Token, cancellationToken)
            ? NoContent()
            : BadRequest(InvalidToken);
    }

    /// <summary>
    /// Pide un código para recuperar la contraseña. Responde siempre igual,
    /// exista o no el correo, para no revelar qué cuentas existen.
    /// </summary>
    /// <response code="202">Si la cuenta existe, el correo va en camino.</response>
    /// <response code="400">Datos inválidos.</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await forgotValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        await security.ForgotPasswordAsync(request.Email, cancellationToken);
        return Accepted();
    }

    /// <summary>Cambia la contraseña con el código de recuperación y cierra todas las sesiones.</summary>
    /// <response code="204">Contraseña cambiada.</response>
    /// <response code="400">Datos inválidos, o el código no es válido o caducó.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Si la contraseña es débil se responde aquí, SIN gastar el código.
        var validation = await resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        return await security.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken)
            ? NoContent()
            : BadRequest(InvalidToken);
    }

    private IActionResult ValidationFailed(ValidationResult validation) =>
        ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
}