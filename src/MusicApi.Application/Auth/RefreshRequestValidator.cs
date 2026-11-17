using FluentValidation;

namespace MusicApi.Application.Auth;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        // Los refresh tokens miden 43 caracteres; el límite evita procesar entradas absurdas.
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
    }
}