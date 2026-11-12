namespace MusicApi.Domain.Entities;

/// <summary>
/// Datos visibles del usuario (tabla UserProfiles, relación 1 a 1 con Users).
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? CountryCode { get; set; }
    public string? LanguageCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
}
