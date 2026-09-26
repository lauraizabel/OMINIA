using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Validation;

public sealed class ApplicationValidationPipelineTests
{
    [Fact]
    public void Application_scan_registers_user_and_auth_validators()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssembly(typeof(ApplicationLayer).Assembly);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<CreateUserCommandValidator>(
            provider.GetRequiredService<IValidator<CreateUserCommand>>());
        Assert.IsType<GetUserValidator>(
            provider.GetRequiredService<IValidator<GetUserCommand>>());
        Assert.IsType<DeleteUserValidator>(
            provider.GetRequiredService<IValidator<DeleteUserCommand>>());
        Assert.IsType<AuthenticateUserValidator>(
            provider.GetRequiredService<IValidator<AuthenticateUserCommand>>());
    }

    [Fact]
    public async Task Validation_behavior_rejects_invalid_user_and_auth_requests_before_the_handler()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssembly(typeof(ApplicationLayer).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        await using var provider = services.BuildServiceProvider();

        await AssertRejected<CreateUserCommand, CreateUserResult>(
            provider, new CreateUserCommand());
        await AssertRejected<GetUserCommand, GetUserResult>(
            provider, new GetUserCommand(Guid.Empty));
        await AssertRejected<DeleteUserCommand, DeleteUserResponse>(
            provider, new DeleteUserCommand(Guid.Empty));
        await AssertRejected<AuthenticateUserCommand, AuthenticateUserResult>(
            provider, new AuthenticateUserCommand());
    }

    private static async Task AssertRejected<TRequest, TResponse>(
        IServiceProvider provider,
        TRequest request)
        where TRequest : IRequest<TResponse>
    {
        var handlerCalled = false;
        var behavior = provider.GetRequiredService<IPipelineBehavior<TRequest, TResponse>>();

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            request,
            () =>
            {
                handlerCalled = true;
                return Task.FromResult(default(TResponse)!);
            },
            CancellationToken.None));

        Assert.False(handlerCalled);
    }
}
