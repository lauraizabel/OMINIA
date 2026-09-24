using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Results;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

internal static class SaleListQueryValidation
{
    [DoesNotReturn]
    public static void Fail(string field, string code, string message) =>
        throw new ValidationException([new ValidationFailure(field, message) { ErrorCode = code }]);
}
