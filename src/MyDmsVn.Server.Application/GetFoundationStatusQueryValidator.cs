using FluentValidation;

namespace MyDmsVn.Server.Application
{
    internal sealed class GetFoundationStatusQueryValidator
        : AbstractValidator<GetFoundationStatusQuery>
    {
        public GetFoundationStatusQueryValidator()
        {
            RuleFor(query => query.ClientName)
                .NotEmpty()
                .WithErrorCode("Validation.Required")
                .WithMessage("Client name is required.");
        }
    }
}
