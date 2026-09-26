using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Ambev.DeveloperEvaluation.ORM.Security;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.ORM.Queries;
using Ambev.DeveloperEvaluation.ORM.HealthChecks;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.IoC.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<ISaleReadService, SaleReadService>();
        builder.Services.AddScoped<IAuthenticatedUserStatusValidator, AuthenticatedUserStatusValidator>();
        builder.Services.AddScoped<IRefreshSessionService, RefreshSessionService>();
        builder.Services.AddOptions<RefreshSessionOptions>()
            .Bind(builder.Configuration.GetSection(RefreshSessionOptions.SectionName))
            .Validate(options => options.IdleExpirationDays is > 0 and <= 30, "Refresh-session idle expiration must be between 1 and 30 days.")
            .Validate(options => options.AbsoluteExpirationDays is > 0 and <= 90, "Refresh-session absolute expiration must be between 1 and 90 days.")
            .Validate(options => options.AbsoluteExpirationDays >= options.IdleExpirationDays, "Refresh-session absolute expiration cannot be shorter than idle expiration.")
            .Validate(options => options.TokenSizeBytes is >= 32 and <= 128, "Refresh-session tokens must contain between 32 and 128 random bytes.")
            .Validate(options => options.ReuseGraceSeconds is >= 0 and <= 60, "Refresh-session reuse grace must be between 0 and 60 seconds.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Refresh-session cookie name is required.")
            .Validate(options => !options.SecureCookie || options.CookieName.StartsWith("__Host-", StringComparison.Ordinal), "Secure refresh-session cookies must use the __Host- prefix.")
            .ValidateOnStart();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICorrelationIdProvider, HttpCorrelationIdProvider>();
        builder.Services.AddScoped<IOutboxAdministration, OutboxAdministration>();
        builder.Services.AddOptions<OutboxOptions>()
            .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
            .Validate(options => options.BatchSize is > 0 and <= 500, "Outbox batch size must be between 1 and 500.")
            .Validate(options => options.PollIntervalSeconds > 0, "Outbox poll interval must be positive.")
            .Validate(options => options.LeaseSeconds > 0, "Outbox lease must be positive.")
            .Validate(options => options.MaxAttempts > 0, "Outbox maximum attempts must be positive.")
            .Validate(options => options.ProcessedRetentionDays > 0, "Outbox retention must be positive.")
            .Validate(options => options.CleanupIntervalMinutes > 0, "Outbox cleanup interval must be positive.")
            .ValidateOnStart();

        var mongoAudit = builder.Configuration
            .GetSection(MongoAuditOptions.SectionName)
            .Get<MongoAuditOptions>() ?? new MongoAuditOptions();
        builder.Services.AddOptions<MongoAuditOptions>()
            .Bind(builder.Configuration.GetSection(MongoAuditOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MongoAudit connection string is required when auditing is enabled.")
            .ValidateOnStart();

        var healthChecks = builder.Services.AddHealthChecks()
            .AddCheck<PostgreSqlReadinessHealthCheck>("postgresql", tags: ["readiness"])
            .AddCheck<OutboxHealthCheck>("outbox", tags: ["diagnostics"]);

        if (mongoAudit.Enabled)
        {
            builder.Services.AddSingleton<IMongoClient>(_ =>
            {
                var settings = MongoClientSettings.FromConnectionString(mongoAudit.ConnectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
                return new MongoClient(settings);
            });
            builder.Services.AddSingleton<ISaleDomainEventPublisher, MongoSaleDomainEventPublisher>();
            builder.Services.AddScoped<OutboxProcessor>();
            builder.Services.AddHostedService<OutboxWorker>();
            healthChecks.AddCheck<MongoAuditHealthCheck>("mongodb-audit", tags: ["diagnostics"]);
        }
    }
}
