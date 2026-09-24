namespace Ambev.DeveloperEvaluation.Common.Security;

public interface IAuthenticatedUserStatusValidator
{
    Task<bool> IsActiveAsync(string userId, CancellationToken cancellationToken);
}
