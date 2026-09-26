using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Riok.Mapperly.Abstractions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Users.CreateUser;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class CreateUserMappings
{
    public static partial CreateUserCommand ToCommand(this CreateUserRequest request);

    [MapperIgnoreTarget(nameof(CreateUserResponse.Name))]
    [MapperIgnoreTarget(nameof(CreateUserResponse.Email))]
    [MapperIgnoreTarget(nameof(CreateUserResponse.Phone))]
    [MapperIgnoreTarget(nameof(CreateUserResponse.Role))]
    [MapperIgnoreTarget(nameof(CreateUserResponse.Status))]
    public static partial CreateUserResponse ToResponse(this CreateUserResult result);
}
