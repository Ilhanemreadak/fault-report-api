namespace LotusCode.Application.DTOs.FaultReports
{
    public sealed class FaultReportDetailDto
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public Guid CreatedByUserId { get; init; }

        public string CreatedByFullName { get; init; } = string.Empty;

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
    }
}
