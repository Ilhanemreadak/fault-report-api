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
        public string? Status { get; init; }

        public string? Priority { get; init; }

        public string? Location { get; init; }

        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = 10;

        public string SortBy { get; init; } = "createdAt";

        public string SortDirection { get; init; } = "desc";
    }
}
