namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents query parameters used for filtering, sorting and paginating
    /// fault report list results.
    /// Supports filtering by status, priority and location,
    /// along with paging and sorting options.
    /// </summary>
    public sealed class GetFaultReportsQuery
    {
        /// <summary>
        /// Optional status filter. Accepted values: New, Reviewing, Assigned, InProgress, Completed, Cancelled, FalseAlarm.
        /// Case-insensitive.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Optional priority filter. Accepted values: Low, Medium, High.
        /// Case-insensitive.
        /// </summary>
        public string? Priority { get; init; }

        /// <summary>
        /// Optional location filter. Performs a case-insensitive contains match.
        /// </summary>
        public string? Location { get; init; }

        /// <summary>
        /// Page number. Must be greater than or equal to 1.
        /// </summary>
        public int Page { get; init; } = 1;

        /// <summary>
        /// Number of items per page. Allowed range: 1 to 100.
        /// </summary>
        public int PageSize { get; init; } = 10;

        /// <summary>
        /// Sort field. Accepted values: createdAt, priority.
        /// Case-insensitive.
        /// </summary>
        public string SortBy { get; init; } = "createdAt";

        /// <summary>
        /// Sort direction. Accepted values: asc, desc.
        /// Case-insensitive.
        /// </summary>
        public string SortDirection { get; init; } = "desc";
    }
}
