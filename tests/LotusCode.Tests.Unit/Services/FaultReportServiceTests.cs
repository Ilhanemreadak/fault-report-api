using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using LotusCode.Application.Exceptions;
using LotusCode.Application.Interfaces;
using LotusCode.Domain.Policies;
using LotusCode.Infrastructure.Persistence;
using LotusCode.Infrastructure.Services;
using LotusCode.Tests.Unit.Helpers;
using Moq;
using Xunit;

namespace LotusCode.Tests.Unit.Services
{
    /// <summary>
    /// Tests for FaultReportService business logic including creation,
    /// retrieval, listing, updates, and status transitions.
    /// Validates access control, business rules, and data persistence.
    /// </summary>
    public class FaultReportServiceTests
    {
        private readonly Mock<ICurrentUserService> mockCurrentUserService;
        private readonly Mock<IFaultReportStatusTransitionPolicy> mockStatusTransitionPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultReportServiceTests"/> class.
        /// Sets up mock dependencies that are reused across test methods.
        /// Note: DbContext is created per test method for isolation.
        /// </summary>
        public FaultReportServiceTests()
        {
            mockCurrentUserService = MockHelpers.CreateMockCurrentUserService();
            mockStatusTransitionPolicy = MockHelpers.CreateMockStatusTransitionPolicy();
        }

        [Fact]
        public async Task CreateAsync_WhenValidRequest_ShouldReturnValidGuidAndSaveToDatabase()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var request = TestDataBuilder.CreateValidRequest(
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: "High");

            var beforeCreate = DateTime.UtcNow;

            // Act
            var result = await sut.CreateAsync(request, CancellationToken.None);

            var afterCreate = DateTime.UtcNow;

            // Assert
            result.Should().NotBeEmpty("CreateAsync should return a valid Guid");

            var savedReport = await dbContext.FaultReports.FindAsync(result);
            savedReport.Should().NotBeNull("Fault report should be saved to database");
            savedReport!.Status.Should().Be(Domain.Enums.FaultReportStatus.New, "Status should be initialized to New");
            savedReport.CreatedByUserId.Should().Be(userId, "CreatedByUserId should be set from ICurrentUserService");
            savedReport.CreatedAtUtc.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2), "CreatedAtUtc should be set to current UTC time");
            savedReport.CreatedAtUtc.Should().BeBefore(afterCreate.AddSeconds(1));
            savedReport.UpdatedAtUtc.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2), "UpdatedAtUtc should be set to current UTC time");
            savedReport.UpdatedAtUtc.Should().BeBefore(afterCreate.AddSeconds(1));

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicateLocationWithinOneHour_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            
            // Create an existing fault report with a specific location 30 minutes ago
            var existingReport = TestDataBuilder.CreateFaultReport(
                location: "Building A",
                createdAtUtc: DateTime.UtcNow.AddMinutes(-30),
                createdByUserId: userId);
            
            dbContext.FaultReports.Add(existingReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create a request with the same location but with different case and whitespace
            var request = TestDataBuilder.CreateValidRequest(
                title: "Another Fault",
                description: "Another Description",
                location: "  BUILDING A  ", // Different case and whitespace
                priority: "Medium");

            // Act
            Func<Task> act = async () => await sut.CreateAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*same location*within the last hour*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicateLocationOutsideOneHour_ShouldSucceed()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            
            // Create an existing fault report with a specific location 61 minutes ago (outside the one-hour window)
            var existingReport = TestDataBuilder.CreateFaultReport(
                location: "Building A",
                createdAtUtc: DateTime.UtcNow.AddMinutes(-61),
                createdByUserId: userId);
            
            dbContext.FaultReports.Add(existingReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create a request with the same location
            var request = TestDataBuilder.CreateValidRequest(
                title: "Another Fault",
                description: "Another Description",
                location: "Building A",
                priority: "Medium");

            // Act
            var result = await sut.CreateAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty("CreateAsync should succeed when duplicate location is outside one-hour window");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task CreateAsync_WhenLocationNormalizationApplied_ShouldDetectDuplicates()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            
            // Create an existing fault report with lowercase location
            var existingReport = TestDataBuilder.CreateFaultReport(
                location: "building a",
                createdAtUtc: DateTime.UtcNow.AddMinutes(-15),
                createdByUserId: userId);
            
            dbContext.FaultReports.Add(existingReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Test various normalized forms of the same location
            var testCases = new[]
            {
                "BUILDING A",           // All uppercase
                "Building A",           // Mixed case
                "  building a  ",       // With whitespace
                "  BUILDING A  ",       // Uppercase with whitespace
                "\tBuilding A\t"        // With tabs
            };

            foreach (var locationVariant in testCases)
            {
                var request = TestDataBuilder.CreateValidRequest(
                    title: "Test Fault",
                    description: "Test Description",
                    location: locationVariant,
                    priority: "High");

                // Act
                Func<Task> act = async () => await sut.CreateAsync(request, CancellationToken.None);

                // Assert
                await act.Should().ThrowAsync<BusinessRuleException>()
                    .WithMessage("*same location*within the last hour*",
                        $"Location normalization should detect '{locationVariant}' as duplicate of 'building a'");
            }

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task CreateAsync_WhenInvalidPriority_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var request = TestDataBuilder.CreateValidRequest(
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: "InvalidPriority"); // Invalid priority value

            // Act
            Func<Task> act = async () => await sut.CreateAsync(request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("Invalid priority value.");

            // Cleanup
            dbContext.Dispose();
        }

        // Feature: comprehensive-unit-test-suite, Property 1: Fault report creation returns valid identifier
        // **Validates: Requirements 1.1**
        [Property(MaxTest = 100)]
        public Property CreateAsync_ReturnsValidGuid_ForAnyValidRequest()
        {
            return Prop.ForAll(
                GenerateValidCreateRequest(),
                async request =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Act
                    var result = await sut.CreateAsync(request, CancellationToken.None);

                    // Assert
                    result.Should().NotBeEmpty("CreateAsync should return a valid non-empty Guid for any valid request");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary valid CreateFaultReportRequest instances for property-based testing.
        /// </summary>
        private static Arbitrary<Application.DTOs.FaultReports.CreateFaultReportRequest> GenerateValidCreateRequest()
        {
            var titleGen = Gen.Elements(
                "Critical System Failure",
                "Network Outage",
                "Hardware Malfunction",
                "Software Bug",
                "Security Vulnerability",
                "Performance Issue",
                "Data Corruption",
                "Service Unavailable",
                "Configuration Error",
                "User Access Problem"
            );

            var descriptionGen = Gen.Elements(
                "The system has encountered a critical error and requires immediate attention.",
                "Multiple users are reporting connectivity issues with the network infrastructure.",
                "Hardware component showing signs of failure and needs replacement.",
                "Application crashes when performing specific operations.",
                "Potential security breach detected in the authentication system.",
                "System response time has degraded significantly under normal load.",
                "Database integrity check revealed corrupted records.",
                "Service endpoint is not responding to requests.",
                "Configuration settings are causing unexpected behavior.",
                "Users unable to access their accounts due to permission issues."
            );

            var locationGen = Gen.Elements(
                "Building A - Floor 1",
                "Building A - Floor 2",
                "Building B - Floor 1",
                "Building B - Floor 3",
                "Building C - Basement",
                "Data Center - Room 101",
                "Data Center - Room 202",
                "Server Room Alpha",
                "Server Room Beta",
                "Office Wing East",
                "Office Wing West",
                "Parking Lot Level 1",
                "Parking Lot Level 2",
                "Main Entrance",
                "Conference Room A"
            );

            var priorityGen = Gen.Elements("Low", "Medium", "High");

            return Arb.From(
                from title in titleGen
                from description in descriptionGen
                from location in locationGen
                from priority in priorityGen
                select new Application.DTOs.FaultReports.CreateFaultReportRequest
                {
                    Title = title,
                    Description = description,
                    Location = location,
                    Priority = priority
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 2: Fault report creation initializes status to New
        // **Validates: Requirements 1.2**
        [Property(MaxTest = 100)]
        public Property CreateAsync_InitializesStatusToNew_ForAnyValidRequest()
        {
            return Prop.ForAll(
                GenerateValidCreateRequest(),
                async request =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();
                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Act
                    var result = await sut.CreateAsync(request, CancellationToken.None);

                    // Assert
                    var savedReport = await dbContext.FaultReports.FindAsync(result);
                    savedReport.Should().NotBeNull("Fault report should be saved to database");
                    savedReport!.Status.Should().Be(Domain.Enums.FaultReportStatus.New, 
                        "For any valid CreateFaultReportRequest, the created fault report should have Status set to FaultReportStatus.New in the database");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        [Fact]
        public async Task GetByIdAsync_WhenUserAccessesOwnReport_ShouldReturnDtoWithCreatedByUserInfo()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create a user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "John Doe",
                email: "john.doe@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the user
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Act
            var result = await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetByIdAsync should return a DTO");
            result.Id.Should().Be(faultReport.Id, "DTO should have the correct fault report ID");
            result.Title.Should().Be(faultReport.Title, "DTO should have the correct title");
            result.Description.Should().Be(faultReport.Description, "DTO should have the correct description");
            result.Location.Should().Be(faultReport.Location, "DTO should have the correct location");
            result.Priority.Should().Be(faultReport.Priority.ToString(), "DTO should have the correct priority");
            result.Status.Should().Be(faultReport.Status.ToString(), "DTO should have the correct status");
            result.CreatedByUserId.Should().Be(userId, "DTO should have the correct CreatedByUserId");
            result.CreatedByFullName.Should().Be("John Doe", "DTO should include CreatedByUser FullName information");
            result.CreatedAtUtc.Should().BeCloseTo(faultReport.CreatedAtUtc, TimeSpan.FromSeconds(1), "DTO should have the correct CreatedAtUtc");
            result.UpdatedAtUtc.Should().BeCloseTo(faultReport.UpdatedAtUtc, TimeSpan.FromSeconds(1), "DTO should have the correct UpdatedAtUtc");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetByIdAsync_WhenUserAccessesAnotherUsersReport_ShouldThrowForbiddenException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: currentUserId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the other user
            var otherUser = TestDataBuilder.CreateUser(
                id: otherUserId,
                fullName: "Other User",
                email: "other.user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the other user
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Other User's Fault Report",
                description: "This belongs to another user",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: otherUserId);

            dbContext.Users.Add(otherUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Act
            Func<Task> act = async () => await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("*not allowed to access*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetByIdAsync_WhenAdminAccessesAnyReport_ShouldReturnDto()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var reportOwnerId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create the report owner
            var reportOwner = TestDataBuilder.CreateUser(
                id: reportOwnerId,
                fullName: "Report Owner",
                email: "owner@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by another user (not the admin)
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "User's Fault Report",
                description: "This belongs to a regular user",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: reportOwnerId);

            dbContext.Users.Add(adminUser);
            dbContext.Users.Add(reportOwner);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Act
            var result = await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("Admin should be able to retrieve any fault report");
            result.Id.Should().Be(faultReport.Id, "DTO should have the correct fault report ID");
            result.Title.Should().Be(faultReport.Title, "DTO should have the correct title");
            result.Description.Should().Be(faultReport.Description, "DTO should have the correct description");
            result.Location.Should().Be(faultReport.Location, "DTO should have the correct location");
            result.Priority.Should().Be(faultReport.Priority.ToString(), "DTO should have the correct priority");
            result.Status.Should().Be(faultReport.Status.ToString(), "DTO should have the correct status");
            result.CreatedByUserId.Should().Be(reportOwnerId, "DTO should have the correct CreatedByUserId");
            result.CreatedByFullName.Should().Be("Report Owner", "DTO should include CreatedByUser FullName information");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetByIdAsync_WhenNonExistentId_ShouldThrowNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var nonExistentId = Guid.NewGuid();

            // Act
            Func<Task> act = async () => await sut.GetByIdAsync(nonExistentId, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("*not found*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenUserRole_ShouldReturnOnlyOwnFaultReports()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: currentUserId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the current user
            var currentUser = TestDataBuilder.CreateUser(
                id: currentUserId,
                fullName: "Current User",
                email: "current.user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create another user
            var otherUser = TestDataBuilder.CreateUser(
                id: otherUserId,
                fullName: "Other User",
                email: "other.user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create fault reports owned by the current user
            var currentUserReport1 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Current User Report 1",
                description: "Description 1",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: currentUserId);

            var currentUserReport2 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Current User Report 2",
                description: "Description 2",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: currentUserId);

            // Create fault reports owned by the other user
            var otherUserReport1 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Other User Report 1",
                description: "Description 3",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: otherUserId);

            var otherUserReport2 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Other User Report 2",
                description: "Description 4",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: otherUserId);

            dbContext.Users.Add(currentUser);
            dbContext.Users.Add(otherUser);
            dbContext.FaultReports.Add(currentUserReport1);
            dbContext.FaultReports.Add(currentUserReport2);
            dbContext.FaultReports.Add(otherUserReport1);
            dbContext.FaultReports.Add(otherUserReport2);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(2, "User role should only see their own fault reports");
            result.Items.Should().OnlyContain(x => x.Id == currentUserReport1.Id || x.Id == currentUserReport2.Id,
                "All returned reports should belong to the current user");
            result.Items.Should().NotContain(x => x.Id == otherUserReport1.Id || x.Id == otherUserReport2.Id,
                "Reports from other users should not be included");
            result.TotalCount.Should().Be(2, "TotalCount should reflect only the current user's reports");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenAdminRole_ShouldReturnAllFaultReports()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create regular users
            var user1 = TestDataBuilder.CreateUser(
                id: user1Id,
                fullName: "User One",
                email: "user1@example.com",
                role: Domain.Enums.UserRole.User);

            var user2 = TestDataBuilder.CreateUser(
                id: user2Id,
                fullName: "User Two",
                email: "user2@example.com",
                role: Domain.Enums.UserRole.User);

            // Create fault reports owned by different users
            var adminReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Admin Report",
                description: "Admin's report",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: adminUserId);

            var user1Report1 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "User 1 Report 1",
                description: "User 1's first report",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: user1Id);

            var user1Report2 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "User 1 Report 2",
                description: "User 1's second report",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: user1Id);

            var user2Report = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "User 2 Report",
                description: "User 2's report",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: user2Id);

            dbContext.Users.Add(adminUser);
            dbContext.Users.Add(user1);
            dbContext.Users.Add(user2);
            dbContext.FaultReports.Add(adminReport);
            dbContext.FaultReports.Add(user1Report1);
            dbContext.FaultReports.Add(user1Report2);
            dbContext.FaultReports.Add(user2Report);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(4, "Admin role should see all fault reports");
            result.Items.Should().Contain(x => x.Id == adminReport.Id, "Admin should see their own report");
            result.Items.Should().Contain(x => x.Id == user1Report1.Id, "Admin should see User 1's first report");
            result.Items.Should().Contain(x => x.Id == user1Report2.Id, "Admin should see User 1's second report");
            result.Items.Should().Contain(x => x.Id == user2Report.Id, "Admin should see User 2's report");
            result.TotalCount.Should().Be(4, "TotalCount should reflect all fault reports in the system");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenStatusFilter_ShouldReturnOnlyMatchingReports()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create fault reports with different statuses
            var newReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "New Report",
                description: "Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            var reviewingReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Reviewing Report",
                description: "Description",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: userId);

            var assignedReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Assigned Report",
                description: "Description",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: userId);

            var inProgressReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "InProgress Report",
                description: "Description",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(newReport);
            dbContext.FaultReports.Add(reviewingReport);
            dbContext.FaultReports.Add(assignedReport);
            dbContext.FaultReports.Add(inProgressReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Status = "Reviewing",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(1, "Only reports with Reviewing status should be returned");
            result.Items.Should().OnlyContain(x => x.Status == "Reviewing", "All returned reports should have Reviewing status");
            result.Items.First().Id.Should().Be(reviewingReport.Id, "The returned report should be the reviewing report");
            result.TotalCount.Should().Be(1, "TotalCount should reflect only the filtered reports");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenPriorityFilter_ShouldReturnOnlyMatchingReports()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create fault reports with different priorities
            var highReport1 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "High Priority Report 1",
                description: "Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            var mediumReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Medium Priority Report",
                description: "Description",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: userId);

            var lowReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Low Priority Report",
                description: "Description",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: userId);

            var highReport2 = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "High Priority Report 2",
                description: "Description",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(highReport1);
            dbContext.FaultReports.Add(mediumReport);
            dbContext.FaultReports.Add(lowReport);
            dbContext.FaultReports.Add(highReport2);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Priority = "High",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(2, "Only reports with High priority should be returned");
            result.Items.Should().OnlyContain(x => x.Priority == "High", "All returned reports should have High priority");
            result.Items.Should().Contain(x => x.Id == highReport1.Id, "The first high priority report should be included");
            result.Items.Should().Contain(x => x.Id == highReport2.Id, "The second high priority report should be included");
            result.TotalCount.Should().Be(2, "TotalCount should reflect only the filtered reports");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenLocationFilter_ShouldReturnReportsContainingSubstring()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create fault reports with different locations
            var buildingAReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report 1",
                description: "Description",
                location: "Building A - Floor 1",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            var buildingBReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report 2",
                description: "Description",
                location: "Building B - Floor 2",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: userId);

            var buildingAFloor2Report = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report 3",
                description: "Description",
                location: "Building A - Floor 2",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: userId);

            var parkingLotReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report 4",
                description: "Description",
                location: "Parking Lot",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(buildingAReport);
            dbContext.FaultReports.Add(buildingBReport);
            dbContext.FaultReports.Add(buildingAFloor2Report);
            dbContext.FaultReports.Add(parkingLotReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Location = "Building A",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(2, "Only reports with locations containing 'Building A' should be returned");
            result.Items.Should().Contain(x => x.Id == buildingAReport.Id, "Building A - Floor 1 report should be included");
            result.Items.Should().Contain(x => x.Id == buildingAFloor2Report.Id, "Building A - Floor 2 report should be included");
            result.Items.Should().NotContain(x => x.Id == buildingBReport.Id, "Building B report should not be included");
            result.Items.Should().NotContain(x => x.Id == parkingLotReport.Id, "Parking Lot report should not be included");
            result.TotalCount.Should().Be(2, "TotalCount should reflect only the filtered reports");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenInvalidStatusFilter_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Status = "InvalidStatus",
                Page = 1,
                PageSize = 10
            };

            // Act
            Func<Task> act = async () => await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("Invalid status filter value.");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenInvalidPriorityFilter_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Priority = "InvalidPriority",
                Page = 1,
                PageSize = 10
            };

            // Act
            Func<Task> act = async () => await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("Invalid priority filter value.");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenPaginationApplied_ShouldReturnCorrectPageOfResults()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create 15 fault reports to test pagination
            var reports = new List<Domain.Entities.FaultReport>();
            for (int i = 1; i <= 15; i++)
            {
                var report = TestDataBuilder.CreateFaultReport(
                    id: Guid.NewGuid(),
                    title: $"Report {i}",
                    description: $"Description {i}",
                    location: $"Building {i}",
                    priority: Domain.Enums.PriorityLevel.Medium,
                    status: Domain.Enums.FaultReportStatus.New,
                    createdByUserId: userId,
                    createdAtUtc: DateTime.UtcNow.AddMinutes(-i)); // Different timestamps for ordering
                reports.Add(report);
            }

            dbContext.Users.Add(user);
            dbContext.FaultReports.AddRange(reports);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Test Page 1 with PageSize 5
            var queryPage1 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 5
            };

            // Act
            var resultPage1 = await sut.GetListAsync(queryPage1, CancellationToken.None);

            // Assert
            resultPage1.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPage1.Items.Should().HaveCount(5, "Page 1 with PageSize 5 should return 5 items");
            resultPage1.Page.Should().Be(1, "Page should be 1");
            resultPage1.PageSize.Should().Be(5, "PageSize should be 5");

            // Test Page 2 with PageSize 5
            var queryPage2 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 2,
                PageSize = 5
            };

            // Act
            var resultPage2 = await sut.GetListAsync(queryPage2, CancellationToken.None);

            // Assert
            resultPage2.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPage2.Items.Should().HaveCount(5, "Page 2 with PageSize 5 should return 5 items");
            resultPage2.Page.Should().Be(2, "Page should be 2");
            resultPage2.PageSize.Should().Be(5, "PageSize should be 5");

            // Verify that Page 1 and Page 2 contain different items
            var page1Ids = resultPage1.Items.Select(x => x.Id).ToList();
            var page2Ids = resultPage2.Items.Select(x => x.Id).ToList();
            page1Ids.Should().NotIntersectWith(page2Ids, "Page 1 and Page 2 should contain different items");

            // Test Page 3 with PageSize 5 (should return remaining 5 items)
            var queryPage3 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 3,
                PageSize = 5
            };

            // Act
            var resultPage3 = await sut.GetListAsync(queryPage3, CancellationToken.None);

            // Assert
            resultPage3.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPage3.Items.Should().HaveCount(5, "Page 3 with PageSize 5 should return 5 items");
            resultPage3.Page.Should().Be(3, "Page should be 3");
            resultPage3.PageSize.Should().Be(5, "PageSize should be 5");

            // Test Page 4 with PageSize 5 (should return 0 items as we only have 15 total)
            var queryPage4 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 4,
                PageSize = 5
            };

            // Act
            var resultPage4 = await sut.GetListAsync(queryPage4, CancellationToken.None);

            // Assert
            resultPage4.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPage4.Items.Should().HaveCount(0, "Page 4 with PageSize 5 should return 0 items when only 15 total items exist");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenPaginationApplied_ShouldReturnCorrectTotalCountAndTotalPages()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create 23 fault reports to test TotalCount and TotalPages calculation
            var reports = new List<Domain.Entities.FaultReport>();
            for (int i = 1; i <= 23; i++)
            {
                var report = TestDataBuilder.CreateFaultReport(
                    id: Guid.NewGuid(),
                    title: $"Report {i}",
                    description: $"Description {i}",
                    location: $"Building {i}",
                    priority: Domain.Enums.PriorityLevel.Medium,
                    status: Domain.Enums.FaultReportStatus.New,
                    createdByUserId: userId);
                reports.Add(report);
            }

            dbContext.Users.Add(user);
            dbContext.FaultReports.AddRange(reports);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Test with PageSize 10 (should have 3 pages: 10, 10, 3)
            var queryPageSize10 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var resultPageSize10 = await sut.GetListAsync(queryPageSize10, CancellationToken.None);

            // Assert
            resultPageSize10.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPageSize10.TotalCount.Should().Be(23, "TotalCount should be 23 for all fault reports");
            resultPageSize10.TotalPages.Should().Be(3, "TotalPages should be 3 when 23 items with PageSize 10 (ceil(23/10) = 3)");
            resultPageSize10.Items.Should().HaveCount(10, "Page 1 should return 10 items");

            // Test with PageSize 5 (should have 5 pages: 5, 5, 5, 5, 3)
            var queryPageSize5 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 5
            };

            // Act
            var resultPageSize5 = await sut.GetListAsync(queryPageSize5, CancellationToken.None);

            // Assert
            resultPageSize5.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPageSize5.TotalCount.Should().Be(23, "TotalCount should be 23 for all fault reports");
            resultPageSize5.TotalPages.Should().Be(5, "TotalPages should be 5 when 23 items with PageSize 5 (ceil(23/5) = 5)");
            resultPageSize5.Items.Should().HaveCount(5, "Page 1 should return 5 items");

            // Test with PageSize 20 (should have 2 pages: 20, 3)
            var queryPageSize20 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 1,
                PageSize = 20
            };

            // Act
            var resultPageSize20 = await sut.GetListAsync(queryPageSize20, CancellationToken.None);

            // Assert
            resultPageSize20.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPageSize20.TotalCount.Should().Be(23, "TotalCount should be 23 for all fault reports");
            resultPageSize20.TotalPages.Should().Be(2, "TotalPages should be 2 when 23 items with PageSize 20 (ceil(23/20) = 2)");
            resultPageSize20.Items.Should().HaveCount(20, "Page 1 should return 20 items");

            // Test Page 2 with PageSize 20 (should return remaining 3 items)
            var queryPage2PageSize20 = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Page = 2,
                PageSize = 20
            };

            // Act
            var resultPage2PageSize20 = await sut.GetListAsync(queryPage2PageSize20, CancellationToken.None);

            // Assert
            resultPage2PageSize20.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultPage2PageSize20.TotalCount.Should().Be(23, "TotalCount should be 23 for all fault reports");
            resultPage2PageSize20.TotalPages.Should().Be(2, "TotalPages should be 2 when 23 items with PageSize 20");
            resultPage2PageSize20.Items.Should().HaveCount(3, "Page 2 should return remaining 3 items");

            // Test with filters to verify TotalCount reflects filtered results
            // Add some reports with different status
            var reviewingReports = new List<Domain.Entities.FaultReport>();
            for (int i = 1; i <= 7; i++)
            {
                var report = TestDataBuilder.CreateFaultReport(
                    id: Guid.NewGuid(),
                    title: $"Reviewing Report {i}",
                    description: $"Description {i}",
                    location: $"Building {i}",
                    priority: Domain.Enums.PriorityLevel.High,
                    status: Domain.Enums.FaultReportStatus.Reviewing,
                    createdByUserId: userId);
                reviewingReports.Add(report);
            }

            dbContext.FaultReports.AddRange(reviewingReports);
            await dbContext.SaveChangesAsync();

            var queryWithFilter = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                Status = "Reviewing",
                Page = 1,
                PageSize = 5
            };

            // Act
            var resultWithFilter = await sut.GetListAsync(queryWithFilter, CancellationToken.None);

            // Assert
            resultWithFilter.Should().NotBeNull("GetListAsync should return a PagedResult");
            resultWithFilter.TotalCount.Should().Be(7, "TotalCount should be 7 for filtered Reviewing reports");
            resultWithFilter.TotalPages.Should().Be(2, "TotalPages should be 2 when 7 items with PageSize 5 (ceil(7/5) = 2)");
            resultWithFilter.Items.Should().HaveCount(5, "Page 1 should return 5 items");
            resultWithFilter.Items.Should().OnlyContain(x => x.Status == "Reviewing", "All items should have Reviewing status");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenSortByPriorityAscending_ShouldReturnReportsInPriorityOrder()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create fault reports with different priorities in random order
            var highReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "High Priority Report",
                description: "Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            var lowReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Low Priority Report",
                description: "Description",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: userId);

            var mediumReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Medium Priority Report",
                description: "Description",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: userId);

            var anotherHighReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Another High Priority Report",
                description: "Description",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(highReport);
            dbContext.FaultReports.Add(lowReport);
            dbContext.FaultReports.Add(mediumReport);
            dbContext.FaultReports.Add(anotherHighReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                SortBy = "priority",
                SortDirection = "asc",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(4, "All 4 fault reports should be returned");
            
            // Verify ascending order: Low, Medium, High, High
            var priorities = result.Items.Select(x => x.Priority).ToList();
            
            // Verify the specific order: Low should come first, then Medium, then High reports
            priorities[0].Should().Be("Low", "First report should have Low priority");
            priorities[1].Should().Be("Medium", "Second report should have Medium priority");
            priorities[2].Should().Be("High", "Third report should have High priority");
            priorities[3].Should().Be("High", "Fourth report should have High priority");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task GetListAsync_WhenSortByCreatedAtDescending_ShouldReturnReportsNewestFirst()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create fault reports with different creation dates
            var oldestReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Oldest Report",
                description: "Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddDays(-10));

            var middleReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Middle Report",
                description: "Description",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.Reviewing,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddDays(-5));

            var newestReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Newest Report",
                description: "Description",
                location: "Building C",
                priority: Domain.Enums.PriorityLevel.Low,
                status: Domain.Enums.FaultReportStatus.Assigned,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddDays(-1));

            var secondNewestReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Second Newest Report",
                description: "Description",
                location: "Building D",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.InProgress,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddDays(-3));

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(oldestReport);
            dbContext.FaultReports.Add(middleReport);
            dbContext.FaultReports.Add(newestReport);
            dbContext.FaultReports.Add(secondNewestReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
            {
                SortBy = "createdat",
                SortDirection = "desc",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await sut.GetListAsync(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull("GetListAsync should return a PagedResult");
            result.Items.Should().HaveCount(4, "All 4 fault reports should be returned");
            
            // Verify descending order: newest first
            var createdDates = result.Items.Select(x => x.CreatedAtUtc).ToList();
            createdDates.Should().BeInDescendingOrder("Reports should be sorted by creation date in descending order (newest first)");
            
            // Verify the specific order by checking IDs
            result.Items[0].Id.Should().Be(newestReport.Id, "First report should be the newest (1 day ago)");
            result.Items[1].Id.Should().Be(secondNewestReport.Id, "Second report should be 3 days old");
            result.Items[2].Id.Should().Be(middleReport.Id, "Third report should be 5 days old");
            result.Items[3].Id.Should().Be(oldestReport.Id, "Fourth report should be the oldest (10 days ago)");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenUserUpdatesOwnReport_ShouldUpdateFieldsAndSetUpdatedAtUtcWithoutChangingStatus()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the user with initial status
            var initialStatus = Domain.Enums.FaultReportStatus.Reviewing;
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Original Title",
                description: "Original Description",
                location: "Original Location",
                priority: Domain.Enums.PriorityLevel.Low,
                status: initialStatus,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddDays(-2),
                updatedAtUtc: DateTime.UtcNow.AddDays(-1));

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create an update request with new values
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Location = "Updated Location",
                Priority = "High"
            };

            var beforeUpdate = DateTime.UtcNow;

            // Act
            await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

            var afterUpdate = DateTime.UtcNow;

            // Assert
            var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
            updatedReport.Should().NotBeNull("Fault report should exist in database");
            updatedReport!.Title.Should().Be("Updated Title", "Title should be updated");
            updatedReport.Description.Should().Be("Updated Description", "Description should be updated");
            updatedReport.Location.Should().Be("Updated Location", "Location should be updated");
            updatedReport.Priority.Should().Be(Domain.Enums.PriorityLevel.High, "Priority should be updated");
            updatedReport.Status.Should().Be(initialStatus, "Status should remain unchanged after update");
            updatedReport.UpdatedAtUtc.Should().BeCloseTo(beforeUpdate, TimeSpan.FromSeconds(2), "UpdatedAtUtc should be set to current UTC time");
            updatedReport.UpdatedAtUtc.Should().BeBefore(afterUpdate.AddSeconds(1));
            updatedReport.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromSeconds(2), "CreatedAtUtc should remain unchanged");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenUserUpdatesAnotherUsersReport_ShouldThrowForbiddenException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: currentUserId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the current user
            var currentUser = TestDataBuilder.CreateUser(
                id: currentUserId,
                fullName: "Current User",
                email: "current.user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create the other user
            var otherUser = TestDataBuilder.CreateUser(
                id: otherUserId,
                fullName: "Other User",
                email: "other.user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the other user
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Other User's Report",
                description: "This belongs to another user",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: otherUserId);

            dbContext.Users.Add(currentUser);
            dbContext.Users.Add(otherUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create an update request
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Attempted Update",
                Description = "Trying to update another user's report",
                Location = "Building C",
                Priority = "High"
            };

            // Act
            Func<Task> act = async () => await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("*not allowed to access*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenAdminUpdatesAnyReport_ShouldUpdateSuccessfully()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var reportOwnerId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create the report owner
            var reportOwner = TestDataBuilder.CreateUser(
                id: reportOwnerId,
                fullName: "Report Owner",
                email: "owner@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by another user (not the admin)
            var initialStatus = Domain.Enums.FaultReportStatus.Assigned;
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Original Title",
                description: "Original Description",
                location: "Original Location",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: initialStatus,
                createdByUserId: reportOwnerId,
                createdAtUtc: DateTime.UtcNow.AddDays(-3),
                updatedAtUtc: DateTime.UtcNow.AddDays(-2));

            dbContext.Users.Add(adminUser);
            dbContext.Users.Add(reportOwner);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create an update request with new values
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Admin Updated Title",
                Description = "Admin Updated Description",
                Location = "Admin Updated Location",
                Priority = "High"
            };

            var beforeUpdate = DateTime.UtcNow;

            // Act
            await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

            var afterUpdate = DateTime.UtcNow;

            // Assert
            var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
            updatedReport.Should().NotBeNull("Admin should be able to update any fault report");
            updatedReport!.Title.Should().Be("Admin Updated Title", "Title should be updated by admin");
            updatedReport.Description.Should().Be("Admin Updated Description", "Description should be updated by admin");
            updatedReport.Location.Should().Be("Admin Updated Location", "Location should be updated by admin");
            updatedReport.Priority.Should().Be(Domain.Enums.PriorityLevel.High, "Priority should be updated by admin");
            updatedReport.Status.Should().Be(initialStatus, "Status should remain unchanged after update");
            updatedReport.CreatedByUserId.Should().Be(reportOwnerId, "CreatedByUserId should remain unchanged");
            updatedReport.UpdatedAtUtc.Should().BeCloseTo(beforeUpdate, TimeSpan.FromSeconds(2), "UpdatedAtUtc should be set to current UTC time");
            updatedReport.UpdatedAtUtc.Should().BeBefore(afterUpdate.AddSeconds(1));
            updatedReport.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(-3), TimeSpan.FromSeconds(2), "CreatedAtUtc should remain unchanged");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenNonExistentId_ShouldThrowNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var nonExistentId = Guid.NewGuid();
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Location = "Updated Location",
                Priority = "High"
            };

            // Act
            Func<Task> act = async () => await sut.UpdateAsync(nonExistentId, updateRequest, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("*not found*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenInvalidPriority_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the user
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Original Title",
                description: "Original Description",
                location: "Original Location",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create an update request with invalid priority
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Location = "Updated Location",
                Priority = "InvalidPriority" // Invalid priority value
            };

            // Act
            Func<Task> act = async () => await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("Invalid priority value.");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenDuplicateLocationWithinOneHour_ShouldThrowBusinessRuleException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.User);

            // Create an existing fault report with a specific location 30 minutes ago
            var existingReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Existing Report",
                description: "Existing Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddMinutes(-30));

            // Create another fault report that we will try to update to the same location
            var reportToUpdate = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report to Update",
                description: "Description to Update",
                location: "Building B",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(existingReport);
            dbContext.FaultReports.Add(reportToUpdate);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Create an update request with the same location as the existing report (with different case and whitespace)
            var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Location = "  BUILDING A  ", // Same location as existing report, different case and whitespace
                Priority = "High"
            };

            // Act
            Func<Task> act = async () => await sut.UpdateAsync(reportToUpdate.Id, updateRequest, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*same location*within the last hour*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task UpdateAsync_WhenLocationNormalizationApplied_ShouldDetectDuplicates()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Test User",
                email: "test@example.com",
                role: Domain.Enums.UserRole.User);

            // Create an existing fault report with lowercase location
            var existingReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Existing Report",
                description: "Existing Description",
                location: "building a",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId,
                createdAtUtc: DateTime.UtcNow.AddMinutes(-15));

            // Create another fault report that we will try to update
            var reportToUpdate = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Report to Update",
                description: "Description to Update",
                location: "Building Z",
                priority: Domain.Enums.PriorityLevel.Medium,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(existingReport);
            dbContext.FaultReports.Add(reportToUpdate);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            // Test various normalized forms of the same location
            var testCases = new[]
            {
                "BUILDING A",           // All uppercase
                "Building A",           // Mixed case
                "  building a  ",       // With whitespace
                "  BUILDING A  ",       // Uppercase with whitespace
                "\tBuilding A\t"        // With tabs
            };

            foreach (var locationVariant in testCases)
            {
                var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
                {
                    Title = "Updated Title",
                    Description = "Updated Description",
                    Location = locationVariant,
                    Priority = "High"
                };

                // Act
                Func<Task> act = async () => await sut.UpdateAsync(reportToUpdate.Id, updateRequest, CancellationToken.None);

                // Assert
                await act.Should().ThrowAsync<BusinessRuleException>()
                    .WithMessage("*same location*within the last hour*",
                        $"Location normalization should detect '{locationVariant}' as duplicate of 'building a'");
            }

            // Cleanup
            dbContext.Dispose();
        }

        // Feature: comprehensive-unit-test-suite, Property 3: Duplicate location prevention
        // **Validates: Requirements 1.3**
        [Property(MaxTest = 100)]
        public Property CreateAsync_PreventsDuplicateLocation_ForAnyLocationWithinOneHour()
        {
            return Prop.ForAll(
                GenerateLocationVariants(),
                async locationData =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create an existing fault report with the base location within the last hour
                    var existingReport = TestDataBuilder.CreateFaultReport(
                        location: locationData.BaseLocation,
                        createdAtUtc: DateTime.UtcNow.AddMinutes(-30), // 30 minutes ago, within one hour
                        createdByUserId: userId);

                    dbContext.FaultReports.Add(existingReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Create a request with a variant of the same location (different case/whitespace)
                    var request = TestDataBuilder.CreateValidRequest(
                        title: "New Fault Report",
                        description: "Test Description",
                        location: locationData.VariantLocation,
                        priority: "High");

                    // Act
                    Func<Task> act = async () => await sut.CreateAsync(request, CancellationToken.None);

                    // Assert
                    await act.Should().ThrowAsync<BusinessRuleException>(
                        $"For any location string, if a fault report exists for that normalized location within the last hour, " +
                        $"attempting to create another fault report with the same normalized location should throw BusinessRuleException. " +
                        $"Base location: '{locationData.BaseLocation}', Variant: '{locationData.VariantLocation}'");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary location variants for property-based testing of duplicate location prevention.
        /// Each generated value contains a base location and a variant with different case/whitespace.
        /// </summary>
        private static Arbitrary<(string BaseLocation, string VariantLocation)> GenerateLocationVariants()
        {
            var baseLocationGen = Gen.Elements(
                "Building A",
                "Building B - Floor 1",
                "Data Center Room 101",
                "Server Room Alpha",
                "Office Wing East",
                "Parking Lot Level 1",
                "Conference Room A",
                "Main Entrance",
                "Building C - Basement"
            );

            var variantTransformGen = Gen.Elements<Func<string, string>>(
                loc => loc.ToUpper(),                    // All uppercase
                loc => loc.ToLower(),                    // All lowercase
                loc => $"  {loc}  ",                     // Add leading/trailing spaces
                loc => $"  {loc.ToUpper()}  ",           // Uppercase with spaces
                loc => $"\t{loc}\t",                     // Add tabs
                loc => $" {loc.ToLower()} ",             // Lowercase with spaces
                loc => loc                                // No transformation (exact match)
            );

            return Arb.From(
                from baseLocation in baseLocationGen
                from variantTransform in variantTransformGen
                select (baseLocation, variantTransform(baseLocation))
            );
        }

        // NOTE: Property 4 test for location normalization was skipped (optional task 3.7)
        // The unit test in task 3.5 (CreateAsync_WhenLocationNormalizationApplied_ShouldDetectDuplicates)
        // already provides adequate coverage for location normalization behavior.
        // A property-based test would require more complex setup to handle EF Core InMemory database
        // limitations with string operations in LINQ queries.

        // Feature: comprehensive-unit-test-suite, Property 7: Access control for owned reports
        // **Validates: Requirements 2.1, 2.2**
        [Property(MaxTest = 100)]
        public Property GetByIdAsync_EnforcesAccessControl_ForNonAdminUsers()
        {
            return Prop.ForAll(
                GenerateAccessControlScenario(),
                async scenario =>
                {
                    // Arrange
                    var currentUserId = Guid.NewGuid();
                    var otherUserId = Guid.NewGuid();
                    var reportOwnerId = scenario.IsOwnReport ? currentUserId : otherUserId;

                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: currentUserId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create users
                    var currentUser = TestDataBuilder.CreateUser(
                        id: currentUserId,
                        fullName: "Current User",
                        email: "current@example.com",
                        role: Domain.Enums.UserRole.User);

                    var otherUser = TestDataBuilder.CreateUser(
                        id: otherUserId,
                        fullName: "Other User",
                        email: "other@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create a fault report
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: scenario.Title,
                        description: scenario.Description,
                        location: scenario.Location,
                        priority: scenario.Priority,
                        status: scenario.Status,
                        createdByUserId: reportOwnerId);

                    dbContext.Users.Add(currentUser);
                    dbContext.Users.Add(otherUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Act & Assert
                    if (scenario.IsOwnReport)
                    {
                        // User accessing their own report - should succeed
                        var result = await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);
                        result.Should().NotBeNull(
                            "For any non-admin user, GetByIdAsync should succeed when accessing their own fault reports");
                        result.Id.Should().Be(faultReport.Id);
                        result.CreatedByUserId.Should().Be(currentUserId);
                    }
                    else
                    {
                        // User accessing another user's report - should throw ForbiddenException
                        Func<Task> act = async () => await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);
                        await act.Should().ThrowAsync<ForbiddenException>(
                            "For any non-admin user, GetByIdAsync should throw ForbiddenException when accessing reports created by other users");
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 7: Access control for owned reports
        // **Validates: Requirements 4.1, 4.2**
        [Property(MaxTest = 100)]
        public Property UpdateAsync_EnforcesAccessControl_ForNonAdminUsers()
        {
            return Prop.ForAll(
                GenerateAccessControlScenario(),
                async scenario =>
                {
                    // Arrange
                    var currentUserId = Guid.NewGuid();
                    var otherUserId = Guid.NewGuid();
                    var reportOwnerId = scenario.IsOwnReport ? currentUserId : otherUserId;

                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: currentUserId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create users
                    var currentUser = TestDataBuilder.CreateUser(
                        id: currentUserId,
                        fullName: "Current User",
                        email: "current@example.com",
                        role: Domain.Enums.UserRole.User);

                    var otherUser = TestDataBuilder.CreateUser(
                        id: otherUserId,
                        fullName: "Other User",
                        email: "other@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create a fault report
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: scenario.Title,
                        description: scenario.Description,
                        location: scenario.Location,
                        priority: scenario.Priority,
                        status: scenario.Status,
                        createdByUserId: reportOwnerId);

                    dbContext.Users.Add(currentUser);
                    dbContext.Users.Add(otherUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Create an update request
                    var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
                    {
                        Title = "Updated Title",
                        Description = "Updated Description",
                        Location = "Updated Location",
                        Priority = "High"
                    };

                    // Act & Assert
                    if (scenario.IsOwnReport)
                    {
                        // User updating their own report - should succeed
                        await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);
                        
                        var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
                        updatedReport.Should().NotBeNull(
                            "For any non-admin user, UpdateAsync should succeed when accessing their own fault reports");
                        updatedReport!.Title.Should().Be("Updated Title");
                        updatedReport.CreatedByUserId.Should().Be(currentUserId);
                    }
                    else
                    {
                        // User updating another user's report - should throw ForbiddenException
                        Func<Task> act = async () => await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);
                        await act.Should().ThrowAsync<ForbiddenException>(
                            "For any non-admin user, UpdateAsync should throw ForbiddenException when accessing reports created by other users");
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 8: Admin access override
        // **Validates: Requirements 2.3**
        [Property(MaxTest = 100)]
        public Property GetByIdAsync_AdminCanAccessAnyReport()
        {
            return Prop.ForAll(
                GenerateAccessControlScenario(),
                async scenario =>
                {
                    // Arrange
                    var adminUserId = Guid.NewGuid();
                    var reportOwnerId = Guid.NewGuid(); // Different from admin

                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create admin user
                    var adminUser = TestDataBuilder.CreateUser(
                        id: adminUserId,
                        fullName: "Admin User",
                        email: "admin@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create report owner (different from admin)
                    var reportOwner = TestDataBuilder.CreateUser(
                        id: reportOwnerId,
                        fullName: "Report Owner",
                        email: "owner@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create a fault report owned by another user
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: scenario.Title,
                        description: scenario.Description,
                        location: scenario.Location,
                        priority: scenario.Priority,
                        status: scenario.Status,
                        createdByUserId: reportOwnerId);

                    dbContext.Users.Add(adminUser);
                    dbContext.Users.Add(reportOwner);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Act
                    var result = await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);

                    // Assert
                    result.Should().NotBeNull(
                        "For any admin user, GetByIdAsync should succeed for all fault reports regardless of ownership");
                    result.Id.Should().Be(faultReport.Id, "The returned DTO should have the correct fault report ID");
                    result.Title.Should().Be(scenario.Title, "The returned DTO should have the correct title");
                    result.Description.Should().Be(scenario.Description, "The returned DTO should have the correct description");
                    result.Location.Should().Be(scenario.Location, "The returned DTO should have the correct location");
                    result.Priority.Should().Be(scenario.Priority.ToString(), "The returned DTO should have the correct priority");
                    result.Status.Should().Be(scenario.Status.ToString(), "The returned DTO should have the correct status");
                    result.CreatedByUserId.Should().Be(reportOwnerId, "The returned DTO should have the correct CreatedByUserId");
                    result.CreatedByFullName.Should().Be("Report Owner", "The returned DTO should include CreatedByUser FullName information");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 14: Status immutability through UpdateAsync
        // **Validates: Requirements 4.7**
        [Property(MaxTest = 100)]
        public Property UpdateAsync_StatusRemainsUnchanged_ForAnyUpdateRequest()
        {
            return Prop.ForAll(
                GenerateUpdateScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Test User",
                        email: "test@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create a fault report with an initial status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Original Title",
                        description: "Original Description",
                        location: scenario.OriginalLocation,
                        priority: Domain.Enums.PriorityLevel.Low,
                        status: scenario.InitialStatus,
                        createdByUserId: userId,
                        createdAtUtc: DateTime.UtcNow.AddDays(-2),
                        updatedAtUtc: DateTime.UtcNow.AddDays(-1));

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Create an update request with new values
                    var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
                    {
                        Title = scenario.NewTitle,
                        Description = scenario.NewDescription,
                        Location = scenario.NewLocation,
                        Priority = scenario.NewPriority
                    };

                    // Act
                    await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

                    // Assert
                    var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
                    updatedReport.Should().NotBeNull("Fault report should exist in database");
                    updatedReport!.Status.Should().Be(scenario.InitialStatus,
                        "For any fault report update via UpdateAsync, the Status field should remain unchanged regardless of the update request content");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary update scenarios for property-based testing of status immutability.
        /// Each scenario includes an initial status and various update field values.
        /// </summary>
        private static Arbitrary<UpdateScenario> GenerateUpdateScenario()
        {
            var initialStatusGen = Gen.Elements(
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed,
                Domain.Enums.FaultReportStatus.Cancelled,
                Domain.Enums.FaultReportStatus.FalseAlarm
            );

            var titleGen = Gen.Elements(
                "Updated Critical System Failure",
                "Updated Network Outage",
                "Updated Hardware Malfunction",
                "Updated Software Bug",
                "Updated Security Vulnerability",
                "Updated Performance Issue",
                "Updated Data Corruption"
            );

            var descriptionGen = Gen.Elements(
                "Updated: The system has encountered a critical error.",
                "Updated: Multiple users are reporting connectivity issues.",
                "Updated: Hardware component showing signs of failure.",
                "Updated: Application crashes when performing operations.",
                "Updated: Potential security breach detected.",
                "Updated: System response time has degraded significantly.",
                "Updated: Database integrity check revealed corrupted records."
            );

            var originalLocationGen = Gen.Elements(
                "Original Building A",
                "Original Building B",
                "Original Building C",
                "Original Data Center",
                "Original Server Room",
                "Original Office Wing",
                "Original Conference Room"
            );

            var newLocationGen = Gen.Elements(
                "Updated Building X",
                "Updated Building Y",
                "Updated Building Z",
                "Updated Data Center",
                "Updated Server Room",
                "Updated Office Wing",
                "Updated Conference Room"
            );

            var priorityGen = Gen.Elements("Low", "Medium", "High");

            return Arb.From(
                from initialStatus in initialStatusGen
                from newTitle in titleGen
                from newDescription in descriptionGen
                from originalLocation in originalLocationGen
                from newLocation in newLocationGen
                from newPriority in priorityGen
                select new UpdateScenario
                {
                    InitialStatus = initialStatus,
                    NewTitle = newTitle,
                    NewDescription = newDescription,
                    OriginalLocation = originalLocation,
                    NewLocation = newLocation,
                    NewPriority = newPriority
                });
        }
        // Feature: comprehensive-unit-test-suite, Property 6: Timestamp management
        // **Validates: Requirements 1.7, 4.8, 5.7**
        [Property(MaxTest = 100)]
        public Property TimestampManagement_SetsTimestampsCorrectly_ForCreateAndUpdateOperations()
        {
            return Prop.ForAll(
                GenerateTimestampScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Test User",
                        email: "test@example.com",
                        role: Domain.Enums.UserRole.User);

                    dbContext.Users.Add(user);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    if (scenario.OperationType == "Create")
                    {
                        // Test CreateAsync timestamp management
                        var createRequest = TestDataBuilder.CreateValidRequest(
                            title: scenario.Title,
                            description: scenario.Description,
                            location: scenario.Location,
                            priority: scenario.Priority);

                        var beforeCreate = DateTime.UtcNow;

                        // Act
                        var reportId = await sut.CreateAsync(createRequest, CancellationToken.None);

                        var afterCreate = DateTime.UtcNow;

                        // Assert
                        var createdReport = await dbContext.FaultReports.FindAsync(reportId);
                        createdReport.Should().NotBeNull("Fault report should be saved to database");
                        createdReport!.CreatedAtUtc.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2),
                            "For any create operation, CreatedAtUtc should be set to the current UTC time (Requirement 1.7)");
                        createdReport.CreatedAtUtc.Should().BeBefore(afterCreate.AddSeconds(1));
                        createdReport.UpdatedAtUtc.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2),
                            "For any create operation, UpdatedAtUtc should be set to the current UTC time (Requirement 1.7)");
                        createdReport.UpdatedAtUtc.Should().BeBefore(afterCreate.AddSeconds(1));
                    }
                    else if (scenario.OperationType == "Update")
                    {
                        // Test UpdateAsync timestamp management
                        var originalCreatedAt = DateTime.UtcNow.AddDays(-5);
                        var originalUpdatedAt = DateTime.UtcNow.AddDays(-3);

                        var faultReport = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: "Original Title",
                            description: "Original Description",
                            location: "Original Location",
                            priority: Domain.Enums.PriorityLevel.Low,
                            status: Domain.Enums.FaultReportStatus.New,
                            createdByUserId: userId,
                            createdAtUtc: originalCreatedAt,
                            updatedAtUtc: originalUpdatedAt);

                        dbContext.FaultReports.Add(faultReport);
                        await dbContext.SaveChangesAsync();

                        var updateRequest = new Application.DTOs.FaultReports.UpdateFaultReportRequest
                        {
                            Title = scenario.Title,
                            Description = scenario.Description,
                            Location = scenario.Location,
                            Priority = scenario.Priority
                        };

                        var beforeUpdate = DateTime.UtcNow;

                        // Act
                        await sut.UpdateAsync(faultReport.Id, updateRequest, CancellationToken.None);

                        var afterUpdate = DateTime.UtcNow;

                        // Assert
                        var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
                        updatedReport.Should().NotBeNull("Fault report should exist in database");
                        updatedReport!.UpdatedAtUtc.Should().BeCloseTo(beforeUpdate, TimeSpan.FromSeconds(2),
                            "For any update operation, UpdatedAtUtc should be set to the current UTC time (Requirement 4.8)");
                        updatedReport.UpdatedAtUtc.Should().BeBefore(afterUpdate.AddSeconds(1));
                        updatedReport.CreatedAtUtc.Should().BeCloseTo(originalCreatedAt, TimeSpan.FromSeconds(1),
                            "For any update operation, CreatedAtUtc should remain unchanged");
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary timestamp management scenarios for property-based testing.
        /// Each scenario includes an operation type (Create or Update) and fault report data.
        /// </summary>
        private static Arbitrary<TimestampScenario> GenerateTimestampScenario()
        {
            var operationTypeGen = Gen.Elements("Create", "Update");

            var titleGen = Gen.Elements(
                "Critical System Failure",
                "Network Outage",
                "Hardware Malfunction",
                "Software Bug",
                "Security Vulnerability",
                "Performance Issue",
                "Data Corruption"
            );

            var descriptionGen = Gen.Elements(
                "The system has encountered a critical error.",
                "Multiple users are reporting connectivity issues.",
                "Hardware component showing signs of failure.",
                "Application crashes when performing operations.",
                "Potential security breach detected.",
                "System response time has degraded significantly.",
                "Database integrity check revealed corrupted records."
            );

            var locationGen = Gen.Elements(
                "Building A - Floor 1",
                "Building B - Floor 2",
                "Building C - Basement",
                "Data Center - Room 101",
                "Server Room Alpha",
                "Office Wing East",
                "Conference Room A"
            );

            var priorityGen = Gen.Elements("Low", "Medium", "High");

            return Arb.From(
                from operationType in operationTypeGen
                from title in titleGen
                from description in descriptionGen
                from location in locationGen
                from priority in priorityGen
                select new TimestampScenario
                {
                    OperationType = operationType,
                    Title = title,
                    Description = description,
                    Location = location,
                    Priority = priority
                });
        }

        /// <summary>
        /// Represents a timestamp management test scenario with operation type and fault report data.
        /// </summary>
        private class TimestampScenario
        {
            public string OperationType { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
        }


        /// <summary>
        /// Represents an update test scenario with initial status and new field values.
        /// </summary>
        private class UpdateScenario
        {
            public Domain.Enums.FaultReportStatus InitialStatus { get; set; }
            public string NewTitle { get; set; } = string.Empty;
            public string NewDescription { get; set; } = string.Empty;
            public string OriginalLocation { get; set; } = string.Empty;
            public string NewLocation { get; set; } = string.Empty;
            public string NewPriority { get; set; } = string.Empty;
        }

        // Feature: comprehensive-unit-test-suite, Property 9: DTO population with user information
        // **Validates: Requirements 2.5**
        [Property(MaxTest = 100)]
        public Property GetByIdAsync_PopulatesDtoWithUserInformation()
        {
            return Prop.ForAll(
                GenerateDtoPopulationScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create user with specific full name
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: scenario.UserFullName,
                        email: scenario.UserEmail,
                        role: Domain.Enums.UserRole.Admin);

                    // Create a fault report owned by the user
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: scenario.Title,
                        description: scenario.Description,
                        location: scenario.Location,
                        priority: scenario.Priority,
                        status: scenario.Status,
                        createdByUserId: userId);

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    // Act
                    var result = await sut.GetByIdAsync(faultReport.Id, CancellationToken.None);

                    // Assert
                    result.Should().NotBeNull(
                        "For any fault report retrieved via GetByIdAsync, the returned FaultReportDetailDto should not be null");
                    result.Id.Should().Be(faultReport.Id, 
                        "The returned DTO should include the correct fault report ID");
                    result.Title.Should().Be(scenario.Title, 
                        "The returned DTO should include the correct title");
                    result.Description.Should().Be(scenario.Description, 
                        "The returned DTO should include the correct description");
                    result.Location.Should().Be(scenario.Location, 
                        "The returned DTO should include the correct location");
                    result.Priority.Should().Be(scenario.Priority.ToString(), 
                        "The returned DTO should include the correct priority");
                    result.Status.Should().Be(scenario.Status.ToString(), 
                        "The returned DTO should include the correct status");
                    result.CreatedByUserId.Should().Be(userId, 
                        "The returned DTO should include the correct CreatedByUserId");
                    result.CreatedByFullName.Should().Be(scenario.UserFullName, 
                        "For any fault report retrieved via GetByIdAsync, the returned FaultReportDetailDto should include the CreatedByUser FullName information");
                    result.CreatedAtUtc.Should().BeCloseTo(faultReport.CreatedAtUtc, TimeSpan.FromSeconds(1), 
                        "The returned DTO should include the correct CreatedAtUtc timestamp");
                    result.UpdatedAtUtc.Should().BeCloseTo(faultReport.UpdatedAtUtc, TimeSpan.FromSeconds(1), 
                        "The returned DTO should include the correct UpdatedAtUtc timestamp");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary DTO population scenarios for property-based testing.
        /// Each scenario includes fault report data and user information to verify DTO population.
        /// </summary>
        private static Arbitrary<DtoPopulationScenario> GenerateDtoPopulationScenario()
        {
            var titleGen = Gen.Elements(
                "Critical System Failure",
                "Network Outage",
                "Hardware Malfunction",
                "Software Bug",
                "Security Vulnerability",
                "Performance Issue",
                "Data Corruption"
            );

            var descriptionGen = Gen.Elements(
                "The system has encountered a critical error.",
                "Multiple users are reporting connectivity issues.",
                "Hardware component showing signs of failure.",
                "Application crashes when performing operations.",
                "Potential security breach detected.",
                "System response time has degraded significantly.",
                "Database integrity check revealed corrupted records."
            );

            var locationGen = Gen.Elements(
                "Building A - Floor 1",
                "Building B - Floor 2",
                "Building C - Basement",
                "Data Center - Room 101",
                "Server Room Alpha",
                "Office Wing East",
                "Conference Room A"
            );

            var priorityGen = Gen.Elements(
                Domain.Enums.PriorityLevel.Low,
                Domain.Enums.PriorityLevel.Medium,
                Domain.Enums.PriorityLevel.High
            );

            var statusGen = Gen.Elements(
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed
            );

            var userFullNameGen = Gen.Elements(
                "John Doe",
                "Jane Smith",
                "Alice Johnson",
                "Bob Williams",
                "Charlie Brown",
                "Diana Prince",
                "Edward Norton"
            );

            var userEmailGen = Gen.Elements(
                "john.doe@example.com",
                "jane.smith@example.com",
                "alice.johnson@example.com",
                "bob.williams@example.com",
                "charlie.brown@example.com",
                "diana.prince@example.com",
                "edward.norton@example.com"
            );

            return Arb.From(
                from title in titleGen
                from description in descriptionGen
                from location in locationGen
                from priority in priorityGen
                from status in statusGen
                from userFullName in userFullNameGen
                from userEmail in userEmailGen
                select new DtoPopulationScenario
                {
                    Title = title,
                    Description = description,
                    Location = location,
                    Priority = priority,
                    Status = status,
                    UserFullName = userFullName,
                    UserEmail = userEmail
                });
        }

        /// <summary>
        /// Represents a DTO population test scenario with fault report data and user information.
        /// </summary>
        private class DtoPopulationScenario
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public Domain.Enums.PriorityLevel Priority { get; set; }
            public Domain.Enums.FaultReportStatus Status { get; set; }
            public string UserFullName { get; set; } = string.Empty;
            public string UserEmail { get; set; } = string.Empty;
        }

        /// <summary>
        /// Generates arbitrary access control scenarios for property-based testing.
        /// Each scenario includes fault report data and whether the report is owned by the current user.
        /// </summary>
        private static Arbitrary<AccessControlScenario> GenerateAccessControlScenario()
        {
            var titleGen = Gen.Elements(
                "Critical System Failure",
                "Network Outage",
                "Hardware Malfunction",
                "Software Bug",
                "Security Vulnerability"
            );

            var descriptionGen = Gen.Elements(
                "The system has encountered a critical error.",
                "Multiple users are reporting connectivity issues.",
                "Hardware component showing signs of failure.",
                "Application crashes when performing operations.",
                "Potential security breach detected."
            );

            var locationGen = Gen.Elements(
                "Building A - Floor 1",
                "Building B - Floor 2",
                "Data Center - Room 101",
                "Server Room Alpha",
                "Office Wing East"
            );

            var priorityGen = Gen.Elements(
                Domain.Enums.PriorityLevel.Low,
                Domain.Enums.PriorityLevel.Medium,
                Domain.Enums.PriorityLevel.High
            );

            var statusGen = Gen.Elements(
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress
            );

            var isOwnReportGen = Gen.Elements(true, false);

            return Arb.From(
                from title in titleGen
                from description in descriptionGen
                from location in locationGen
                from priority in priorityGen
                from status in statusGen
                from isOwnReport in isOwnReportGen
                select new AccessControlScenario
                {
                    Title = title,
                    Description = description,
                    Location = location,
                    Priority = priority,
                    Status = status,
                    IsOwnReport = isOwnReport
                });
        }

        /// <summary>
        /// Represents an access control test scenario with fault report data and ownership information.
        /// </summary>
        private class AccessControlScenario
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public Domain.Enums.PriorityLevel Priority { get; set; }
            public Domain.Enums.FaultReportStatus Status { get; set; }
            public bool IsOwnReport { get; set; }
        }

        // Feature: comprehensive-unit-test-suite, Property 10: Role-based list visibility
        // **Validates: Requirements 3.1, 3.2**
        [Property(MaxTest = 100)]
        public Property GetListAsync_EnforcesRoleBasedVisibility()
        {
            return Prop.ForAll(
                GenerateRoleBasedListScenario(),
                async scenario =>
                {
                    // Arrange
                    var currentUserId = Guid.NewGuid();
                    var otherUserId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(
                        userId: currentUserId, 
                        role: scenario.UserRole);
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the current user
                    var currentUser = TestDataBuilder.CreateUser(
                        id: currentUserId,
                        fullName: "Current User",
                        email: "current.user@example.com",
                        role: scenario.UserRole == "Admin" ? Domain.Enums.UserRole.Admin : Domain.Enums.UserRole.User);

                    // Create another user
                    var otherUser = TestDataBuilder.CreateUser(
                        id: otherUserId,
                        fullName: "Other User",
                        email: "other.user@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create fault reports owned by the current user
                    var currentUserReports = new List<Domain.Entities.FaultReport>();
                    for (int i = 0; i < scenario.CurrentUserReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Current User Report {i + 1}",
                            description: $"Description {i + 1}",
                            location: $"Building {i + 1}",
                            priority: scenario.Priority,
                            status: scenario.Status,
                            createdByUserId: currentUserId);
                        currentUserReports.Add(report);
                    }

                    // Create fault reports owned by the other user
                    var otherUserReports = new List<Domain.Entities.FaultReport>();
                    for (int i = 0; i < scenario.OtherUserReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Other User Report {i + 1}",
                            description: $"Description {i + 1}",
                            location: $"Building {i + 100}",
                            priority: scenario.Priority,
                            status: scenario.Status,
                            createdByUserId: otherUserId);
                        otherUserReports.Add(report);
                    }

                    dbContext.Users.Add(currentUser);
                    dbContext.Users.Add(otherUser);
                    dbContext.FaultReports.AddRange(currentUserReports);
                    dbContext.FaultReports.AddRange(otherUserReports);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
                    {
                        Page = 1,
                        PageSize = 100 // Large enough to get all reports
                    };

                    // Act
                    var result = await sut.GetListAsync(query, CancellationToken.None);

                    // Assert
                    var currentUserReportIds = currentUserReports.Select(r => r.Id).ToHashSet();
                    var otherUserReportIds = otherUserReports.Select(r => r.Id).ToHashSet();
                    var returnedReportIds = result.Items.Select(x => x.Id).ToHashSet();

                    if (scenario.UserRole == "User")
                    {
                        // For User role, should only see their own reports
                        result.Items.Should().HaveCount(scenario.CurrentUserReportCount,
                            "For any user with User role, GetListAsync should return only fault reports where CreatedByUserId matches the current user's ID");
                        result.TotalCount.Should().Be(scenario.CurrentUserReportCount,
                            "TotalCount should reflect only the current user's reports for User role");
                        
                        // Verify all returned reports are from the current user
                        returnedReportIds.Should().BeSubsetOf(currentUserReportIds,
                            "All returned reports should belong to the current user when role is User");
                        
                        // Verify no reports from other users are included
                        returnedReportIds.Should().NotIntersectWith(otherUserReportIds,
                            "Reports from other users should not be visible to User role");
                    }
                    else // Admin role
                    {
                        // For Admin role, should see all reports
                        var expectedTotalCount = scenario.CurrentUserReportCount + scenario.OtherUserReportCount;
                        result.Items.Should().HaveCount(expectedTotalCount,
                            "For any admin user, GetListAsync should return all fault reports");
                        result.TotalCount.Should().Be(expectedTotalCount,
                            "TotalCount should reflect all fault reports in the system for Admin role");
                        
                        // Verify that both current user's and other user's reports are included
                        foreach (var reportId in currentUserReportIds)
                        {
                            returnedReportIds.Should().Contain(reportId,
                                "Admin should see current user's reports");
                        }
                        
                        foreach (var reportId in otherUserReportIds)
                        {
                            returnedReportIds.Should().Contain(reportId,
                                "Admin should see other users' reports");
                        }
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary role-based list visibility scenarios for property-based testing.
        /// Each scenario includes user role and report counts for different users.
        /// </summary>
        private static Arbitrary<RoleBasedListScenario> GenerateRoleBasedListScenario()
        {
            var userRoleGen = Gen.Elements("User", "Admin");

            // Generate report counts between 0 and 5 for variety
            var currentUserReportCountGen = Gen.Choose(0, 5);
            var otherUserReportCountGen = Gen.Choose(0, 5);

            var priorityGen = Gen.Elements(
                Domain.Enums.PriorityLevel.Low,
                Domain.Enums.PriorityLevel.Medium,
                Domain.Enums.PriorityLevel.High
            );

            var statusGen = Gen.Elements(
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed
            );

            return Arb.From(
                from userRole in userRoleGen
                from currentUserReportCount in currentUserReportCountGen
                from otherUserReportCount in otherUserReportCountGen
                from priority in priorityGen
                from status in statusGen
                select new RoleBasedListScenario
                {
                    UserRole = userRole,
                    CurrentUserReportCount = currentUserReportCount,
                    OtherUserReportCount = otherUserReportCount,
                    Priority = priority,
                    Status = status
                });
        }

        /// <summary>
        /// Represents a role-based list visibility test scenario.
        /// </summary>
        private class RoleBasedListScenario
        {
            public string UserRole { get; set; } = string.Empty;
            public int CurrentUserReportCount { get; set; }
            public int OtherUserReportCount { get; set; }
            public Domain.Enums.PriorityLevel Priority { get; set; }
            public Domain.Enums.FaultReportStatus Status { get; set; }
        }

        // Feature: comprehensive-unit-test-suite, Property 11: Filter application
        // **Validates: Requirements 3.3, 3.4, 3.5**
        [Property(MaxTest = 100)]
        public Property GetListAsync_AppliesFiltersCorrectly()
        {
            return Prop.ForAll(
                GenerateFilterScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(
                        userId: userId, 
                        role: "Admin"); // Use Admin to see all reports
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Test User",
                        email: "test@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create a diverse set of fault reports with different attributes
                    var reports = new List<Domain.Entities.FaultReport>();
                    
                    // Create reports that match the filter criteria
                    for (int i = 0; i < scenario.MatchingReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Matching Report {i + 1}",
                            description: $"Description {i + 1}",
                            location: scenario.LocationFilter != null ? $"{scenario.LocationFilter} - Room {i + 1}" : $"Location {i + 1}",
                            priority: scenario.PriorityFilter ?? Domain.Enums.PriorityLevel.Medium,
                            status: scenario.StatusFilter ?? Domain.Enums.FaultReportStatus.New,
                            createdByUserId: userId);
                        reports.Add(report);
                    }

                    // Create reports that don't match the filter criteria
                    for (int i = 0; i < scenario.NonMatchingReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Non-Matching Report {i + 1}",
                            description: $"Description {i + 100}",
                            location: scenario.LocationFilter != null ? $"Different Location {i + 1}" : $"Location {i + 100}",
                            priority: scenario.PriorityFilter.HasValue ? GetDifferentPriority(scenario.PriorityFilter.Value) : Domain.Enums.PriorityLevel.Low,
                            status: scenario.StatusFilter.HasValue ? GetDifferentStatus(scenario.StatusFilter.Value) : Domain.Enums.FaultReportStatus.Reviewing,
                            createdByUserId: userId);
                        reports.Add(report);
                    }

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.AddRange(reports);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
                    {
                        Status = scenario.StatusFilter?.ToString(),
                        Priority = scenario.PriorityFilter?.ToString(),
                        Location = scenario.LocationFilter,
                        Page = 1,
                        PageSize = 100 // Large enough to get all reports
                    };

                    // Act
                    var result = await sut.GetListAsync(query, CancellationToken.None);

                    // Assert
                    result.Should().NotBeNull("GetListAsync should return a PagedResult");
                    
                    // Verify that the correct number of matching reports are returned
                    result.Items.Should().HaveCount(scenario.MatchingReportCount,
                        "For any GetListAsync call with filters, the returned results should only include fault reports matching all specified filter criteria");
                    result.TotalCount.Should().Be(scenario.MatchingReportCount,
                        "TotalCount should reflect only the filtered reports");

                    // Verify status filter is applied correctly
                    if (scenario.StatusFilter.HasValue)
                    {
                        result.Items.Should().OnlyContain(x => x.Status == scenario.StatusFilter.Value.ToString(),
                            "For any GetListAsync call with a status filter, only fault reports matching that status should be returned (Requirement 3.3)");
                    }

                    // Verify priority filter is applied correctly
                    if (scenario.PriorityFilter.HasValue)
                    {
                        result.Items.Should().OnlyContain(x => x.Priority == scenario.PriorityFilter.Value.ToString(),
                            "For any GetListAsync call with a priority filter, only fault reports matching that priority should be returned (Requirement 3.4)");
                    }

                    // Verify location filter is applied correctly (substring match)
                    if (scenario.LocationFilter != null)
                    {
                        result.Items.Should().OnlyContain(x => x.Location.Contains(scenario.LocationFilter, StringComparison.OrdinalIgnoreCase),
                            "For any GetListAsync call with a location filter, only fault reports containing that location substring should be returned (Requirement 3.5)");
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary filter scenarios for property-based testing.
        /// Each scenario includes optional status, priority, and location filters.
        /// </summary>
        private static Arbitrary<FilterScenario> GenerateFilterScenario()
        {
            // Generate scenarios with different filter combinations
            var filterTypeGen = Gen.Choose(1, 7); // 7 combinations: none, status, priority, location, status+priority, status+location, priority+location, all

            var statusGen = Gen.Elements(
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed
            );

            var priorityGen = Gen.Elements(
                Domain.Enums.PriorityLevel.Low,
                Domain.Enums.PriorityLevel.Medium,
                Domain.Enums.PriorityLevel.High
            );

            var locationGen = Gen.Elements(
                "Building A",
                "Building B",
                "Building C",
                "Data Center",
                "Server Room"
            );

            // Generate report counts between 1 and 5 for variety
            var matchingCountGen = Gen.Choose(1, 5);
            var nonMatchingCountGen = Gen.Choose(1, 5);

            return Arb.From(
                from filterType in filterTypeGen
                from status in statusGen
                from priority in priorityGen
                from location in locationGen
                from matchingCount in matchingCountGen
                from nonMatchingCount in nonMatchingCountGen
                select new FilterScenario
                {
                    StatusFilter = filterType == 1 || filterType == 4 || filterType == 5 || filterType == 7 ? status : null,
                    PriorityFilter = filterType == 2 || filterType == 4 || filterType == 6 || filterType == 7 ? priority : null,
                    LocationFilter = filterType == 3 || filterType == 5 || filterType == 6 || filterType == 7 ? location : null,
                    MatchingReportCount = matchingCount,
                    NonMatchingReportCount = nonMatchingCount
                });
        }

        /// <summary>
        /// Represents a filter application test scenario.
        /// </summary>
        private class FilterScenario
        {
            public Domain.Enums.FaultReportStatus? StatusFilter { get; set; }
            public Domain.Enums.PriorityLevel? PriorityFilter { get; set; }
            public string? LocationFilter { get; set; }
            public int MatchingReportCount { get; set; }
            public int NonMatchingReportCount { get; set; }
        }

        /// <summary>
        /// Helper method to get a different priority than the specified one.
        /// </summary>
        private static Domain.Enums.PriorityLevel GetDifferentPriority(Domain.Enums.PriorityLevel priority)
        {
            return priority switch
            {
                Domain.Enums.PriorityLevel.Low => Domain.Enums.PriorityLevel.High,
                Domain.Enums.PriorityLevel.Medium => Domain.Enums.PriorityLevel.Low,
                Domain.Enums.PriorityLevel.High => Domain.Enums.PriorityLevel.Medium,
                _ => Domain.Enums.PriorityLevel.Medium
            };
        }

        /// <summary>
        /// Helper method to get a different status than the specified one.
        /// </summary>
        private static Domain.Enums.FaultReportStatus GetDifferentStatus(Domain.Enums.FaultReportStatus status)
        {
            return status switch
            {
                Domain.Enums.FaultReportStatus.New => Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Reviewing => Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.Assigned => Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.InProgress => Domain.Enums.FaultReportStatus.Completed,
                Domain.Enums.FaultReportStatus.Completed => Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Cancelled => Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.FalseAlarm => Domain.Enums.FaultReportStatus.New,
                _ => Domain.Enums.FaultReportStatus.Reviewing
            };
        }

        // Feature: comprehensive-unit-test-suite, Property 12: Pagination correctness
        // **Validates: Requirements 3.8, 3.11**
        [Property(MaxTest = 100)]
        public Property GetListAsync_PaginationIsCorrect()
        {
            return Prop.ForAll(
                GeneratePaginationScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(
                        userId: userId, 
                        role: "Admin"); // Use Admin to see all reports
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Test User",
                        email: "test@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create fault reports with sequential timestamps for consistent ordering
                    var reports = new List<Domain.Entities.FaultReport>();
                    for (int i = 0; i < scenario.TotalReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Report {i + 1}",
                            description: $"Description {i + 1}",
                            location: $"Building {i + 1}",
                            priority: Domain.Enums.PriorityLevel.Medium,
                            status: Domain.Enums.FaultReportStatus.New,
                            createdByUserId: userId,
                            createdAtUtc: DateTime.UtcNow.AddMinutes(-scenario.TotalReportCount + i)); // Sequential timestamps
                        reports.Add(report);
                    }

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.AddRange(reports);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
                    {
                        Page = scenario.Page,
                        PageSize = scenario.PageSize
                    };

                    // Act
                    var result = await sut.GetListAsync(query, CancellationToken.None);

                    // Assert
                    result.Should().NotBeNull("GetListAsync should return a PagedResult");

                    // Calculate expected values
                    var expectedTotalCount = scenario.TotalReportCount;
                    var expectedTotalPages = (int)Math.Ceiling((double)expectedTotalCount / scenario.PageSize);
                    var expectedItemCount = Math.Min(
                        scenario.PageSize,
                        Math.Max(0, expectedTotalCount - (scenario.Page - 1) * scenario.PageSize));

                    // Verify TotalCount is correct
                    result.TotalCount.Should().Be(expectedTotalCount,
                        "For any GetListAsync call with pagination parameters, the PagedResult should contain correct TotalCount (Requirement 3.11)");

                    // Verify TotalPages is correct
                    result.TotalPages.Should().Be(expectedTotalPages,
                        "For any GetListAsync call with pagination parameters, the PagedResult should contain correct TotalPages (Requirement 3.11)");

                    // Verify the correct page of results is returned
                    result.Items.Should().HaveCount(expectedItemCount,
                        "For any GetListAsync call with pagination parameters, the correct page of results should be returned (Requirement 3.8)");

                    // Verify Page and PageSize are set correctly in the result
                    result.Page.Should().Be(scenario.Page, "Page should match the requested page");
                    result.PageSize.Should().Be(scenario.PageSize, "PageSize should match the requested page size");

                    // If we're not on the last page and there are items, verify we got a full page
                    if (scenario.Page < expectedTotalPages && expectedItemCount > 0)
                    {
                        result.Items.Should().HaveCount(scenario.PageSize,
                            "Pages before the last page should contain PageSize items");
                    }

                    // Verify items are unique (no duplicates across pagination)
                    var itemIds = result.Items.Select(x => x.Id).ToList();
                    itemIds.Should().OnlyHaveUniqueItems("Pagination should not return duplicate items");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary pagination scenarios for property-based testing.
        /// Each scenario includes total report count, page number, and page size.
        /// </summary>
        private static Arbitrary<PaginationScenario> GeneratePaginationScenario()
        {
            // Generate total report counts between 5 and 50
            var totalCountGen = Gen.Choose(5, 50);
            
            // Generate page sizes between 5 and 20
            var pageSizeGen = Gen.Choose(5, 20);

            return Arb.From(
                from totalCount in totalCountGen
                from pageSize in pageSizeGen
                let maxPage = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize))
                from page in Gen.Choose(1, maxPage + 1) // Include one page beyond the last to test empty results
                select new PaginationScenario
                {
                    TotalReportCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
        }

        /// <summary>
        /// Represents a pagination test scenario.
        /// </summary>
        private class PaginationScenario
        {
            public int TotalReportCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }

        // Feature: comprehensive-unit-test-suite, Property 13: Sorting correctness
        // **Validates: Requirements 3.9, 3.10**
        [Property(MaxTest = 100)]
        public Property GetListAsync_SortingIsCorrect()
        {
            return Prop.ForAll(
                GenerateSortingScenario(),
                async scenario =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(
                        userId: userId, 
                        role: "Admin"); // Use Admin to see all reports
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Test User",
                        email: "test@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create fault reports with varying priorities and timestamps
                    var reports = new List<Domain.Entities.FaultReport>();
                    var priorityLevels = new[] { 
                        Domain.Enums.PriorityLevel.Low, 
                        Domain.Enums.PriorityLevel.Medium, 
                        Domain.Enums.PriorityLevel.High 
                    };
                    
                    for (int i = 0; i < scenario.ReportCount; i++)
                    {
                        var report = TestDataBuilder.CreateFaultReport(
                            id: Guid.NewGuid(),
                            title: $"Report {i + 1}",
                            description: $"Description {i + 1}",
                            location: $"Building {i + 1}",
                            priority: priorityLevels[i % priorityLevels.Length],
                            status: Domain.Enums.FaultReportStatus.New,
                            createdByUserId: userId,
                            createdAtUtc: DateTime.UtcNow.AddMinutes(-scenario.ReportCount + i)); // Sequential timestamps
                        reports.Add(report);
                    }

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.AddRange(reports);
                    await dbContext.SaveChangesAsync();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockStatusTransitionPolicy.Object);

                    var query = new Application.DTOs.FaultReports.GetFaultReportsQuery
                    {
                        SortBy = scenario.SortBy,
                        SortDirection = scenario.SortDirection,
                        Page = 1,
                        PageSize = scenario.ReportCount + 10 // Get all reports
                    };

                    // Act
                    var result = await sut.GetListAsync(query, CancellationToken.None);

                    // Assert
                    result.Should().NotBeNull("GetListAsync should return a PagedResult");
                    result.Items.Should().NotBeEmpty("Result should contain items");

                    // Verify sorting based on the scenario
                    if (scenario.SortBy == "priority")
                    {
                        var priorities = result.Items.Select(x => x.Priority).ToList();
                        
                        if (scenario.SortDirection == "asc")
                        {
                            // Verify ascending order: Low, Medium, High
                            for (int i = 0; i < priorities.Count - 1; i++)
                            {
                                var currentPriority = ParsePriority(priorities[i]);
                                var nextPriority = ParsePriority(priorities[i + 1]);
                                currentPriority.Should().BeLessThanOrEqualTo(nextPriority,
                                    "For any GetListAsync call with SortBy='priority' and SortDirection='asc', results should be ordered by priority ascending (Requirement 3.9)");
                            }
                        }
                        else // desc
                        {
                            // Verify descending order: High, Medium, Low
                            for (int i = 0; i < priorities.Count - 1; i++)
                            {
                                var currentPriority = ParsePriority(priorities[i]);
                                var nextPriority = ParsePriority(priorities[i + 1]);
                                currentPriority.Should().BeGreaterThanOrEqualTo(nextPriority,
                                    "For any GetListAsync call with SortBy='priority' and SortDirection='desc', results should be ordered by priority descending");
                            }
                        }
                    }
                    else if (scenario.SortBy == "createdat")
                    {
                        var createdDates = result.Items.Select(x => x.CreatedAtUtc).ToList();
                        
                        if (scenario.SortDirection == "asc")
                        {
                            createdDates.Should().BeInAscendingOrder(
                                "For any GetListAsync call with SortBy='createdat' and SortDirection='asc', results should be ordered by creation date ascending");
                        }
                        else // desc
                        {
                            createdDates.Should().BeInDescendingOrder(
                                "For any GetListAsync call with SortBy='createdat' and SortDirection='desc', results should be ordered by creation date descending (Requirement 3.10)");
                        }
                    }

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary sorting scenarios for property-based testing.
        /// Each scenario includes sort field, sort direction, and number of reports.
        /// </summary>
        private static Arbitrary<SortingScenario> GenerateSortingScenario()
        {
            var sortByGen = Gen.Elements("priority", "createdat");
            var sortDirectionGen = Gen.Elements("asc", "desc");
            var reportCountGen = Gen.Choose(5, 20); // Generate between 5 and 20 reports

            return Arb.From(
                from sortBy in sortByGen
                from sortDirection in sortDirectionGen
                from reportCount in reportCountGen
                select new SortingScenario
                {
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    ReportCount = reportCount
                });
        }

        /// <summary>
        /// Represents a sorting test scenario.
        /// </summary>
        private class SortingScenario
        {
            public string SortBy { get; set; } = string.Empty;
            public string SortDirection { get; set; } = string.Empty;
            public int ReportCount { get; set; }
        }

        /// <summary>
        /// Parses a priority string to a numeric value for comparison.
        /// Low = 0, Medium = 1, High = 2
        /// </summary>
        private static int ParsePriority(string priority)
        {
            return priority switch
            {
                "Low" => 0,
                "Medium" => 1,
                "High" => 2,
                _ => throw new ArgumentException($"Unknown priority: {priority}")
            };
        }

        #region ChangeStatusAsync Tests

        [Fact]
        public async Task ChangeStatusAsync_WhenAdminPerformsValidTransition_ShouldUpdateStatusAndTimestamp()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create a fault report with status New
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: adminUserId,
                updatedAtUtc: DateTime.UtcNow.AddHours(-1)); // Set to 1 hour ago

            dbContext.Users.Add(adminUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            // Configure the mock policy to allow the transition
            var mockPolicy = MockHelpers.CreateMockStatusTransitionPolicy(canTransition: true);

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockPolicy.Object);

            var request = TestDataBuilder.CreateStatusChangeRequest(status: "Reviewing");
            var beforeChange = DateTime.UtcNow;

            // Act
            await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

            var afterChange = DateTime.UtcNow;

            // Assert
            var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
            updatedReport.Should().NotBeNull("Fault report should exist in database");
            updatedReport!.Status.Should().Be(Domain.Enums.FaultReportStatus.Reviewing, 
                "Status should be updated to Reviewing");
            updatedReport.UpdatedAtUtc.Should().BeCloseTo(beforeChange, TimeSpan.FromSeconds(2), 
                "UpdatedAtUtc should be set to current UTC time");
            updatedReport.UpdatedAtUtc.Should().BeBefore(afterChange.AddSeconds(1));

            // Verify that CanTransition was invoked with correct parameters
            mockPolicy.Verify(
                x => x.CanTransition(
                    Domain.Enums.UserRole.Admin,
                    Domain.Enums.FaultReportStatus.New,
                    Domain.Enums.FaultReportStatus.Reviewing),
                Times.Once,
                "IFaultReportStatusTransitionPolicy.CanTransition should be invoked with correct parameters");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task ChangeStatusAsync_WhenInvalidTransition_ShouldThrowStatusTransitionException()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create a fault report with status New
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: adminUserId);

            dbContext.Users.Add(adminUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            // Configure the mock policy to reject the transition
            var mockPolicy = MockHelpers.CreateMockStatusTransitionPolicy(
                canTransition: false,
                validationMessage: "Status transition from 'New' to 'Assigned' is not allowed.");

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockPolicy.Object);

            var request = TestDataBuilder.CreateStatusChangeRequest(status: "Assigned");

            // Act
            Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<StatusTransitionException>()
                .WithMessage("*not allowed*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task ChangeStatusAsync_WhenNonAdminUser_ShouldThrowForbiddenException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the user
            var user = TestDataBuilder.CreateUser(
                id: userId,
                fullName: "Regular User",
                email: "user@example.com",
                role: Domain.Enums.UserRole.User);

            // Create a fault report owned by the user
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: userId);

            dbContext.Users.Add(user);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var request = TestDataBuilder.CreateStatusChangeRequest(status: "Reviewing");

            // Act
            Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("*admin*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task ChangeStatusAsync_WhenInvalidStatusString_ShouldThrowStatusTransitionException()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create a fault report
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.New,
                createdByUserId: adminUserId);

            dbContext.Users.Add(adminUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var request = TestDataBuilder.CreateStatusChangeRequest(status: "InvalidStatus");

            // Act
            Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<StatusTransitionException>()
                .WithMessage("*Invalid target status*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task ChangeStatusAsync_WhenNonExistentId_ShouldThrowNotFoundException()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockStatusTransitionPolicy.Object);

            var nonExistentId = Guid.NewGuid();
            var request = TestDataBuilder.CreateStatusChangeRequest(status: "Reviewing");

            // Act
            Func<Task> act = async () => await sut.ChangeStatusAsync(nonExistentId, request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("*not found*");

            // Cleanup
            dbContext.Dispose();
        }

        [Fact]
        public async Task ChangeStatusAsync_WhenTransitionFromTerminalState_ShouldThrowStatusTransitionException()
        {
            // Arrange
            var adminUserId = Guid.NewGuid();
            var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
            var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

            // Create the admin user
            var adminUser = TestDataBuilder.CreateUser(
                id: adminUserId,
                fullName: "Admin User",
                email: "admin@example.com",
                role: Domain.Enums.UserRole.Admin);

            // Create a fault report with terminal status (Completed)
            var faultReport = TestDataBuilder.CreateFaultReport(
                id: Guid.NewGuid(),
                title: "Test Fault Report",
                description: "Test Description",
                location: "Building A",
                priority: Domain.Enums.PriorityLevel.High,
                status: Domain.Enums.FaultReportStatus.Completed,
                createdByUserId: adminUserId);

            dbContext.Users.Add(adminUser);
            dbContext.FaultReports.Add(faultReport);
            await dbContext.SaveChangesAsync();

            // Configure the mock policy to reject the transition from terminal state
            var mockPolicy = MockHelpers.CreateMockStatusTransitionPolicy(
                canTransition: false,
                validationMessage: "Status transition from 'Completed' to 'New' is not allowed.");

            var sut = new FaultReportService(
                dbContext,
                mockCurrentUser.Object,
                mockPolicy.Object);

            var request = TestDataBuilder.CreateStatusChangeRequest(status: "New");

            // Act
            Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<StatusTransitionException>()
                .WithMessage("*not allowed*");

            // Cleanup
            dbContext.Dispose();
        }

        // Feature: comprehensive-unit-test-suite, Property 15: Valid status transitions for admins
        // **Validates: Requirements 5.1**
        [Property(MaxTest = 100)]
        public Property ChangeStatusAsync_AdminValidTransitions_ShouldSucceed()
        {
            return Prop.ForAll(
                GenerateValidStatusTransition(),
                async transition =>
                {
                    // Arrange
                    var adminUserId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the admin user
                    var adminUser = TestDataBuilder.CreateUser(
                        id: adminUserId,
                        fullName: "Admin User",
                        email: "admin@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create a fault report with the source status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Test Fault Report",
                        description: "Test Description",
                        location: "Building A",
                        priority: Domain.Enums.PriorityLevel.High,
                        status: transition.FromStatus,
                        createdByUserId: adminUserId);

                    dbContext.Users.Add(adminUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    // Use the real policy to test actual valid transitions
                    var realPolicy = new FaultReportStatusTransitionPolicy();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        realPolicy);

                    var request = TestDataBuilder.CreateStatusChangeRequest(status: transition.ToStatus.ToString());

                    // Act
                    await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

                    // Assert
                    var updatedReport = await dbContext.FaultReports.FindAsync(faultReport.Id);
                    updatedReport.Should().NotBeNull("Fault report should exist in database");
                    updatedReport!.Status.Should().Be(transition.ToStatus,
                        "For any admin user requesting a status transition that is allowed by the policy, ChangeStatusAsync should complete successfully and update the status (Requirement 5.1)");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 16: Invalid status transition rejection
        // **Validates: Requirements 5.2**
        [Property(MaxTest = 100)]
        public Property ChangeStatusAsync_InvalidTransitions_ShouldThrowException()
        {
            return Prop.ForAll(
                GenerateInvalidStatusTransition(),
                async transition =>
                {
                    // Arrange
                    var adminUserId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the admin user
                    var adminUser = TestDataBuilder.CreateUser(
                        id: adminUserId,
                        fullName: "Admin User",
                        email: "admin@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create a fault report with the source status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Test Fault Report",
                        description: "Test Description",
                        location: "Building A",
                        priority: Domain.Enums.PriorityLevel.High,
                        status: transition.FromStatus,
                        createdByUserId: adminUserId);

                    dbContext.Users.Add(adminUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    // Use the real policy to test actual invalid transitions
                    var realPolicy = new FaultReportStatusTransitionPolicy();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        realPolicy);

                    var request = TestDataBuilder.CreateStatusChangeRequest(status: transition.ToStatus.ToString());

                    // Act
                    Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

                    // Assert
                    await act.Should().ThrowAsync<StatusTransitionException>(
                        "For any status transition that is not allowed by the policy, ChangeStatusAsync should throw StatusTransitionException (Requirement 5.2)");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 17: Status change admin-only access
        // **Validates: Requirements 5.3**
        [Property(MaxTest = 100)]
        public Property ChangeStatusAsync_NonAdminUser_ShouldAlwaysThrowForbiddenException()
        {
            return Prop.ForAll(
                GenerateAnyStatusTransition(),
                async transition =>
                {
                    // Arrange
                    var userId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: userId, role: "User");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the user
                    var user = TestDataBuilder.CreateUser(
                        id: userId,
                        fullName: "Regular User",
                        email: "user@example.com",
                        role: Domain.Enums.UserRole.User);

                    // Create a fault report with the source status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Test Fault Report",
                        description: "Test Description",
                        location: "Building A",
                        priority: Domain.Enums.PriorityLevel.High,
                        status: transition.FromStatus,
                        createdByUserId: userId);

                    dbContext.Users.Add(user);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    // Use the real policy
                    var realPolicy = new FaultReportStatusTransitionPolicy();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        realPolicy);

                    var request = TestDataBuilder.CreateStatusChangeRequest(status: transition.ToStatus.ToString());

                    // Act
                    Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

                    // Assert
                    await act.Should().ThrowAsync<ForbiddenException>(
                        "For any non-admin user, ChangeStatusAsync should throw ForbiddenException regardless of the requested transition (Requirement 5.3)");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 18: Terminal state immutability
        // **Validates: Requirements 5.6**
        [Property(MaxTest = 100)]
        public Property ChangeStatusAsync_FromTerminalState_ShouldAlwaysThrowException()
        {
            return Prop.ForAll(
                GenerateTerminalStateTransition(),
                async transition =>
                {
                    // Arrange
                    var adminUserId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the admin user
                    var adminUser = TestDataBuilder.CreateUser(
                        id: adminUserId,
                        fullName: "Admin User",
                        email: "admin@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create a fault report with a terminal status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Test Fault Report",
                        description: "Test Description",
                        location: "Building A",
                        priority: Domain.Enums.PriorityLevel.High,
                        status: transition.FromStatus,
                        createdByUserId: adminUserId);

                    dbContext.Users.Add(adminUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    // Use the real policy
                    var realPolicy = new FaultReportStatusTransitionPolicy();

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        realPolicy);

                    var request = TestDataBuilder.CreateStatusChangeRequest(status: transition.ToStatus.ToString());

                    // Act
                    Func<Task> act = async () => await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

                    // Assert
                    await act.Should().ThrowAsync<StatusTransitionException>(
                        "For any fault report in a terminal state (Completed, Cancelled, FalseAlarm), attempting to transition to any other status should throw StatusTransitionException (Requirement 5.6)");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        // Feature: comprehensive-unit-test-suite, Property 19: Status transition policy invocation
        // **Validates: Requirements 5.8**
        [Property(MaxTest = 100)]
        public Property ChangeStatusAsync_ShouldInvokeCanTransitionWithCorrectParameters()
        {
            return Prop.ForAll(
                GenerateAnyStatusTransition(),
                async transition =>
                {
                    // Arrange
                    var adminUserId = Guid.NewGuid();
                    var mockCurrentUser = MockHelpers.CreateMockCurrentUserService(userId: adminUserId, role: "Admin");
                    var dbContext = MockDbContextFactory.CreateInMemoryDbContext();

                    // Create the admin user
                    var adminUser = TestDataBuilder.CreateUser(
                        id: adminUserId,
                        fullName: "Admin User",
                        email: "admin@example.com",
                        role: Domain.Enums.UserRole.Admin);

                    // Create a fault report with the source status
                    var faultReport = TestDataBuilder.CreateFaultReport(
                        id: Guid.NewGuid(),
                        title: "Test Fault Report",
                        description: "Test Description",
                        location: "Building A",
                        priority: Domain.Enums.PriorityLevel.High,
                        status: transition.FromStatus,
                        createdByUserId: adminUserId);

                    dbContext.Users.Add(adminUser);
                    dbContext.FaultReports.Add(faultReport);
                    await dbContext.SaveChangesAsync();

                    // Use a mock policy to verify invocation
                    var mockPolicy = MockHelpers.CreateMockStatusTransitionPolicy(canTransition: true);

                    var sut = new FaultReportService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockPolicy.Object);

                    var request = TestDataBuilder.CreateStatusChangeRequest(status: transition.ToStatus.ToString());

                    // Act
                    await sut.ChangeStatusAsync(faultReport.Id, request, CancellationToken.None);

                    // Assert
                    mockPolicy.Verify(
                        x => x.CanTransition(
                            Domain.Enums.UserRole.Admin,
                            transition.FromStatus,
                            transition.ToStatus),
                        Times.Once,
                        "For any ChangeStatusAsync call, the IFaultReportStatusTransitionPolicy.CanTransition method should be invoked with the correct UserRole, current status, and target status (Requirement 5.8)");

                    // Cleanup
                    dbContext.Dispose();
                });
        }

        /// <summary>
        /// Generates arbitrary valid status transitions for property-based testing.
        /// Valid transitions are based on the FaultReportStatusTransitionPolicy rules.
        /// </summary>
        private static Arbitrary<StatusTransition> GenerateValidStatusTransition()
        {
            var validTransitions = new List<StatusTransition>
            {
                // From New
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.Reviewing },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.Cancelled },
                
                // From Reviewing
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.Assigned },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.FalseAlarm },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.Cancelled },
                
                // From Assigned
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.InProgress },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.Cancelled },
                
                // From InProgress
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.Completed },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.Cancelled }
            };

            return Arb.From(Gen.Elements(validTransitions.ToArray()));
        }

        /// <summary>
        /// Generates arbitrary invalid status transitions for property-based testing.
        /// Invalid transitions are those not allowed by the FaultReportStatusTransitionPolicy.
        /// </summary>
        private static Arbitrary<StatusTransition> GenerateInvalidStatusTransition()
        {
            var invalidTransitions = new List<StatusTransition>
            {
                // Invalid from New
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.Assigned },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.InProgress },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.Completed },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.New, ToStatus = Domain.Enums.FaultReportStatus.FalseAlarm },
                
                // Invalid from Reviewing
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.New },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.InProgress },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Reviewing, ToStatus = Domain.Enums.FaultReportStatus.Completed },
                
                // Invalid from Assigned
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.New },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.Reviewing },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.Completed },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Assigned, ToStatus = Domain.Enums.FaultReportStatus.FalseAlarm },
                
                // Invalid from InProgress
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.New },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.Reviewing },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.Assigned },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.InProgress, ToStatus = Domain.Enums.FaultReportStatus.FalseAlarm },
                
                // All transitions from terminal states
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Completed, ToStatus = Domain.Enums.FaultReportStatus.New },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.Cancelled, ToStatus = Domain.Enums.FaultReportStatus.New },
                new StatusTransition { FromStatus = Domain.Enums.FaultReportStatus.FalseAlarm, ToStatus = Domain.Enums.FaultReportStatus.New }
            };

            return Arb.From(Gen.Elements(invalidTransitions.ToArray()));
        }

        /// <summary>
        /// Generates arbitrary status transitions (both valid and invalid) for property-based testing.
        /// </summary>
        private static Arbitrary<StatusTransition> GenerateAnyStatusTransition()
        {
            var allStatuses = new[]
            {
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed,
                Domain.Enums.FaultReportStatus.Cancelled,
                Domain.Enums.FaultReportStatus.FalseAlarm
            };

            var fromStatusGen = Gen.Elements(allStatuses);
            var toStatusGen = Gen.Elements(allStatuses);

            return Arb.From(
                from fromStatus in fromStatusGen
                from toStatus in toStatusGen
                select new StatusTransition { FromStatus = fromStatus, ToStatus = toStatus });
        }

        /// <summary>
        /// Generates arbitrary transitions from terminal states for property-based testing.
        /// Terminal states are: Completed, Cancelled, FalseAlarm
        /// </summary>
        private static Arbitrary<StatusTransition> GenerateTerminalStateTransition()
        {
            var terminalStatuses = new[]
            {
                Domain.Enums.FaultReportStatus.Completed,
                Domain.Enums.FaultReportStatus.Cancelled,
                Domain.Enums.FaultReportStatus.FalseAlarm
            };

            var allStatuses = new[]
            {
                Domain.Enums.FaultReportStatus.New,
                Domain.Enums.FaultReportStatus.Reviewing,
                Domain.Enums.FaultReportStatus.Assigned,
                Domain.Enums.FaultReportStatus.InProgress,
                Domain.Enums.FaultReportStatus.Completed,
                Domain.Enums.FaultReportStatus.Cancelled,
                Domain.Enums.FaultReportStatus.FalseAlarm
            };

            var fromStatusGen = Gen.Elements(terminalStatuses);
            var toStatusGen = Gen.Elements(allStatuses);

            return Arb.From(
                from fromStatus in fromStatusGen
                from toStatus in toStatusGen
                select new StatusTransition { FromStatus = fromStatus, ToStatus = toStatus });
        }

        /// <summary>
        /// Represents a status transition for property-based testing.
        /// </summary>
        private class StatusTransition
        {
            public Domain.Enums.FaultReportStatus FromStatus { get; set; }
            public Domain.Enums.FaultReportStatus ToStatus { get; set; }
        }

        #endregion
    }
}
