using LotusCode.Application.Common;
using LotusCode.Application.DTOs.FaultReports;
using LotusCode.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LotusCode.Api.Controllers
{
    /// <summary>
    /// Exposes endpoints for managing fault reports.
    /// </summary>
    [ApiController]
    [Route("api/fault-reports")]
    [Authorize]
    public sealed class FaultReportsController : ControllerBase
    {
        private readonly IFaultReportService faultReportService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultReportsController"/> class.
        /// </summary>
        /// <param name="faultReportService">The fault report service.</param>
        public FaultReportsController(IFaultReportService faultReportService)
        {
            this.faultReportService = faultReportService;
        }

        /// <summary>
        /// Gets a fault report by its identifier.
        /// </summary>
        /// <param name="id">The fault report identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The fault report detail.</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<FaultReportDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<FaultReportDetailDto>>> GetById( Guid id,
            CancellationToken cancellationToken)
        {
            var result = await this.faultReportService.GetByIdAsync(id, cancellationToken);

            return this.Ok(new ApiResponse<FaultReportDetailDto>
            {
                Success = true,
                Data = result,
                Message = "Fault report retrieved successfully.",
                Errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// Gets a paginated list of fault reports with filtering and sorting.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<FaultReportListItemDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<PagedResult<FaultReportListItemDto>>>> GetList(
            [FromQuery] GetFaultReportsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await this.faultReportService.GetListAsync(query, cancellationToken);

            return this.Ok(new ApiResponse<PagedResult<FaultReportListItemDto>>
            {
                Success = true,
                Data = result,
                Message = "Fault reports retrieved successfully.",
                Errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// Creates a new fault report.
        /// </summary>
        /// <param name="request">The create request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identifier of the created fault report.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ApiResponse<Guid>>> Create( [FromBody] CreateFaultReportRequest request,
        CancellationToken cancellationToken)
        {
            var id = await this.faultReportService.CreateAsync(request, cancellationToken);

            return this.StatusCode(
                StatusCodes.Status201Created,
                new ApiResponse<Guid>
                {
                    Success = true,
                    Data = id,
                    Message = "Fault report created successfully.",
                    Errors = Array.Empty<string>()
                });
        }

        /// <summary>
        /// Updates an existing fault report.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ApiResponse<object>>> Update(
            Guid id,
            [FromBody] UpdateFaultReportRequest request,
            CancellationToken cancellationToken)
        {
            await this.faultReportService.UpdateAsync(id, request, cancellationToken);

            return this.Ok(new ApiResponse<object>
            {
                Success = true,
                Data = null,
                Message = "Fault report updated successfully.",
                Errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// Changes the status of a fault report.
        /// Only admin users are allowed to perform this operation.
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ApiResponse<object>>> ChangeStatus(
            Guid id,
            [FromBody] ChangeFaultReportStatusRequest request,
            CancellationToken cancellationToken)
        {
            await this.faultReportService.ChangeStatusAsync(id, request, cancellationToken);

            return this.Ok(new ApiResponse<object>
            {
                Success = true,
                Data = null,
                Message = "Fault report status updated successfully.",
                Errors = Array.Empty<string>()
            });
        }
    }
}
