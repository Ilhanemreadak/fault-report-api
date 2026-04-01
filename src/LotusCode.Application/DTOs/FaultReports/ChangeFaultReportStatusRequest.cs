namespace LotusCode.Application.DTOs.FaultReports
{
    /// <summary>
    /// Represents request model for changing fault report status.
    /// Only contains target status value and is used by admin users.
    /// </summary>
    public sealed class ChangeFaultReportStatusRequest
    {
        /// <summary>
        /// Target status value. Accepted values: New, Reviewing, Assigned, InProgress, Completed, Cancelled, FalseAlarm.
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }
}
