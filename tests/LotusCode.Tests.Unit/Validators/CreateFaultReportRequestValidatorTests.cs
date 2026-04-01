using FluentAssertions;
using FluentValidation.TestHelper;
using LotusCode.Application.DTOs.FaultReports;
using LotusCode.Application.Validators.FaultReports;

namespace LotusCode.Tests.Unit.Validators;

/// <summary>
/// Tests for CreateFaultReportRequestValidator that validates
/// fault report creation request payloads.
/// </summary>
public class CreateFaultReportRequestValidatorTests
{
    private readonly CreateFaultReportRequestValidator _validator;

    public CreateFaultReportRequestValidatorTests()
    {
        _validator = new CreateFaultReportRequestValidator();
    }

    [Fact]
    public void Validate_WhenAllFieldsValid_ShouldPassValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenTitleEmpty_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = string.Empty,
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required.");
    }

    [Fact]
    public void Validate_WhenTitleExceeds200Characters_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = new string('A', 201),
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 200 characters.");
    }

    [Fact]
    public void Validate_WhenDescriptionEmpty_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = string.Empty,
            Location = "Building A, Floor 3",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description is required.");
    }

    [Fact]
    public void Validate_WhenDescriptionExceeds2000Characters_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = new string('A', 2001),
            Location = "Building A, Floor 3",
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 2000 characters.");
    }

    [Fact]
    public void Validate_WhenLocationEmpty_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = string.Empty,
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Location)
            .WithErrorMessage("Location is required.");
    }

    [Fact]
    public void Validate_WhenLocationExceeds300Characters_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = new string('A', 301),
            Priority = "Medium"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Location)
            .WithErrorMessage("Location cannot exceed 300 characters.");
    }

    [Fact]
    public void Validate_WhenPriorityEmpty_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = string.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority is required.");
    }

    [Fact]
    public void Validate_WhenPriorityInvalid_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = "InvalidPriority"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be one of the following values: Low, Medium, High.");
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("low")]
    [InlineData("LOW")]
    [InlineData("Medium")]
    [InlineData("medium")]
    [InlineData("MEDIUM")]
    [InlineData("High")]
    [InlineData("high")]
    [InlineData("HIGH")]
    public void Validate_WhenPriorityDifferentCase_ShouldPassValidation(string priority)
    {
        // Arrange
        var request = new CreateFaultReportRequest
        {
            Title = "Test Fault Report",
            Description = "This is a test description",
            Location = "Building A, Floor 3",
            Priority = priority
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }
}
