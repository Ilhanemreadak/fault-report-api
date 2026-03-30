using LotusCode.Domain.Enums;

namespace LotusCode.Domain.Policies
{
    public sealed class FaultReportStatusTransitionPolicy : IFaultReportStatusTransitionPolicy
    {
        private static readonly Dictionary<FaultReportStatus, HashSet<FaultReportStatus>> AllowedTransitions = new()
        {
            [FaultReportStatus.New] = new()
        {
            FaultReportStatus.Reviewing,
            FaultReportStatus.Cancelled
        },
            [FaultReportStatus.Reviewing] = new()
        {
            FaultReportStatus.Assigned,
            FaultReportStatus.FalseAlarm,
            FaultReportStatus.Cancelled
        },
            [FaultReportStatus.Assigned] = new()
        {
            FaultReportStatus.InProgress,
            FaultReportStatus.Cancelled
        },
            [FaultReportStatus.InProgress] = new()
        {
            FaultReportStatus.Completed,
            FaultReportStatus.Cancelled
        },
            [FaultReportStatus.Completed] = new(),
            [FaultReportStatus.Cancelled] = new(),
            [FaultReportStatus.FalseAlarm] = new()
        };

        public bool CanTransition(
            UserRole role,
            FaultReportStatus currentStatus,
            FaultReportStatus targetStatus)
        {
            if (role != UserRole.Admin)
            {
                return false;
            }

            return AllowedTransitions.TryGetValue(currentStatus, out var allowedTargets)
                && allowedTargets.Contains(targetStatus);
        }

        public string? GetValidationMessage(
            UserRole role,
            FaultReportStatus currentStatus,
            FaultReportStatus targetStatus)
        {
            if (role != UserRole.Admin)
            {
                return "Only admins can change fault report status.";
            }

            if (!AllowedTransitions.TryGetValue(currentStatus, out var allowedTargets)
                || !allowedTargets.Contains(targetStatus))
            {
                return $"Status transition from '{currentStatus}' to '{targetStatus}' is not allowed.";
            }

            return null;
        }
    }
}
