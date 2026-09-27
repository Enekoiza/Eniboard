using Application.Dtos;
using FluentValidation;

namespace Application.Validators;

public sealed class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
{
    public CreateCardRequestValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.ColumnId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.CardType).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.LinkedBranch).MaximumLength(250).When(x => x.LinkedBranch is not null);
    }
}

public sealed class UpdateCardRequestValidator : AbstractValidator<UpdateCardRequest>
{
    public UpdateCardRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.CardType).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class MoveCardRequestValidator : AbstractValidator<MoveCardRequest>
{
    public MoveCardRequestValidator()
    {
        RuleFor(x => x.TargetColumnId).NotEmpty();
        RuleFor(x => x.LinkedBranch).MaximumLength(250).When(x => x.LinkedBranch is not null);
    }
}
