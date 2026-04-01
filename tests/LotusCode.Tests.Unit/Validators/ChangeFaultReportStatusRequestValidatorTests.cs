using FluentAssertions;
using FluentValidation.TestHelper;
using FsCheck;
using FsCheck.Xunit;
using LotusCode.Application.DTOs.FaultReports;
using LotusCode.Application.Validators.FaultReports;

namespace LotusCode.Tests.Unit.Validators;

/// <summary>
/// Tests for ChangeFaultReportStatusRequestValidator that validates
/// status change request payloads.
/// </summary>
public class ChangeFaultReportStatusRequestValidatorTests
{
    private readonly ChangeFaultReportStatusRequestValidator _validator;

    public ChangeFaultReportStatusRequestValidatorTests()
    {
        _validator = new ChangeFaultReportStatusRequestValidator();
    }

    [Fact]
    public void Validate_WhenStatusValid_ShouldPassValidation()
    {
        // Arrange
        var request = new ChangeFaultReportStatusRequest
        {
            Status = "Reviewing"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenStatusEmpty_ShouldFailValidation()
    {
        // Arrange
        var request = new ChangeFaultReportStatusRequest
        {
            Status = string.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Status is required.");
    }

    [Fact]
    public void Validate_WhenStatusInvalid_ShouldFailValidation()
    {
        // Arrange
        var request = new ChangeFaultReportStatusRequest
        {
            Status = "InvalidStatus"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Invalid status value.");
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Reviewing")]
    [InlineData("Assigned")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("FalseAlarm")]
    public void Validate_WhenAllValidStatusValues_ShouldPassValidation(string status)
    {
        // Arrange
        var request = new ChangeFaultReportStatusRequest
        {
            Status = status
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    #region Property-Based Tests

    [Property(MaxTest = 100)]
    public Property Validate_AllValidStatusValues_ShouldAlwaysPassValidation()
    {
        // Feature: comprehensive-unit-test-suite, Property 24: All valid status values accepted
        // **Validates: Requirements 8.4**

        var validStatuses = new[]
        {
            "New",
            "Reviewing",
            "Assigned",
            "InProgress",
            "Completed",
            "Cancelled",
            "FalseAlarm"
        };

        return Prop.ForAll(
            Gen.Elements(validStatuses).ToArbitrary(),
            status =>
            {
                // Arrange
                var request = new ChangeFaultReportStatusRequest
                {
                    Status = status
                };

                // Act
                var result = _validator.TestValidate(request);

                // Assert
                return (!result.IsValid == false).ToProperty();
            });
    }

    #endregion
}
