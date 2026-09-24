using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed record CancelSaleItemCommand(
    Guid SaleId,
    Guid ItemId,
    long ExpectedVersion) : IRequest<SaleResult>;
