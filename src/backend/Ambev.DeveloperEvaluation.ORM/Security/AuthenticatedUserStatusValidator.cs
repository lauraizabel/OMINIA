using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Security;

public sealed class AuthenticatedUserStatusValidator : IAuthenticatedUserStatusValidator
{
    private readonly DefaultContext _context;

    public AuthenticatedUserStatusValidator(DefaultContext context)
    {
        _context = context;
    }

    public async Task<bool> IsActiveAsync(string userId, CancellationToken cancellationToken)
    {
        return Guid.TryParse(userId, out var id) &&
            await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == id && user.Status == UserStatus.Active, cancellationToken);
    }
}
