using LotusCode.Domain.Enums;

namespace LotusCode.Domain.Policies
{
    public interface IFaultReportStatusTransitionPolicy
    {
        bool CanTransition(
            UserRole role,
            FaultReportStatus currentStatus,
            FaultReportStatus targetStatus);

        string? GetValidationMessage(
            UserRole role,
            FaultReportStatus currentStatus,
            FaultReportStatus targetStatus);
    }
}
