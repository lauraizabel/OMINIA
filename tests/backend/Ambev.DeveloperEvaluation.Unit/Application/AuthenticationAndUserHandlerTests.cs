using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class AuthenticationAndUserHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwords = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokens = Substitute.For<IJwtTokenGenerator>();
    private readonly IRefreshSessionService _refreshSessions = Substitute.For<IRefreshSessionService>();

    [Fact]
    public async Task Authenticate_returns_identity_and_generated_token_for_active_user()
    {
        var user = User(UserStatus.Active);
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwords.VerifyPassword("valid-password", user.Password).Returns(true);
        _tokens.GenerateToken(user).Returns("signed-token");
        _refreshSessions.IssueAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new IssuedRefreshToken("refresh-token", DateTimeOffset.UtcNow.AddDays(7)));
        var handler = Handler();

        var result = await handler.Handle(
            new AuthenticateUserCommand { Email = user.Email, Password = "valid-password" },
            CancellationToken.None);

        result.Token.Should().Be("signed-token");
        result.Email.Should().Be(user.Email);
        result.Name.Should().Be(user.Username);
        result.Role.Should().Be(nameof(UserRole.Manager));
        result.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Authenticate_rejects_unknown_user_without_verifying_a_password()
    {
        _users.GetByEmailAsync("missing@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var handler = Handler();

        var action = () => handler.Handle(
            new AuthenticateUserCommand { Email = "missing@example.com", Password = "password" },
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
        _passwords.DidNotReceiveWithAnyArgs().VerifyPassword(default!, default!);
    }

    [Fact]
    public async Task Authenticate_rejects_an_invalid_password()
    {
        var user = User(UserStatus.Active);
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwords.VerifyPassword("wrong-password", user.Password).Returns(false);
        var handler = Handler();

        var action = () => handler.Handle(
            new AuthenticateUserCommand { Email = user.Email, Password = "wrong-password" },
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
        _tokens.DidNotReceiveWithAnyArgs().GenerateToken(default!);
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Suspended)]
    public async Task Authenticate_rejects_non_active_accounts(UserStatus status)
    {
        var user = User(status);
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwords.VerifyPassword(Arg.Any<string>(), user.Password).Returns(true);
        var handler = Handler();

        var action = () => handler.Handle(
            new AuthenticateUserCommand { Email = user.Email, Password = "password" },
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not active");
    }

    [Fact]
    public async Task Refresh_rotates_the_session_before_issuing_a_new_access_token()
    {
        var user = User(UserStatus.Active);
        _refreshSessions.RotateAsync("current-refresh", Arg.Any<CancellationToken>())
            .Returns(new RotatedRefreshToken(user, "next-refresh", DateTimeOffset.UtcNow.AddDays(7)));
        _tokens.GenerateToken(user).Returns("next-access");
        var handler = new RefreshUserSessionHandler(_refreshSessions, _tokens);

        var result = await handler.Handle(
            new RefreshUserSessionCommand("current-refresh"),
            CancellationToken.None);

        result.Token.Should().Be("next-access");
        result.RefreshToken.Should().Be("next-refresh");
        result.Email.Should().Be(user.Email);
        await _refreshSessions.Received(1).RotateAsync("current-refresh", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_user_reports_success_after_repository_deletion()
    {
        var id = Guid.NewGuid();
        _users.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new DeleteUserHandler(_users);

        var result = await handler.Handle(new DeleteUserCommand(id), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_user_returns_domain_not_found_for_unknown_identifier()
    {
        var id = Guid.NewGuid();
        _users.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(false);
        var handler = new DeleteUserHandler(_users);
        var action = () => handler.Handle(new DeleteUserCommand(id), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DomainNotFoundException>();
        exception.Which.Code.Should().Be(DomainErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task Get_user_returns_domain_not_found_for_unknown_identifier()
    {
        var handler = new GetUserHandler(_users);
        var id = Guid.NewGuid();
        _users.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);
        var exception = await FluentActions.Invoking(() => handler.Handle(
                new GetUserCommand(id), CancellationToken.None))
            .Should().ThrowAsync<DomainNotFoundException>();
        exception.Which.Code.Should().Be(DomainErrorCodes.User.NotFound);
    }

    [Theory]
    [InlineData("", "password", false)]
    [InlineData("invalid", "password", false)]
    [InlineData("user@example.com", "short", false)]
    [InlineData("user@example.com", "password", true)]
    public void Authentication_command_validation_matches_the_public_contract(
        string email,
        string password,
        bool expected)
    {
        var result = new AuthenticateUserValidator().Validate(
            new AuthenticateUserCommand { Email = email, Password = password });

        result.IsValid.Should().Be(expected);
    }

    private static User User(UserStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Username = "Sales Manager",
        Email = "manager@example.com",
        Password = TestPasswordHash(),
        Phone = "+5511999999999",
        Role = UserRole.Manager,
        Status = status
    };

    private static string TestPasswordHash() => string.Concat("test", '-', "hash");

    private AuthenticateUserHandler Handler() =>
        new(_users, _passwords, _tokens, _refreshSessions);
}
