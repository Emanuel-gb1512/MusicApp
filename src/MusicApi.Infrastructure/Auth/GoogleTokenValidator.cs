using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Verifica ID tokens de Google con la biblioteca oficial: firma (con las claves públicas de Google),
/// caducidad, emisor y destinatario (el token debe haberse emitido para uno de nuestros Client ID).
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string[] _audiences;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(GoogleOptions options, ILogger<GoogleTokenValidator> logger)
    {
        _logger = logger;
        _audiences = options.ClientIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToArray();
    }

    public bool IsConfigured => _audiences.Length > 0;

    public async Task<ExternalIdentity?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = _audiences });

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new ExternalIdentity(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            // Se registra el motivo, nunca el token.
            _logger.LogWarning("Token de Google rechazado: {Reason}", ex.Message);
            return null;
        }
    }
}