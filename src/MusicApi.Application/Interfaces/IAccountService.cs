using MusicApi.Application.Auth;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Datos de la cuenta del usuario y gestión de sus sesiones.
/// </summary>
public interface IAccountService
{
    Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra una sesión del usuario. Devuelve false si no existe o ya estaba cerrada.</summary>
    Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra todas las sesiones activas menos la indicada. Devuelve cuántas cerró.</summary>
    Task<int> RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Comprueba que la sesión exista, no esté cerrada ni caducada y que el usuario siga activo.</summary>
    Task<bool> IsSessionActiveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}