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
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithErrorCode("Validation.Required")
                .Must(PasswordInputLimits.FitsBcrypt)
                .WithMessage(
                    $"Password cannot exceed {PasswordInputLimits.BcryptMaximumUtf8Bytes} UTF-8 bytes.")
                .WithErrorCode("Validation.MaximumLength");
        }
    }
}
