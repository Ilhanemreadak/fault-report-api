using FluentValidation;
using LotusCode.Application.DTOs.Auth;

namespace LotusCode.Application.Validators.Auth
{
    /// <summary>
    /// Validates login request payload.
    /// Ensures email and password fields are provided before authentication is attempted.
    /// </summary>
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Email format is invalid.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required.");
        }
    }
}
