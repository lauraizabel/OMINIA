using System.IdentityModel.Tokens.Jwt;
using Ambev.DeveloperEvaluation.Common.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Common.Security;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_ShouldIncludeConfiguredTrustBoundaryAndIdentityClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            SecretKey = "unit-tests-signing-key-at-least-32-bytes-long",
            Issuer = "UnitTests",
            Audience = "UnitTests.Client",
            ExpirationMinutes = 30
        });
        var generator = new JwtTokenGenerator(options);
        var user = new TestUser(Guid.NewGuid().ToString(), "Test User", "Manager");
        var beforeGeneration = DateTime.UtcNow;

        var encodedToken = generator.GenerateToken(user);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(encodedToken);
        Assert.Equal("UnitTests", token.Issuer);
        Assert.Contains("UnitTests.Client", token.Audiences);
        Assert.Equal(user.Id, token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.NameId).Value);
        Assert.Equal(user.Username, token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal(user.Role, token.Claims.Single(claim => claim.Type == "role").Value);
        Assert.InRange(token.ValidTo, beforeGeneration.AddMinutes(29), beforeGeneration.AddMinutes(31));
    }

    private sealed record TestUser(string Id, string Username, string Role) : IUser;
}
