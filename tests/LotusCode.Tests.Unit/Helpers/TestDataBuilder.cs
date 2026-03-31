using LotusCode.Application.DTOs.FaultReports;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;

namespace LotusCode.Tests.Unit.Helpers
{
    /// <summary>
    /// Provides factory methods for creating test data with sensible defaults.
    /// Used to simplify test setup and ensure consistent test data across test cases.
    /// </summary>
    public static class TestDataBuilder
    {
        /// <summary>
        /// Creates a FaultReport entity with default or specified values.
        /// </summary>
        public static FaultReport CreateFaultReport(
            Guid? id = null,
            string title = "Test Fault",
            string description = "Test Description",
            string location = "Test Location",
            PriorityLevel priority = PriorityLevel.Medium,
            FaultReportStatus status = FaultReportStatus.New,
            Guid? createdByUserId = null,
            DateTime? createdAtUtc = null,
            DateTime? updatedAtUtc = null)
        {
            var now = DateTime.UtcNow;
            return new FaultReport
            {
                Id = id ?? Guid.NewGuid(),
                Title = title,
                Description = description,
                Location = location,
                Priority = priority,
                Status = status,
                CreatedByUserId = createdByUserId ?? Guid.NewGuid(),
                CreatedAtUtc = createdAtUtc ?? now,
                UpdatedAtUtc = updatedAtUtc ?? now
            };
        }

        /// <summary>
        /// Creates a User entity with default or specified values.
        /// </summary>
        public static User CreateUser(
            Guid? id = null,
            string fullName = "Test User",
            string email = "test@example.com",
            string passwordHash = "hashedpassword",
            UserRole role = UserRole.User)
        {
            return new User
            {
                Id = id ?? Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                PasswordHash = passwordHash,
                Role = role
            };
        }

        /// <summary>
        /// Creates a valid CreateFaultReportRequest with default or specified values.
        /// </summary>
        public static CreateFaultReportRequest CreateValidRequest(
            string title = "Test Fault",
            string description = "Test Description",
            string location = "Test Location",
            string priority = "Medium")
        {
            return new CreateFaultReportRequest
            {
                Title = title,
                Description = description,
                Location = location,
                Priority = priority
            };
        }

        /// <summary>
        /// Creates a ChangeFaultReportStatusRequest with default or specified status.
        /// </summary>
        public static ChangeFaultReportStatusRequest CreateStatusChangeRequest(
            string status = "Reviewing")
        {
            return new ChangeFaultReportStatusRequest
            {
                Status = status
            };
        }
    }
}
