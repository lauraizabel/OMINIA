using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.WebApi.Configuration;

public sealed class DevelopmentAdminSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DevelopmentAdminOptions _options;
    private readonly ILogger<DevelopmentAdminSeeder> _logger;

    public DevelopmentAdminSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<DevelopmentAdminOptions> options,
        ILogger<DevelopmentAdminSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Password);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        if (await repository.GetByEmailAsync(_options.Email, cancellationToken) is not null)
        {
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = new User
        {
            Username = _options.Username.Trim(),
            Email = _options.Email.Trim(),
            Phone = _options.Phone.Trim(),
            Password = passwordHasher.HashPassword(_options.Password),
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        await repository.CreateAsync(admin, cancellationToken);
        _logger.LogInformation("Development administrator {AdminId} was created.", admin.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
