using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>
/// Sustituye a la verificación real de Google, para probar sin internet ni cuentas reales.
/// Formato del token falso: "fake|sub|correo|verificado|nombre". Cualquier otra cosa es inválida.
/// </summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public bool IsConfigured => true;

    public static string Token(string subject, string email, bool verified = true, string name = "Usuario Google") =>
        $"fake|{subject}|{email}|{verified.ToString().ToLowerInvariant()}|{name}";

    public Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var parts = idToken.Split('|');

        if (parts.Length != 5 || parts[0] != "fake" || !bool.TryParse(parts[3], out var verified))
        {
            return Task.FromResult<ExternalIdentity?>(null);
        }

        return Task.FromResult<ExternalIdentity?>(
            new ExternalIdentity(parts[1], parts[2], verified, parts[4], PictureUrl: null));
    }
}