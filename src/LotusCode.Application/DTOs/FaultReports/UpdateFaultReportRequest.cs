namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents request model for updating an existing fault report.
    /// Allows updating title, description, location and priority fields.
    /// Status changes are handled separately via dedicated endpoint.
    /// </summary>
    public sealed class UpdateFaultReportRequest
    {
        /// <summary>
        /// Updated fault report title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Updated fault report description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Updated fault location text.
        /// </summary>
        public string Location { get; init; } = string.Empty;

        /// <summary>
        /// Updated priority value. Accepted values: Low, Medium, High.
        /// </summary>
        public string Priority { get; init; } = string.Empty;
    }
}
