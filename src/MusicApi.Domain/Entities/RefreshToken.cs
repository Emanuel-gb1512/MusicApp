namespace MusicApi.Domain.Entities;

/// <summary>
/// Token de renovación (tabla RefreshTokens). Se guarda SOLO el hash SHA-256
/// en hexadecimal (64 caracteres), nunca el token en claro.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Token que reemplazó a este al rotar. Sirve para detectar reutilización.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public UserSession Session { get; set; } = null!;
    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
