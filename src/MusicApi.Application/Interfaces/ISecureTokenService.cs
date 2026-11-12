namespace MusicApi.Application.Interfaces;

/// <summary>
/// Genera tokens aleatorios de un solo propósito (renovación de sesión, verificación
/// de correo, recuperación de contraseña) y calcula el hash que se guarda en la base.
/// </summary>
public interface ISecureTokenService
{
    /// <summary>Crea un token nuevo. El token se entrega al usuario; solo su hash se guarda.</summary>
    GeneratedToken Generate();

    /// <summary>SHA-256 del token, en hexadecimal minúscula (64 caracteres).</summary>
    string Hash(string token);
}

/// <summary>Token en claro (para el usuario) y su hash (para la base de datos).</summary>
public sealed record GeneratedToken(string Token, string TokenHash);
