using FluentValidation;

namespace MusicApi.Application.Auth;

public sealed class DeviceInfoValidator : AbstractValidator<DeviceInfo>
{
    public DeviceInfoValidator()
    {
        RuleFor(x => x.DeviceIdentifier).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.AppVersion).MaximumLength(30);
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        // La forma del correo cumple la restricción CK_Users_Email de la base de datos
        // (algo@algo.algo), así que un correo aceptado aquí nunca la rompe.
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress()
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")
            .WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .MaximumLength(128)
            .Must(p => p.Any(char.IsLetter)).WithMessage("La contraseña debe incluir al menos una letra.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("La contraseña debe incluir al menos un número.")
            .Must((request, password) =>
                !string.Equals(password, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("La contraseña no puede ser igual al correo.");

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Device)
            .NotNull()
            .SetValidator(new DeviceInfoValidator());
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);

        // En el login no se exige el formato de la contraseña: eso solo importa al crearla.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Device)
            .NotNull()
            .SetValidator(new DeviceInfoValidator());
    }
}