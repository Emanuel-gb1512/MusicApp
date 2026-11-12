using MusicApi.Domain.Enums;

namespace MusicApi.Domain.Entities;

/// <summary>
/// Cuenta de acceso (tabla Users). No guarda datos de perfil: eso va en UserProfile.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash de la contraseña. Es NULL en cuentas que entran solo con Google o Apple.</summary>
    public string? PasswordHash { get; set; }

    public bool EmailConfirmed { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public UserProfile? Profile { get; set; }
    public ICollection<UserDevice> Devices { get; set; } = new List<UserDevice>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
