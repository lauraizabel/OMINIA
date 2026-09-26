using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class RefreshSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlFixture _database;

    public RefreshSessionTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Rotation_stores_only_hashes_and_reuse_revokes_the_token_family()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);

        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);

        await using (var context = CreateContext())
        {
            var stored = await context.RefreshSessions.SingleAsync();
            Assert.NotEqual(issued.Token, stored.TokenHash);
            Assert.DoesNotContain(issued.Token, context.RefreshSessions.Select(session => session.TokenHash));
        }

        RotatedRefreshToken rotated;
        await using (var context = CreateContext())
            rotated = await Service(context, clock).RotateAsync(issued.Token);
        Assert.NotEqual(issued.Token, rotated.Token);

        clock.Advance(TimeSpan.FromSeconds(11));
        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(issued.Token));

        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(rotated.Token));

        await using var verification = CreateContext();
        Assert.Equal(2, await verification.RefreshSessions.CountAsync());
        Assert.All(
            await verification.RefreshSessions.ToListAsync(),
            session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task Refresh_rejects_inactive_users_and_revokes_their_session()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);
        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);

        await using (var context = CreateContext())
        {
            var user = await context.Users.SingleAsync();
            user.Status = UserStatus.Inactive;
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(issued.Token));

        await using var verification = CreateContext();
        Assert.Equal("UserUnavailable", (await verification.RefreshSessions.SingleAsync()).RevocationReason);
    }

    [Fact]
    public async Task Logout_revokes_the_complete_device_session_family()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);
        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);
        RotatedRefreshToken rotated;
        await using (var context = CreateContext())
            rotated = await Service(context, clock).RotateAsync(issued.Token);

        await using (var context = CreateContext())
            await Service(context, clock).RevokeAsync(rotated.Token);

        await using var verification = CreateContext();
        Assert.All(
            await verification.RefreshSessions.ToListAsync(),
            session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task Reuse_inside_the_grace_window_rejects_the_old_token_without_revoking_its_replacement()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);
        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);

        RotatedRefreshToken rotated;
        await using (var context = CreateContext())
            rotated = await Service(context, clock).RotateAsync(issued.Token);

        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(issued.Token));

        await using (var context = CreateContext())
            await Service(context, clock).RotateAsync(rotated.Token);
    }

    [Fact]
    public async Task Expired_and_unknown_tokens_are_rejected_and_logout_is_idempotent()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);
        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);

        clock.Advance(TimeSpan.FromDays(8));
        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(issued.Token));

        await using (var context = CreateContext())
        {
            var service = Service(context, clock);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.RotateAsync("unknown-refresh-token"));
            await service.RevokeAsync("unknown-refresh-token");
        }

        await using var verification = CreateContext();
        var expired = await verification.RefreshSessions.SingleAsync();
        Assert.Equal("Expired", expired.RevocationReason);
    }

    [Fact]
    public async Task A_logged_out_token_cannot_be_rotated_again()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        var clock = new MutableTimeProvider(Now);
        IssuedRefreshToken issued;
        await using (var context = CreateContext())
            issued = await Service(context, clock).IssueAsync(userId);

        await using (var context = CreateContext())
            await Service(context, clock).RevokeAsync(issued.Token);

        await using (var context = CreateContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => Service(context, clock).RotateAsync(issued.Token));
    }

    [Fact]
    public async Task Cleanup_removes_only_families_past_absolute_expiration_and_retention_in_bounded_batches()
    {
        await ResetDatabaseAsync();
        var userId = await SeedUserAsync(UserStatus.Active);
        await using (var context = CreateContext())
        {
            context.RefreshSessions.AddRange(
                Session(userId, Now.AddDays(-9)),
                Session(userId, Now.AddDays(-8)),
                Session(userId, Now.AddDays(-7)),
                Session(userId, Now.AddDays(1), Now.AddDays(-30)));
            await context.SaveChangesAsync();
        }

        var options = Options.Create(new RefreshSessionOptions
        {
            CleanupRetentionDays = 7,
            CleanupBatchSize = 1,
            CleanupMaxBatchesPerCycle = 2
        });
        var services = new ServiceCollection()
            .AddScoped(_ => CreateContext())
            .AddScoped<RefreshSessionCleanup>()
            .AddSingleton<TimeProvider>(new MutableTimeProvider(Now))
            .AddSingleton<IOptions<RefreshSessionOptions>>(options)
            .BuildServiceProvider();
        await using var provider = services;
        var worker = new RefreshSessionCleanupWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<RefreshSessionCleanupWorker>.Instance);

        Assert.Equal(2, await worker.RunCycleAsync());
        Assert.Equal(0, await worker.RunCycleAsync());

        await using var verification = CreateContext();
        var remaining = await verification.RefreshSessions
            .OrderBy(session => session.AbsoluteExpiresAt)
            .ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, session => session.AbsoluteExpiresAt == Now.AddDays(-7));
        Assert.Contains(remaining, session => session.RevokedAt == Now.AddDays(-30));
    }

    private RefreshSessionService Service(DefaultContext context, TimeProvider clock) =>
        new(context, clock, Options.Create(new RefreshSessionOptions()));

    private DefaultContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(
                _database.ConnectionString,
                provider => provider.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName))
            .Options;
        return new DefaultContext(options);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private async Task<Guid> SeedUserAsync(UserStatus status)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "Session User",
            Email = "session@example.test",
            NormalizedEmail = "SESSION@EXAMPLE.TEST",
            Phone = "+5511999999999",
            Password = "not-used-by-session-tests",
            Role = UserRole.Manager,
            Status = status
        };
        await using var context = CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static RefreshSession Session(
        Guid userId,
        DateTimeOffset absoluteExpiration,
        DateTimeOffset? revokedAt = null)
    {
        var session = RefreshSession.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadRight(64, '0'),
            Now.AddDays(-40),
            absoluteExpiration,
            absoluteExpiration);
        if (revokedAt is not null)
            session.Revoke(revokedAt.Value, "Test");
        return session;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
