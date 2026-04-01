using FluentValidation;
using LotusCode.Application.DTOs.FaultReports;

namespace LotusCode.Application.Validators.FaultReports
{
    /// <summary>
    /// Validates update fault report request payload.
    /// Ensures updatable fields are provided and field lengths are within allowed limits.
    /// Status changes are intentionally excluded from this validator.
    /// </summary>
    public sealed class UpdateFaultReportRequestValidator : AbstractValidator<UpdateFaultReportRequest>
    {
        private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

        public UpdateFaultReportRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.")
                .MaximumLength(200)
                .WithMessage("Title cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Description is required.")
                .MaximumLength(2000)
                .WithMessage("Description cannot exceed 2000 characters.");

            RuleFor(x => x.Location)
                .NotEmpty()
                .WithMessage("Location is required.")
                .MaximumLength(300)
                .WithMessage("Location cannot exceed 300 characters.");

            RuleFor(x => x.Priority)
                .NotEmpty()
                .WithMessage("Priority is required.")
                .Must(priority => AllowedPriorities.Contains(priority, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Priority must be one of the following values: Low, Medium, High.");
        }
    }
}
