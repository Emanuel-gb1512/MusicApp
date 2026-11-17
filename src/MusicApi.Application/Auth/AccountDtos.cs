using MusicApi.Domain.Enums;

namespace MusicApi.Application.Auth;

/// <summary>Solicitud para cambiar un refresh token por un par nuevo de tokens.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Una sesión activa del usuario, tal como se muestra en "mis dispositivos".</summary>
public sealed record SessionInfo(
    Guid SessionId,
    string DeviceName,
    DevicePlatform Platform,
    DateTime CreatedAtUtc,
    DateTime LastActivityAtUtc,
    string? IpAddress,
    bool IsCurrent);

/// <summary>Respuesta de "cerrar las demás sesiones".</summary>
public sealed record RevokeOthersResponse(int RevokedSessions);

/// <summary>Motivos por los que una renovación puede fallar.</summary>
public enum RefreshError
{
    None = 0,
    InvalidRefreshToken,
    AccountSuspended
}

/// <summary>Resultado de renovar tokens: éxito con datos, o un error.</summary>
public sealed record RefreshResult(AuthResponse? Response, RefreshError Error)
{
    public bool Succeeded => Error == RefreshError.None;

    public static RefreshResult Success(AuthResponse response) => new(response, RefreshError.None);

    public static RefreshResult Failure(RefreshError error) => new(null, error);
}