using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class GetUserHandlerTests
{
    [Fact(DisplayName = "Given an existing user When retrieving it Then maps every response field")]
    public async Task Handle_ExistingUser_MapsEveryResponseField()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "Ada Lovelace",
            Email = "ada@example.com",
            Phone = "+5511999999999",
            Role = UserRole.Manager,
            Status = UserStatus.Active
        };
        var repository = Substitute.For<IUserRepository>();
        repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new GetUserHandler(repository);

        var result = await handler.Handle(new GetUserCommand(user.Id), CancellationToken.None);

        result.Should().BeEquivalentTo(new GetUserResult
        {
            Id = user.Id,
            Name = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status
        });
    }
}
