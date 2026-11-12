namespace MusicApi.Domain.Entities;

/// <summary>
/// Token de verificación de correo (tabla EmailVerificationTokens, un solo uso).
/// Se guarda solo el hash SHA-256.
/// </summary>
public class EmailVerificationToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
