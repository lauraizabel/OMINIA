using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public sealed record CreateSaleCommand(
    string SaleNumber,
    DateTimeOffset SaleDate,
    ExternalIdentityInput Customer,
    ExternalIdentityInput Branch,
    IReadOnlyCollection<SaleItemInput> Items) : IRequest<SaleResult>;
