using FluentValidation;

namespace MusicApi.Application.Auth;

/// <summary>
/// Reglas de contraseña nueva: entre 10 y 128 caracteres, con al menos una letra y un número.
/// Úsala con Cascade(CascadeMode.Stop) para que se detenga en el primer error.
/// </summary>
public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .MaximumLength(128)
            .Must(p => p is not null && p.Any(char.IsLetter))
                .WithMessage("La contraseña debe incluir al menos una letra.")
            .Must(p => p is not null && p.Any(char.IsDigit))
                .WithMessage("La contraseña debe incluir al menos un número.");
}