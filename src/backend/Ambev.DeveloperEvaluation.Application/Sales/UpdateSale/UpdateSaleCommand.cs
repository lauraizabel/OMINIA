using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public sealed record UpdateSaleCommand(
    Guid Id,
    long ExpectedVersion,
    DateTimeOffset SaleDate,
    ExternalIdentityInput Customer,
    ExternalIdentityInput Branch,
    IReadOnlyCollection<SaleItemInput> Items) : IRequest<SaleResult>;
