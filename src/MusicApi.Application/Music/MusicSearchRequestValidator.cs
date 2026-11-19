using FluentValidation;

namespace MusicApi.Application.Music;

public sealed class MusicSearchRequestValidator : AbstractValidator<MusicSearchRequest>
{
    public MusicSearchRequestValidator()
    {
        RuleFor(x => x.Q)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Escribe qué quieres buscar.")
            .Must(q => q!.Trim().Length <= 100).WithMessage("La búsqueda admite hasta 100 caracteres.");

        RuleFor(x => x.Provider).MaximumLength(30);

        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}