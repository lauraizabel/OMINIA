using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public sealed record DeleteSaleCommand(Guid Id, long ExpectedVersion) : IRequest<Unit>;
