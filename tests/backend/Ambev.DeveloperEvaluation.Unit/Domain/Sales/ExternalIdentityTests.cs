using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

public sealed class ExternalIdentityTests
{
    [Fact]
    public void Create_ShouldTrimValues()
    {
        var identity = ExternalIdentity.Create(" CUSTOMER-001 ", " Example Customer ");

        identity.ExternalId.Should().Be("CUSTOMER-001");
        identity.Name.Should().Be("Example Customer");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CONTROL\nCHARACTER")]
    public void Create_ShouldRejectInvalidExternalId(string? externalId)
    {
        var action = () => ExternalIdentity.Create(externalId, "Valid name");

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be("ExternalIdentity.InvalidId");
    }

    [Fact]
    public void Create_ShouldRejectValuesAboveMaximumLength()
    {
        var action = () => ExternalIdentity.Create(
            new string('A', ExternalIdentity.ExternalIdMaximumLength + 1),
            "Valid name");

        action.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CONTROL\nCHARACTER")]
    public void Create_ShouldRejectInvalidName(string? name)
    {
        var action = () => ExternalIdentity.Create("VALID-ID", name);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be("ExternalIdentity.InvalidName");
    }
}
