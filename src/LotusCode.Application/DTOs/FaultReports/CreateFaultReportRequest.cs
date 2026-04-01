namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents request model for creating a new fault report.
    /// Includes basic information such as title, description, location and priority.
    /// Status is managed by the system and not provided by client.
    /// </summary>
    public sealed class CreateFaultReportRequest
    {
        /// <summary>
        /// Fault report title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Detailed fault report description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Fault location text.
        /// </summary>
        public string Location { get; init; } = string.Empty;

        /// <summary>
        /// Priority value. Accepted values: Low, Medium, High.
        /// </summary>
        public string Priority { get; init; } = string.Empty;
    }
}
