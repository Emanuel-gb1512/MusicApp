using MusicApi.Domain.Enums;

namespace MusicApi.Domain.Entities;

/// <summary>
/// Cuenta de un usuario vinculada a un proveedor externo (tabla ExternalAccounts).
/// Para Google y Apple (solo inicio de sesión) los tokens quedan en NULL.
/// Los tokens, cuando existan, se guardan CIFRADOS. Nunca se guardan contraseñas de servicios externos.
/// </summary>
public class ExternalAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Id del proveedor (ConnectedProviders.Id). Usa (short)ExternalProvider.X.</summary>
    public short ProviderId { get; set; }

    /// <summary>Identificador estable de la cuenta en el proveedor (el "sub" en Google).</summary>
    public string ExternalUserId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public byte[]? AccessTokenEncrypted { get; set; }
    public byte[]? RefreshTokenEncrypted { get; set; }
    public DateTime? TokenExpiresAtUtc { get; set; }
    public string? Scopes { get; set; }
    public ExternalAccountStatus Status { get; set; } = ExternalAccountStatus.Linked;
    public DateTime LinkedAtUtc { get; set; }
    public DateTime? UnlinkedAtUtc { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
}