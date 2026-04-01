using FluentValidation;
using LotusCode.Application.Common;
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
        public ChangeFaultReportStatusRequestValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .WithMessage("Status is required.")
                .Must(FaultReportQueryParsing.IsValidStatus)
                .WithMessage("Invalid status value.");
        }
    }
}
