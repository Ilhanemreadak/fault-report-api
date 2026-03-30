namespace LotusCode.Application.DTOs.FaultReports
{
    public sealed class UpdateFaultReportRequest
    {
        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;
    }
}
