using LotusCode.Application.Common;
using LotusCode.Application.DTOs.FaultReports;

namespace LotusCode.Application.Interfaces
{
    /// <summary>
    /// Defines operations for managing fault reports.
    /// Handles CRUD operations, filtering, pagination and status transitions,
    /// including business rule enforcement.
    /// </summary>
    public interface IFaultReportService
    {
        Task<Guid> CreateAsync(
            CreateFaultReportRequest request,
            CancellationToken cancellationToken);

        Task<FaultReportDetailDto> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<PagedResult<FaultReportListItemDto>> GetListAsync(
            GetFaultReportsQuery query,
            CancellationToken cancellationToken);

        Task UpdateAsync(
            Guid id,
            UpdateFaultReportRequest request,
            CancellationToken cancellationToken);

        Task ChangeStatusAsync(
            Guid id,
            ChangeFaultReportStatusRequest request,
            CancellationToken cancellationToken);

        Task DeleteAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}
