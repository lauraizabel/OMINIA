using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ambev.DeveloperEvaluation.Common.Security;

/// <summary>
/// Implementation of JWT (JSON Web Token) generator.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly byte[] _signingKey;
    private readonly JwtOptions _options;

    /// <summary>
    /// Initializes a new instance of the JWT token generator.
    /// </summary>
    /// <param name="options">Validated JWT settings for token generation.</param>
    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _signingKey = Encoding.UTF8.GetBytes(_options.SecretKey);
    }

    /// <summary>
    /// Generates a JWT token for a specific user.
    /// </summary>
    /// <param name="user">User for whom the token will be generated.</param>
    /// <returns>Valid JWT token as string.</returns>
    /// <remarks>
    /// The generated token includes the following claims:
    /// - NameIdentifier (User ID)
    /// - Name (Username)
    /// - Role (User role)
    /// 
    /// The token is valid for 8 hours from the moment of generation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when user or secret key is not provided.</exception>
    public string GenerateToken(IUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new[]
        {
           new Claim(ClaimTypes.NameIdentifier, user.Id),
           new Claim(ClaimTypes.Name, user.Username),
           new Claim(ClaimTypes.Role, user.Role)
       };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
