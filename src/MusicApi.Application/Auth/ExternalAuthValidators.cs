using FluentValidation;

namespace MusicApi.Application.Auth;

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        // Un ID token de Google mide alrededor de 1 a 2 KB; el límite evita procesar entradas absurdas.
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);

        RuleFor(x => x.Device)
            .NotNull()
            .SetValidator(new DeviceInfoValidator());
    }
}