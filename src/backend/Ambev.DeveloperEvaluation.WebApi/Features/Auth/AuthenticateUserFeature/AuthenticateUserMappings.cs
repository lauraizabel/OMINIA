using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Riok.Mapperly.Abstractions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class AuthenticateUserMappings
{
    public static partial AuthenticateUserCommand ToCommand(this AuthenticateUserRequest request);

    public static partial AuthenticateUserResponse ToResponse(this AuthenticateUserResult result);
}
