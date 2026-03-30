namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents request model for creating a new fault report.
    /// Includes basic information such as title, description, location and priority.
    /// Status is managed by the system and not provided by client.
    /// </summary>
    public sealed class CreateFaultReportRequest
    {
        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;
    }
}
