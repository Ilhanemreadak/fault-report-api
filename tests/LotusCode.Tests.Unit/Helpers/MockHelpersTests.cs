using FluentAssertions;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;
using Moq;
using Xunit;

namespace LotusCode.Tests.Unit.Helpers
{
    /// <summary>
    /// Tests for MockHelpers to verify mock factory methods work correctly.
    /// </summary>
    public class MockHelpersTests
    {
        [Fact]
        public void CreateMockCurrentUserService_WithDefaults_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = MockHelpers.CreateMockCurrentUserService();

            // Assert
            mock.Should().NotBeNull();
            mock.Object.UserId.Should().NotBeEmpty();
            mock.Object.Role.Should().Be("User");
            mock.Object.Email.Should().Be("test@example.com");
            mock.Object.IsAuthenticated.Should().BeTrue();
        }

        [Fact]
        public void CreateMockCurrentUserService_WithCustomValues_ShouldReturnConfiguredMock()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var mock = MockHelpers.CreateMockCurrentUserService(
                userId: userId,
                role: "Admin",
                email: "admin@example.com",
                isAuthenticated: true);

            // Assert
            mock.Object.UserId.Should().Be(userId);
            mock.Object.Role.Should().Be("Admin");
            mock.Object.Email.Should().Be("admin@example.com");
            mock.Object.IsAuthenticated.Should().BeTrue();
        }

        [Fact]
        public void CreateMockStatusTransitionPolicy_WithDefaults_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = MockHelpers.CreateMockStatusTransitionPolicy();

            // Assert
            mock.Should().NotBeNull();
            mock.Object.CanTransition(UserRole.Admin, FaultReportStatus.New, FaultReportStatus.Reviewing)
                .Should().BeTrue();
            mock.Object.GetValidationMessage(UserRole.Admin, FaultReportStatus.New, FaultReportStatus.Reviewing)
                .Should().BeNull();
        }

        [Fact]
        public void CreateMockStatusTransitionPolicy_WithCustomValues_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = MockHelpers.CreateMockStatusTransitionPolicy(
                canTransition: false,
                validationMessage: "Invalid transition");

            // Assert
            mock.Object.CanTransition(UserRole.User, FaultReportStatus.New, FaultReportStatus.Reviewing)
                .Should().BeFalse();
            mock.Object.GetValidationMessage(UserRole.User, FaultReportStatus.New, FaultReportStatus.Reviewing)
                .Should().Be("Invalid transition");
        }

        [Fact]
        public void CreateMockPasswordHasher_WithDefaults_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = MockHelpers.CreateMockPasswordHasher();

            // Assert
            mock.Should().NotBeNull();
            mock.Object.Verify("password", "hash").Should().BeTrue();
            mock.Object.Hash("password").Should().Be("hashedpassword");
        }

        [Fact]
        public void CreateMockPasswordHasher_WithCustomValues_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = MockHelpers.CreateMockPasswordHasher(
                isValid: false,
                hashedPassword: "customhash");

            // Assert
            mock.Object.Verify("password", "hash").Should().BeFalse();
            mock.Object.Hash("password").Should().Be("customhash");
        }

        [Fact]
        public void CreateMockJwtTokenService_WithDefaults_ShouldReturnConfiguredMock()
        {
            // Arrange
            var user = TestDataBuilder.CreateUser();

            // Act
            var mock = MockHelpers.CreateMockJwtTokenService();

            // Assert
            mock.Should().NotBeNull();
            var (token, expiresAt) = mock.Object.GenerateToken(user);
            token.Should().Be("test-token");
            expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void CreateMockJwtTokenService_WithCustomValues_ShouldReturnConfiguredMock()
        {
            // Arrange
            var user = TestDataBuilder.CreateUser();
            var customExpiry = DateTime.UtcNow.AddDays(1);

            // Act
            var mock = MockHelpers.CreateMockJwtTokenService(
                token: "custom-token",
                expiresAt: customExpiry);

            // Assert
            var (token, expiresAt) = mock.Object.GenerateToken(user);
            token.Should().Be("custom-token");
            expiresAt.Should().Be(customExpiry);
        }
    }
}
