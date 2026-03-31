using LotusCode.Application.Interfaces;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;
using LotusCode.Domain.Policies;
using Moq;

namespace LotusCode.Tests.Unit.Helpers
{
    /// <summary>
    /// Provides factory methods for creating configured mock objects.
    /// Used to simplify test setup and ensure consistent mock configurations across test cases.
    /// </summary>
    public static class MockHelpers
    {
        /// <summary>
        /// Creates a mock ICurrentUserService with configurable userId and role.
        /// </summary>
        /// <param name="userId">The user ID to return. If null, a new Guid is generated.</param>
        /// <param name="role">The user role to return. Defaults to "User".</param>
        /// <param name="email">The user email to return. Defaults to "test@example.com".</param>
        /// <param name="isAuthenticated">Whether the user is authenticated. Defaults to true.</param>
        /// <returns>A configured Mock of ICurrentUserService.</returns>
        public static Mock<ICurrentUserService> CreateMockCurrentUserService(
            Guid? userId = null,
            string role = "User",
            string email = "test@example.com",
            bool isAuthenticated = true)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(x => x.UserId).Returns(userId ?? Guid.NewGuid());
            mock.Setup(x => x.Role).Returns(role);
            mock.Setup(x => x.Email).Returns(email);
            mock.Setup(x => x.IsAuthenticated).Returns(isAuthenticated);
            return mock;
        }

        /// <summary>
        /// Creates a mock IFaultReportStatusTransitionPolicy with configurable behavior.
        /// </summary>
        /// <param name="canTransition">The value to return from CanTransition. Defaults to true.</param>
        /// <param name="validationMessage">The message to return from GetValidationMessage. Defaults to null.</param>
        /// <returns>A configured Mock of IFaultReportStatusTransitionPolicy.</returns>
        public static Mock<IFaultReportStatusTransitionPolicy> CreateMockStatusTransitionPolicy(
            bool canTransition = true,
            string? validationMessage = null)
        {
            var mock = new Mock<IFaultReportStatusTransitionPolicy>();
            mock.Setup(x => x.CanTransition(
                It.IsAny<UserRole>(),
                It.IsAny<FaultReportStatus>(),
                It.IsAny<FaultReportStatus>()))
                .Returns(canTransition);
            
            mock.Setup(x => x.GetValidationMessage(
                It.IsAny<UserRole>(),
                It.IsAny<FaultReportStatus>(),
                It.IsAny<FaultReportStatus>()))
                .Returns(validationMessage);
            
            return mock;
        }

        /// <summary>
        /// Creates a mock IPasswordHasher with configurable verification result.
        /// </summary>
        /// <param name="isValid">The value to return from Verify. Defaults to true.</param>
        /// <param name="hashedPassword">The value to return from Hash. Defaults to "hashedpassword".</param>
        /// <returns>A configured Mock of IPasswordHasher.</returns>
        public static Mock<IPasswordHasher> CreateMockPasswordHasher(
            bool isValid = true,
            string hashedPassword = "hashedpassword")
        {
            var mock = new Mock<IPasswordHasher>();
            mock.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(isValid);
            mock.Setup(x => x.Hash(It.IsAny<string>()))
                .Returns(hashedPassword);
            return mock;
        }

        /// <summary>
        /// Creates a mock IJwtTokenService with configurable token generation.
        /// </summary>
        /// <param name="token">The token string to return. Defaults to "test-token".</param>
        /// <param name="expiresAt">The expiration time to return. If null, defaults to 1 hour from now.</param>
        /// <returns>A configured Mock of IJwtTokenService.</returns>
        public static Mock<IJwtTokenService> CreateMockJwtTokenService(
            string token = "test-token",
            DateTime? expiresAt = null)
        {
            var mock = new Mock<IJwtTokenService>();
            mock.Setup(x => x.GenerateToken(It.IsAny<User>()))
                .Returns((token, expiresAt ?? DateTime.UtcNow.AddHours(1)));
            return mock;
        }
    }
}
