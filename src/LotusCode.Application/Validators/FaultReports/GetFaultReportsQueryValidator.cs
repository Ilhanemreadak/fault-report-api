using FluentValidation;
using LotusCode.Application.Common;
using LotusCode.Application.DTOs.FaultReports;

namespace LotusCode.Application.Validators.FaultReports
{
    /// <summary>
    /// Validates query parameters for fault report listing.
    /// Ensures pagination, sorting and filter values are within acceptable limits.
    /// </summary>
    public sealed class GetFaultReportsQueryValidator
        : AbstractValidator<GetFaultReportsQuery>
    {
        public GetFaultReportsQueryValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Page must be greater than or equal to 1.");

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PageSize must be greater than or equal to 1.")
                .LessThanOrEqualTo(100)
                .WithMessage("PageSize cannot exceed 100.");

            RuleFor(x => x.SortBy)
                .Must(FaultReportQueryParsing.IsValidSortBy)
                .WithMessage("SortBy must be 'createdAt' or 'priority'.");

            RuleFor(x => x.SortDirection)
                .Must(FaultReportQueryParsing.IsValidSortDirection)
                .WithMessage("SortDirection must be 'asc' or 'desc'.");

            RuleFor(x => x.Priority)
                .Must(FaultReportQueryParsing.IsValidPriority)
                .WithMessage("Invalid priority value.");

            RuleFor(x => x.Status)
                .Must(FaultReportQueryParsing.IsValidStatus)
                .WithMessage("Invalid status value.");
        }
    }
}
