using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Riok.Mapperly.Abstractions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class GetUserMappings
{
    public static partial GetUserResponse ToResponse(this GetUserResult result);
}
