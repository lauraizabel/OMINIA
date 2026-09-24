using Ambev.DeveloperEvaluation.Application.Sales.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed record CancelSaleCommand(Guid Id, long ExpectedVersion) : IRequest<SaleResult>;
