namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents lightweight fault report data returned in list endpoints.
    /// Contains essential fields for listing without full detail payload.
    /// </summary>
    public sealed class FaultReportListItemDto
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string CreatedByFullName { get; init; } = string.Empty;

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
    }
}
