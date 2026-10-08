using FluentValidation;

namespace MyDmsVn.Server.Application.Identity
{
    internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(command => command.Username)
                .NotEmpty()
                .WithErrorCode("Validation.Required");
            RuleFor(command => command.Password)
                .NotEmpty()
                .WithErrorCode("Validation.Required");
        }
    }
}
