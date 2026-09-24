using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

[ApiController]
[Route("api/sales")]
[Authorize(Policy = ApiPolicies.SalesOperators)]
public sealed class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedSalesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedSalesResponse>> List(
        [FromQuery] ListSalesRequest _,
        CancellationToken cancellationToken)
    {
        var query = SaleListRequestParser.Parse(Request.Query);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(PagedSalesResponse.From(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SaleResponse>> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSaleCommand(
            request.SaleNumber,
            request.SaleDate,
            request.Customer.ToApplication(),
            request.Branch.ToApplication(),
            request.Items.Select(item => item.ToApplication()).ToArray()), cancellationToken);

        Response.Headers.ETag = SaleEtag.Create(result.Version);
        return Created($"/api/sales/{result.Id}", SaleResponse.From(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSaleQuery(id), cancellationToken);
        Response.Headers.ETag = SaleEtag.Create(result.Version);
        return Ok(SaleResponse.From(result));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<SaleResponse>> Update(
        Guid id,
        [FromBody] UpdateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var expectedVersion = SaleEtag.ParseRequired(Request.Headers.IfMatch.ToString());
        var result = await _mediator.Send(new UpdateSaleCommand(
            id,
            expectedVersion,
            request.SaleDate,
            request.Customer.ToApplication(),
            request.Branch.ToApplication(),
            request.Items.Select(item => item.ToApplication()).ToArray()), cancellationToken);

        Response.Headers.ETag = SaleEtag.Create(result.Version);
        return Ok(SaleResponse.From(result));
    }
}
