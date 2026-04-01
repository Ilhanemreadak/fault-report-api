using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using LotusCode.Application.DTOs.Auth;
using LotusCode.Application.Exceptions;
using LotusCode.Application.Interfaces;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;
using LotusCode.Infrastructure.Persistence;
using LotusCode.Infrastructure.Services;
using LotusCode.Tests.Unit.Helpers;
using Moq;
using Xunit;

namespace LotusCode.Tests.Unit.Services
{
    /// <summary>
    /// Tests for AuthService that handles user authentication and JWT token generation.
    /// Validates credential verification, token generation, and error handling for invalid credentials.
    /// </summary>
    public class AuthServiceTests
    {
        private readonly Mock<IPasswordHasher> mockPasswordHasher;
        private readonly Mock<IJwtTokenService> mockJwtTokenService;

        public AuthServiceTests()
        {
            mockPasswordHasher = MockHelpers.CreateMockPasswordHasher();
            mockJwtTokenService = MockHelpers.CreateMockJwtTokenService();
        }

        #region LoginAsync Valid Credentials Tests

        [Fact]
        public async Task LoginAsync_WhenValidCredentials_ShouldReturnJwtToken()
        {
            // Arrange
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var user = TestDataBuilder.CreateUser(
                email: "user@example.com",
                passwordHash: "hashedpassword123",
                role: UserRole.User);
            
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = "password123"
            };

            mockPasswordHasher.Setup(x => x.Verify("password123", "hashedpassword123"))
                .Returns(true);

            var expectedToken = "jwt-token-12345";
            var expectedExpiry = DateTime.UtcNow.AddHours(1);
            mockJwtTokenService.Setup(x => x.GenerateToken(It.IsAny<User>()))
                .Returns((expectedToken, expectedExpiry));

            var sut = new AuthService(dbContext, mockPasswordHasher.Object, mockJwtTokenService.Object);

            // Act
            var result = await sut.LoginAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Token.Should().Be(expectedToken);
            result.ExpiresAtUtc.Should().Be(expectedExpiry);
            result.User.Should().NotBeNull();
            result.User.Id.Should().Be(user.Id);
            result.User.Email.Should().Be(user.Email);
            result.User.FullName.Should().Be(user.FullName);
            result.User.Role.Should().Be(user.Role.ToString());

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task LoginAsync_WhenValidCredentials_ShouldInvokePasswordHasherVerify()
        {
            // Arrange
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var user = TestDataBuilder.CreateUser(
                email: "user@example.com",
                passwordHash: "hashedpassword123");
            
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = "password123"
            };

            mockPasswordHasher.Setup(x => x.Verify("password123", "hashedpassword123"))
                .Returns(true);

            var sut = new AuthService(dbContext, mockPasswordHasher.Object, mockJwtTokenService.Object);

            // Act
            await sut.LoginAsync(request, CancellationToken.None);

            // Assert
            mockPasswordHasher.Verify(
                x => x.Verify("password123", "hashedpassword123"),
                Times.Once);

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task LoginAsync_WhenValidCredentials_ShouldInvokeJwtTokenServiceGenerateToken()
        {
            // Arrange
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var user = TestDataBuilder.CreateUser(
                email: "user@example.com",
                passwordHash: "hashedpassword123");
            
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = "password123"
            };

            mockPasswordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            var sut = new AuthService(dbContext, mockPasswordHasher.Object, mockJwtTokenService.Object);

            // Act
            await sut.LoginAsync(request, CancellationToken.None);

            // Assert
            mockJwtTokenService.Verify(
                x => x.GenerateToken(It.Is<User>(u => 
                    u.Id == user.Id && 
                    u.Email == user.Email &&
                    u.FullName == user.FullName &&
                    u.Role == user.Role)),
                Times.Once);

            // Cleanup
            dbContext.Dispose();
        }

        #endregion

        #region LoginAsync Invalid Credentials Tests

        [Fact]
        public async Task LoginAsync_WhenEmailDoesNotExist_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            
            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "password123"
            };

            var sut = new AuthService(dbContext, mockPasswordHasher.Object, mockJwtTokenService.Object);

            // Act
            Func<Task> act = async () => await sut.LoginAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid email or password.");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsIncorrect_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var user = TestDataBuilder.CreateUser(
                email: "user@example.com",
                passwordHash: "hashedpassword123");
            
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = "wrongpassword"
            };

            mockPasswordHasher.Setup(x => x.Verify("wrongpassword", "hashedpassword123"))
                .Returns(false);

            var sut = new AuthService(dbContext, mockPasswordHasher.Object, mockJwtTokenService.Object);

            // Act
            Func<Task> act = async () => await sut.LoginAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Invalid email or password.");

            // Cleanup
            dbContext.Dispose();
        }

        #endregion

        #region Property-Based Tests

        [Property(MaxTest = 100)]
        public void LoginAsync_ReturnsToken_ForAnyValidCredentials()
        {
            // Feature: comprehensive-unit-test-suite, Property 25: Successful authentication returns token
            
            FsCheck.Prop.ForAll<string, string>((email, password) =>
            {
                // Filter invalid inputs
                if (string.IsNullOrWhiteSpace(email) || 
                    string.IsNullOrWhiteSpace(password) ||
                    !email.Contains("@"))
                {
                    return true;
                }

                // Arrange
                var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
                var user = TestDataBuilder.CreateUser(
                    email: email,
                    passwordHash: "hashedpassword");
                
                dbContext.Users.Add(user);
                dbContext.SaveChanges();

                var request = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                var mockPasswordHasher = MockHelpers.CreateMockPasswordHasher(isValid: true);
                var mockJwtTokenService = MockHelpers.CreateMockJwtTokenService(
                    token: "generated-token",
                    expiresAt: DateTime.UtcNow.AddHours(1));

                var sut = new AuthService(
                    dbContext,
                    mockPasswordHasher.Object,
                    mockJwtTokenService.Object);

                // Act
                var result = sut.LoginAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var isValid = !string.IsNullOrWhiteSpace(result.Token) &&
                              result.ExpiresAtUtc > DateTime.UtcNow &&
                              result.User != null &&
                              result.User.Email == email;

                // Cleanup
                dbContext.Dispose();

                return isValid;
            }).QuickCheckThrowOnFailure();
        }

        [Property(MaxTest = 100)]
        public void LoginAsync_InvokesDependencies_ForAnyValidCredentials()
        {
            // Feature: comprehensive-unit-test-suite, Property 26: Authentication flow invokes dependencies
            
            FsCheck.Prop.ForAll<string, string>((email, password) =>
            {
                // Filter invalid inputs
                if (string.IsNullOrWhiteSpace(email) || 
                    string.IsNullOrWhiteSpace(password) ||
                    !email.Contains("@"))
                {
                    return true;
                }

                // Arrange
                var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
                var user = TestDataBuilder.CreateUser(
                    email: email,
                    passwordHash: "hashedpassword");
                
                dbContext.Users.Add(user);
                dbContext.SaveChanges();

                var request = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                var mockPasswordHasher = new Mock<IPasswordHasher>();
                mockPasswordHasher.Setup(x => x.Verify(password, "hashedpassword"))
                    .Returns(true);

                var mockJwtTokenService = new Mock<IJwtTokenService>();
                mockJwtTokenService.Setup(x => x.GenerateToken(It.IsAny<User>()))
                    .Returns(("token", DateTime.UtcNow.AddHours(1)));

                var sut = new AuthService(
                    dbContext,
                    mockPasswordHasher.Object,
                    mockJwtTokenService.Object);

                // Act
                var result = sut.LoginAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                // Assert - Verify both dependencies were invoked
                try
                {
                    mockPasswordHasher.Verify(
                        x => x.Verify(password, "hashedpassword"),
                        Times.Once);
                    
                    mockJwtTokenService.Verify(
                        x => x.GenerateToken(It.Is<User>(u => u.Email == email)),
                        Times.Once);

                    // Cleanup
                    dbContext.Dispose();

                    return true;
                }
                catch
                {
                    dbContext.Dispose();
                    return false;
                }
            }).QuickCheckThrowOnFailure();
        }

        #endregion
    }
}
