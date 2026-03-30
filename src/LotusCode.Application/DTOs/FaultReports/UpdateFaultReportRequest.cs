namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents request model for updating an existing fault report.
    /// Allows updating title, description, location and priority fields.
    /// Status changes are handled separately via dedicated endpoint.
    /// </summary>
    public sealed class UpdateFaultReportRequest
    {
        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;
    }
}
