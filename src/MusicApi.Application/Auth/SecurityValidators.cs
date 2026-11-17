using FluentValidation;

namespace MusicApi.Application.Auth;

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        // No se valida el formato: la respuesta debe ser la misma exista o no la cuenta.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .MustBeStrongPassword();
    }
}