using Ambev.DeveloperEvaluation.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Users.GetUser;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class GetUserMappings
{
    [MapProperty(nameof(User.Username), nameof(GetUserResult.Name))]
    public static partial GetUserResult ToGetUserResult(this User user);
}
