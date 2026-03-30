using FluentValidation;
using LotusCode.Application.DTOs.FaultReports;

namespace LotusCode.Application.Validators.FaultReports
{
    /// <summary>
    /// Validates status change request payload.
    /// Ensures target status is provided and is one of the allowed values.
    /// </summary>
    public sealed class ChangeFaultReportStatusRequestValidator
        : AbstractValidator<ChangeFaultReportStatusRequest>
    {
        private static readonly string[] AllowedStatuses =
        [
            "New",
            "Reviewing",
            "Assigned",
            "InProgress",
            "Completed",
            "Cancelled",
            "FalseAlarm"
        ];

        public ChangeFaultReportStatusRequestValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .WithMessage("Status is required.")
                .Must(status => AllowedStatuses.Contains(status))
                .WithMessage("Invalid status value.");
        }
    }
}
