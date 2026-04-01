using LotusCode.Application.Common;
using LotusCode.Application.DTOs.FaultReports;
using LotusCode.Application.Exceptions;
using LotusCode.Application.Interfaces;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;
using LotusCode.Domain.Policies;
using LotusCode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LotusCode.Infrastructure.Services
{
    /// <summary>
    /// Handles business logic for managing fault reports such as creation,
    /// retrieval, listing, updates and status transitions.
    /// </summary>
    public sealed class FaultReportService : IFaultReportService
    {
        private readonly AppDbContext dbContext;
        private readonly ICurrentUserService currentUserService;
        private readonly IFaultReportStatusTransitionPolicy statusTransitionPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultReportService"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="currentUserService">The current user service.</param>
        /// <param name="statusTransitionPolicy">The centralized status transition policy.</param>
        public FaultReportService(
            AppDbContext dbContext,
            ICurrentUserService currentUserService,
            IFaultReportStatusTransitionPolicy statusTransitionPolicy)
        {
            this.dbContext = dbContext;
            this.currentUserService = currentUserService;
            this.statusTransitionPolicy = statusTransitionPolicy;
        }

        /// <summary>
        /// Creates a new fault report after validating business rules.
        /// </summary>
        public async Task<Guid> CreateAsync(
            CreateFaultReportRequest request,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var duplicateThreshold = now.AddHours(-1);

            var normalizedLocation = FaultReportLocationNormalizer.Normalize(request.Location);

            var recentLocations = await this.dbContext.FaultReports
                .AsNoTracking()
                .Where(x => x.CreatedAtUtc >= duplicateThreshold)
                .Select(x => x.Location)
                .ToListAsync(cancellationToken);

            var exists = recentLocations.Any(x =>
                FaultReportLocationNormalizer.Normalize(x) == normalizedLocation);

            if (exists)
            {
                throw new BusinessRuleException(
                    "A fault report for the same location has already been created within the last hour.");
            }

            if (!FaultReportQueryParsing.TryParsePriority(request.Priority, out var priority))
            {
                throw new BusinessRuleException("Invalid priority value.");
            }

            var entity = new FaultReport
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Location = request.Location.Trim(),
                Priority = priority,
                Status = FaultReportStatus.New,
                CreatedByUserId = this.currentUserService.UserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await this.dbContext.FaultReports.AddAsync(entity, cancellationToken);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }

        /// <summary>
        /// Gets a single fault report by identifier while enforcing ownership rules.
        /// </summary>
        /// <param name="id">The fault report identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The fault report detail DTO.</returns>
        public async Task<FaultReportDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var entity = await this.dbContext.FaultReports
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                throw new NotFoundException($"Fault report with id '{id}' was not found.");
            }

            EnsureCanAccess(entity);

            return new FaultReportDetailDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                Location = entity.Location,
                Priority = entity.Priority.ToString(),
                Status = entity.Status.ToString(),
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByFullName = entity.CreatedByUser.FullName,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc
            };
        }

        /// <summary>
        /// Gets a paginated list of fault reports with filters and sorting,
        /// while enforcing role-based visibility rules.
        /// </summary>
        /// <param name="query">The query model.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A paginated result of fault reports.</returns>
        public async Task<PagedResult<FaultReportListItemDto>> GetListAsync(
            GetFaultReportsQuery query,
            CancellationToken cancellationToken)
        {
            IQueryable<FaultReport> faultReportsQuery = this.dbContext.FaultReports
                .AsNoTracking();

            if (!IsAdmin())
            {
                faultReportsQuery = faultReportsQuery
                    .Where(x => x.CreatedByUserId == this.currentUserService.UserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (!FaultReportQueryParsing.TryParseStatus(query.Status, out var status))
                {
                    throw new BusinessRuleException("Invalid status filter value.");
                }

                faultReportsQuery = faultReportsQuery.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Priority))
            {
                if (!FaultReportQueryParsing.TryParsePriority(query.Priority, out var priority))
                {
                    throw new BusinessRuleException("Invalid priority filter value.");
                }

                faultReportsQuery = faultReportsQuery.Where(x => x.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(query.Location))
            {
                var normalizedLocation = FaultReportLocationNormalizer.Normalize(query.Location);

                faultReportsQuery = faultReportsQuery.Where(
                    x => x.Location.Trim().ToLower().Contains(normalizedLocation));
            }

            faultReportsQuery = ApplySorting(faultReportsQuery, query);

            var totalCount = await faultReportsQuery.CountAsync(cancellationToken);

            var items = await faultReportsQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new FaultReportListItemDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Location = x.Location,
                    Priority = x.Priority.ToString(),
                    Status = x.Status.ToString(),
                    CreatedByFullName = x.CreatedByUser != null ? x.CreatedByUser.FullName : string.Empty,
                    CreatedAtUtc = x.CreatedAtUtc,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<FaultReportListItemDto>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }

        /// <summary>
        /// Updates an existing fault report while enforcing ownership rules and business rules.
        /// Status changes are not allowed through this method.
        /// </summary>
        /// <param name="id">The fault report identifier.</param>
        /// <param name="request">The update request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task UpdateAsync(
            Guid id,
            UpdateFaultReportRequest request,
            CancellationToken cancellationToken)
        {
            var entity = await this.dbContext.FaultReports
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                throw new NotFoundException($"Fault report with id '{id}' was not found.");
            }

            EnsureCanAccess(entity);

            if (!FaultReportQueryParsing.TryParsePriority(request.Priority, out var priority))
            {
                throw new BusinessRuleException("Invalid priority value.");
            }

            var normalizedNewLocation = FaultReportLocationNormalizer.Normalize(request.Location);
            var now = DateTime.UtcNow;
            var duplicateThreshold = now.AddHours(-1);

            var recentLocations = await this.dbContext.FaultReports
                .AsNoTracking()
                .Where(x => x.Id != id && x.CreatedAtUtc >= duplicateThreshold)
                .Select(x => x.Location)
                .ToListAsync(cancellationToken);

            var exists = recentLocations.Any(x =>
                FaultReportLocationNormalizer.Normalize(x) == normalizedNewLocation);

            if (exists)
            {
                throw new BusinessRuleException(
                    "A fault report for the same location has already been created within the last hour.");
            }

            entity.Title = request.Title.Trim();
            entity.Description = request.Description.Trim();
            entity.Location = request.Location.Trim();
            entity.Priority = priority;
            entity.UpdatedAtUtc = now;

            await this.dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Changes the status of a fault report according to the centralized transition policy.
        /// Only authorized transitions are allowed.
        /// </summary>
        /// <param name="id">The fault report identifier.</param>
        /// <param name="request">The status change request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ChangeStatusAsync(
            Guid id,
            ChangeFaultReportStatusRequest request,
            CancellationToken cancellationToken)
        {
            var entity = await this.dbContext.FaultReports
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                throw new NotFoundException($"Fault report with id '{id}' was not found.");
            }

            if (!IsAdmin())
            {
                throw new ForbiddenException("Only admin users can change fault report status.");
            }

            if (!FaultReportQueryParsing.TryParseStatus(request.Status, out var targetStatus))
            {
                throw new StatusTransitionException("Invalid target status value.");
            }

            var currentUserRole = ParseCurrentUserRole();

            var isAllowed = this.statusTransitionPolicy.CanTransition(
                currentUserRole,
                entity.Status,
                targetStatus);

            if (!isAllowed)
            {
                var validationMessage = this.statusTransitionPolicy.GetValidationMessage(
                    currentUserRole,
                    entity.Status,
                    targetStatus);

                throw new StatusTransitionException(
                    validationMessage ??
                    $"Status transition from '{entity.Status}' to '{targetStatus}' is not allowed.");
            }

            entity.Status = targetStatus;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await this.dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Deletes a fault report while enforcing ownership rules.
        /// </summary>
        /// <param name="id">The fault report identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var entity = await this.dbContext.FaultReports
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                throw new NotFoundException($"Fault report with id '{id}' was not found.");
            }

            EnsureCanAccess(entity);

            this.dbContext.FaultReports.Remove(entity);

            await this.dbContext.SaveChangesAsync(cancellationToken);
        }

        #region Helpers
        private void EnsureCanAccess(FaultReport entity)
        {
            if (IsAdmin())
            {
                return;
            }

            if (entity.CreatedByUserId != this.currentUserService.UserId)
            {
                throw new ForbiddenException("You are not allowed to access this fault report.");
            }
        }

        private bool IsAdmin()
        {
            return string.Equals(
                this.currentUserService.Role,
                UserRole.Admin.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        private UserRole ParseCurrentUserRole()
        {
            if (Enum.TryParse<UserRole>(this.currentUserService.Role, true, out var role))
            {
                return role;
            }

            throw new ForbiddenException("Current user role is invalid.");
        }

        private static IQueryable<FaultReport> ApplySorting(
            IQueryable<FaultReport> query,
            GetFaultReportsQuery request)
        {
            var sortBy = FaultReportQueryParsing.NormalizeSortBy(request.SortBy);
            var sortDirection = FaultReportQueryParsing.NormalizeSortDirection(request.SortDirection);

            return (sortBy, sortDirection) switch
            {
                ("priority", "asc") => query.OrderBy(x => x.Priority).ThenByDescending(x => x.CreatedAtUtc),
                ("priority", "desc") => query.OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAtUtc),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAtUtc),
                ("createdat", "desc") => query.OrderByDescending(x => x.CreatedAtUtc),
                _ => query.OrderByDescending(x => x.CreatedAtUtc)
            };
        }

        #endregion
    }
}
