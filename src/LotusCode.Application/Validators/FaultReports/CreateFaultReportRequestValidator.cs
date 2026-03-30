using FluentValidation;
using LotusCode.Application.DTOs.FaultReports;

namespace LotusCode.Application.Validators.FaultReports
{
    /// <summary>
    /// Validates create fault report request payload.
    /// Ensures required fields are provided and field lengths are within allowed limits.
    /// </summary>
    public sealed class CreateFaultReportRequestValidator : AbstractValidator<CreateFaultReportRequest>
    {
        private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

        public CreateFaultReportRequestValidator()
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
                .Must(priority => AllowedPriorities.Contains(priority))
                .WithMessage("Priority must be one of the following values: Low, Medium, High.");
        }
    }
}
