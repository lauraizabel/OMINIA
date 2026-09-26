using Ambev.DeveloperEvaluation.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Users.CreateUser;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class CreateUserMappings
{
    [MapperIgnoreTarget(nameof(User.Id))]
    [MapperIgnoreTarget(nameof(User.NormalizedEmail))]
    [MapperIgnoreTarget(nameof(User.Password))]
    [MapperIgnoreTarget(nameof(User.CreatedAt))]
    [MapperIgnoreTarget(nameof(User.UpdatedAt))]
    public static partial User ToEntity(this CreateUserCommand command);

    public static partial CreateUserResult ToCreateUserResult(this User user);
}
