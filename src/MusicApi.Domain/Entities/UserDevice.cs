using MusicApi.Domain.Enums;

namespace MusicApi.Domain.Entities;

/// <summary>
/// Una instalación de la app de un usuario (tabla UserDevices).
/// </summary>
public class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Identificador generado por la app al instalarse.</summary>
    public string DeviceIdentifier { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
