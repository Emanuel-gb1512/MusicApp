using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using MusicApi.Api.Models;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;

namespace MusicApi.Api.Controllers;

/// <summary>
/// Registro e inicio de sesión con correo y contraseña.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService authService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : ControllerBase
{
    /// <summary>Crea una cuenta nueva y abre una sesión en el dispositivo indicado.</summary>
    /// <response code="201">Cuenta creada. Devuelve los tokens.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="409">Ya existe una cuenta con ese correo.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        var result = await authService.RegisterAsync(request, GetClientInfo(), cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Response)
            : ToErrorResponse(result.Error);
    }

    /// <summary>Inicia sesión con correo y contraseña.</summary>
    /// <response code="200">Sesión iniciada. Devuelve los tokens.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="401">Correo o contraseña incorrectos.</response>
    /// <response code="403">La cuenta está suspendida.</response>
    /// <response code="429">Demasiados intentos fallidos; la cuenta está bloqueada temporalmente.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        var result = await authService.LoginAsync(request, GetClientInfo(), cancellationToken);

        return result.Succeeded
            ? Ok(result.Response)
            : ToErrorResponse(result.Error);
    }

    private ClientInfo GetClientInfo() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());

    private ActionResult ValidationFailed(ValidationResult validation) =>
        ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

    private ActionResult ToErrorResponse(AuthError error) => error switch
    {
        AuthError.EmailAlreadyRegistered => StatusCode(
            StatusCodes.Status409Conflict,
            new ApiError("email_already_registered", "Ya existe una cuenta con ese correo.")),

        AuthError.InvalidCredentials => StatusCode(
            StatusCodes.Status401Unauthorized,
            new ApiError("invalid_credentials", "Correo o contraseña incorrectos.")),

        AuthError.AccountLocked => StatusCode(
            StatusCodes.Status429TooManyRequests,
            new ApiError("account_locked", "Demasiados intentos fallidos. Inténtalo de nuevo en 15 minutos.")),

        AuthError.AccountSuspended => StatusCode(
            StatusCodes.Status403Forbidden,
            new ApiError("account_suspended", "Esta cuenta está suspendida.")),

        _ => StatusCode(
            StatusCodes.Status500InternalServerError,
            new ApiError("unexpected_error", "Ocurrió un error inesperado."))
    };
}