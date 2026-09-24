using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Ambev.DeveloperEvaluation.ORM.Security;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.ORM.Queries;
using Ambev.DeveloperEvaluation.ORM.HealthChecks;
using Ambev.DeveloperEvaluation.ORM.Observability;
using Ambev.DeveloperEvaluation.IoC.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICorrelationIdProvider, HttpCorrelationIdProvider>();
        builder.Services.AddScoped<ISaleDomainEventPublisher, StructuredLogSaleDomainEventPublisher>();
        builder.Services.AddHealthChecks()
            .AddCheck<PostgreSqlReadinessHealthCheck>("postgresql", tags: ["readiness"]);
    }
}
