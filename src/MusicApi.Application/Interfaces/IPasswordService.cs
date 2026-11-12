namespace MusicApi.Application.Interfaces;

/// <summary>Resultado de comprobar una contraseña contra su hash.</summary>
public enum PasswordVerificationOutcome
{
    Failed = 0,
    Success = 1,

    /// <summary>Es correcta, pero conviene guardar un hash nuevo (el algoritmo mejoró).</summary>
    SuccessRehashNeeded = 2
}

/// <summary>
/// Hash y verificación de contraseñas. Nunca se guarda ni se registra la contraseña original.
/// </summary>
public interface IPasswordService
{
    string Hash(string password);

    PasswordVerificationOutcome Verify(string passwordHash, string password);
}
