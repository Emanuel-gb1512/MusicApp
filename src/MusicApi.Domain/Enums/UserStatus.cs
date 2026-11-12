namespace MusicApi.Domain.Enums;

/// <summary>Estado de una cuenta de usuario (columna Users.Status).</summary>
public enum UserStatus : byte
{
    Active = 1,
    Suspended = 2,
    Deleted = 3
}
