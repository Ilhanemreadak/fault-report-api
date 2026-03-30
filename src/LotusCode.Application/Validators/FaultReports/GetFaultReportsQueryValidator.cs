using FluentValidation;
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
        private static readonly string[] AllowedSortBy = ["createdAt", "priority"];
        private static readonly string[] AllowedSortDirection = ["asc", "desc"];
        private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];
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
                .Must(sort => AllowedSortBy.Contains(sort))
                .WithMessage("SortBy must be 'createdAt' or 'priority'.");

            RuleFor(x => x.SortDirection)
                .Must(direction => AllowedSortDirection.Contains(direction))
                .WithMessage("SortDirection must be 'asc' or 'desc'.");

            RuleFor(x => x.Priority)
                .Must(p => p == null || AllowedPriorities.Contains(p))
                .WithMessage("Invalid priority value.");

            RuleFor(x => x.Status)
                .Must(s => s == null || AllowedStatuses.Contains(s))
                .WithMessage("Invalid status value.");
        }
    }
}
