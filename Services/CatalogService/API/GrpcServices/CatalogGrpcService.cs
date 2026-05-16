using CatalogService.Application.Products;
using CatalogService.Application.Products.GetProductById;
using CatalogService.Grpc;
using FluentValidation;
using Grpc.Core;
using MediatR;

namespace CatalogService.API.GrpcServices;

public sealed class CatalogGrpcService : CatalogGrpc.CatalogGrpcBase
{
    private readonly ISender _sender;

    public CatalogGrpcService(ISender sender)
    {
        _sender = sender;
    }

    public override async Task<ProductResponse> GetProductById(GetProductByIdRequest request, ServerCallContext context)
    {
        ProductDto? product;

        try
        {
            product = await _sender.Send(new GetProductByIdQuery(request.Id), context.CancellationToken);
        }
        catch (ValidationException exception)
        {
            var message = string.Join("; ", exception.Errors.Select(error => error.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, message));
        }

        if (product is null)
        {
            return new ProductResponse
            {
                Found = false
            };
        }

        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Price = (double)product.Price,
            Found = true,
            // TODO: Persist Description in the catalog database when the lesson reaches schema changes.
            Description = product.Description
        };
    }
}
