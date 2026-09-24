using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

namespace Ambev.DeveloperEvaluation.Common.Security
{
    public static class AuthenticationExtension
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

            var jwtSection = configuration.GetSection(JwtOptions.SectionName);
            services.AddOptions<JwtOptions>()
                .Bind(jwtSection)
                .Validate(options => Encoding.UTF8.GetByteCount(options.SecretKey) >= 32, "Jwt:SecretKey must contain at least 32 bytes.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
                .Validate(options => options.ExpirationMinutes is > 0 and <= 1440, "Jwt:ExpirationMinutes must be between 1 and 1440.")
                .ValidateOnStart();

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((x, configuredOptions) =>
            {
                var jwtOptions = configuredOptions.Value;
                var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);
                x.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                x.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var statusValidator = context.HttpContext.RequestServices
                            .GetRequiredService<IAuthenticatedUserStatusValidator>();

                        if (string.IsNullOrWhiteSpace(userId) ||
                            !await statusValidator.IsActiveAsync(userId, context.HttpContext.RequestAborted))
                        {
                            context.Fail("The authenticated user is inactive or no longer exists.");
                        }
                    }
                };
            });

            return services;
        }
    }
}
