using Application.Dtos;
using FluentValidation;

namespace Application.Validators;

public sealed class CreateAppRequestValidator : AbstractValidator<CreateAppRequest>
{
    public CreateAppRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(32);
        RuleFor(x => x.RepoUrl).MaximumLength(500).When(x => x.RepoUrl is not null);
    }
}
