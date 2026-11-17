using MusicApi.Application.Auth;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Verifica un ID token de Google: firma, caducidad, emisor y que fue emitido para nuestra aplicación.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>False si no hay ningún Client ID configurado.</summary>
    bool IsConfigured { get; }

    /// <summary>Devuelve la identidad, o null si el token no es válido.</summary>
    Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}