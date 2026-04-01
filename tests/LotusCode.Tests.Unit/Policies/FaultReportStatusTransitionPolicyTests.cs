using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using LotusCode.Domain.Enums;
using LotusCode.Domain.Policies;
using Xunit;

namespace LotusCode.Tests.Unit.Policies
{
    /// <summary>
    /// Tests for FaultReportStatusTransitionPolicy that validates
    /// status transition rules and role-based permissions.
    /// </summary>
    public class FaultReportStatusTransitionPolicyTests
    {
        private readonly FaultReportStatusTransitionPolicy _sut;

        public FaultReportStatusTransitionPolicyTests()
        {
            _sut = new FaultReportStatusTransitionPolicy();
        }

        #region CanTransition Admin Role Tests

        [Fact]
        public void CanTransition_WhenAdminWithValidTransition_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Reviewing;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenAdminWithInvalidTransition_ShouldReturnFalse()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Assigned;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void CanTransition_WhenNewToReviewing_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Reviewing;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenReviewingToAssigned_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.Reviewing;
            var targetStatus = FaultReportStatus.Assigned;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenReviewingToFalseAlarm_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.Reviewing;
            var targetStatus = FaultReportStatus.FalseAlarm;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenAssignedToInProgress_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.Assigned;
            var targetStatus = FaultReportStatus.InProgress;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenInProgressToCompleted_ShouldReturnTrue()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.InProgress;
            var targetStatus = FaultReportStatus.Completed;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanTransition_WhenNewToAssigned_ShouldReturnFalse()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Assigned;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeFalse();
        }

        #endregion


        #region CanTransition User Role Tests

        [Theory]
        [InlineData(FaultReportStatus.New, FaultReportStatus.Reviewing)]
        [InlineData(FaultReportStatus.Reviewing, FaultReportStatus.Assigned)]
        [InlineData(FaultReportStatus.Assigned, FaultReportStatus.InProgress)]
        [InlineData(FaultReportStatus.InProgress, FaultReportStatus.Completed)]
        [InlineData(FaultReportStatus.New, FaultReportStatus.Cancelled)]
        public void CanTransition_WhenUserRole_ShouldReturnFalse(
            FaultReportStatus currentStatus,
            FaultReportStatus targetStatus)
        {
            // Arrange
            var role = UserRole.User;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeFalse();
        }

        #endregion


        #region CanTransition Terminal State Tests

        [Theory]
        [InlineData(FaultReportStatus.New)]
        [InlineData(FaultReportStatus.Reviewing)]
        [InlineData(FaultReportStatus.Assigned)]
        [InlineData(FaultReportStatus.InProgress)]
        [InlineData(FaultReportStatus.Cancelled)]
        [InlineData(FaultReportStatus.FalseAlarm)]
        public void CanTransition_WhenCompletedToAnyStatus_ShouldReturnFalse(FaultReportStatus targetStatus)
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.Completed;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(FaultReportStatus.New)]
        [InlineData(FaultReportStatus.Reviewing)]
        [InlineData(FaultReportStatus.Assigned)]
        [InlineData(FaultReportStatus.InProgress)]
        [InlineData(FaultReportStatus.Completed)]
        [InlineData(FaultReportStatus.FalseAlarm)]
        public void CanTransition_WhenCancelledToAnyStatus_ShouldReturnFalse(FaultReportStatus targetStatus)
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.Cancelled;

            // Act
            var result = _sut.CanTransition(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeFalse();
        }

        #endregion


        #region GetValidationMessage Tests

        [Fact]
        public void GetValidationMessage_WhenUserRole_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var role = UserRole.User;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Reviewing;

            // Act
            var result = _sut.GetValidationMessage(role, currentStatus, targetStatus);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("admin");
        }

        [Fact]
        public void GetValidationMessage_WhenInvalidTransition_ShouldReturnDescriptiveMessage()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Assigned;

            // Act
            var result = _sut.GetValidationMessage(role, currentStatus, targetStatus);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("New");
            result.Should().Contain("Assigned");
            result.Should().Contain("not allowed");
        }

        [Fact]
        public void GetValidationMessage_WhenValidTransition_ShouldReturnNull()
        {
            // Arrange
            var role = UserRole.Admin;
            var currentStatus = FaultReportStatus.New;
            var targetStatus = FaultReportStatus.Reviewing;

            // Act
            var result = _sut.GetValidationMessage(role, currentStatus, targetStatus);

            // Assert
            result.Should().BeNull();
        }

        #endregion


        #region Property-Based Tests

        [Property(MaxTest = 100)]
        public Property CanTransition_AdminRoleAllowsValidTransitions()
        {
            // Feature: comprehensive-unit-test-suite, Property 20: Admin role allows valid transitions

            var validTransitions = new[]
            {
                (FaultReportStatus.New, FaultReportStatus.Reviewing),
                (FaultReportStatus.New, FaultReportStatus.Cancelled),
                (FaultReportStatus.Reviewing, FaultReportStatus.Assigned),
                (FaultReportStatus.Reviewing, FaultReportStatus.FalseAlarm),
                (FaultReportStatus.Reviewing, FaultReportStatus.Cancelled),
                (FaultReportStatus.Assigned, FaultReportStatus.InProgress),
                (FaultReportStatus.Assigned, FaultReportStatus.Cancelled),
                (FaultReportStatus.InProgress, FaultReportStatus.Completed),
                (FaultReportStatus.InProgress, FaultReportStatus.Cancelled)
            };

            return Prop.ForAll(
                Gen.Elements(validTransitions).ToArbitrary(),
                transition =>
                {
                    // Arrange
                    var (currentStatus, targetStatus) = transition;

                    // Act
                    var result = _sut.CanTransition(UserRole.Admin, currentStatus, targetStatus);

                    // Assert
                    return result.ToProperty();
                });
        }

        [Property(MaxTest = 100)]
        public Property CanTransition_AdminRoleRejectsInvalidTransitions()
        {
            // Feature: comprehensive-unit-test-suite, Property 21: Admin role rejects invalid transitions

            var invalidTransitions = new[]
            {
                (FaultReportStatus.New, FaultReportStatus.Assigned),
                (FaultReportStatus.New, FaultReportStatus.InProgress),
                (FaultReportStatus.New, FaultReportStatus.Completed),
                (FaultReportStatus.New, FaultReportStatus.FalseAlarm),
                (FaultReportStatus.Reviewing, FaultReportStatus.InProgress),
                (FaultReportStatus.Reviewing, FaultReportStatus.Completed),
                (FaultReportStatus.Assigned, FaultReportStatus.Reviewing),
                (FaultReportStatus.Assigned, FaultReportStatus.Completed),
                (FaultReportStatus.InProgress, FaultReportStatus.Assigned),
                (FaultReportStatus.Completed, FaultReportStatus.New),
                (FaultReportStatus.Cancelled, FaultReportStatus.New),
                (FaultReportStatus.FalseAlarm, FaultReportStatus.New)
            };

            return Prop.ForAll(
                Gen.Elements(invalidTransitions).ToArbitrary(),
                transition =>
                {
                    // Arrange
                    var (currentStatus, targetStatus) = transition;

                    // Act
                    var result = _sut.CanTransition(UserRole.Admin, currentStatus, targetStatus);

                    // Assert
                    return (!result).ToProperty();
                });
        }

        [Property(MaxTest = 100)]
        public Property CanTransition_UserRoleRejectsAllTransitions()
        {
            // Feature: comprehensive-unit-test-suite, Property 22: User role rejects all transitions

            var allStatuses = new[]
            {
                FaultReportStatus.New,
                FaultReportStatus.Reviewing,
                FaultReportStatus.Assigned,
                FaultReportStatus.InProgress,
                FaultReportStatus.Completed,
                FaultReportStatus.Cancelled,
                FaultReportStatus.FalseAlarm
            };

            return Prop.ForAll(
                Gen.Elements(allStatuses).ToArbitrary(),
                Gen.Elements(allStatuses).ToArbitrary(),
                (currentStatus, targetStatus) =>
                {
                    // Act
                    var result = _sut.CanTransition(UserRole.User, currentStatus, targetStatus);

                    // Assert
                    return (!result).ToProperty();
                });
        }

        [Property(MaxTest = 100)]
        public Property GetValidationMessage_ReturnsMessageForInvalidTransitions()
        {
            // Feature: comprehensive-unit-test-suite, Property 23: Validation message for invalid transitions

            var invalidTransitions = new[]
            {
                (FaultReportStatus.New, FaultReportStatus.Assigned),
                (FaultReportStatus.New, FaultReportStatus.InProgress),
                (FaultReportStatus.Reviewing, FaultReportStatus.InProgress),
                (FaultReportStatus.Completed, FaultReportStatus.New),
                (FaultReportStatus.Cancelled, FaultReportStatus.Reviewing)
            };

            return Prop.ForAll(
                Gen.Elements(invalidTransitions).ToArbitrary(),
                transition =>
                {
                    // Arrange
                    var (currentStatus, targetStatus) = transition;

                    // Act
                    var result = _sut.GetValidationMessage(UserRole.Admin, currentStatus, targetStatus);

                    // Assert
                    return (result != null && result.Contains("not allowed")).ToProperty();
                });
        }

        #endregion

    }
}
