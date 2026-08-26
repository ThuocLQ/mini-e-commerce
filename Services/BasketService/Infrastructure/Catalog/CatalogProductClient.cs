using System.Diagnostics;
using System.Net.Http.Json;
using BasketService.Application.Abstractions;
using BasketService.Application.Catalog;
using CatalogService.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace BasketService.Infrastructure.Catalog;

public sealed class CatalogProductClient : ICatalogProductClient
{
    private readonly HttpClient _catalogHttpClient;
    private readonly CatalogGrpc.CatalogGrpcClient _catalogGrpcClient;
    private readonly ILogger<CatalogProductClient> _logger;

    public CatalogProductClient(
        HttpClient catalogHttpClient,
        CatalogGrpc.CatalogGrpcClient catalogGrpcClient,
        ILogger<CatalogProductClient> logger)
    {
        _catalogHttpClient = catalogHttpClient;
        _catalogGrpcClient = catalogGrpcClient;
        _logger = logger;
    }

    public async Task<CatalogProduct?> GetProductByIdAsync(
        string productId,
        CatalogCommunicationMode mode,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationName = GetOperationName(mode);

        try
        {
            var product = await GetProductCoreAsync(productId, mode, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "CatalogService {OperationName} for product {ProductId} succeeded in {ElapsedMilliseconds} ms.",
                operationName,
                productId,
                stopwatch.ElapsedMilliseconds);

            return product;
        }
        catch (Exception ex) when (IsUnavailableException(ex))
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "CatalogService {OperationName} is unavailable when getting product {ProductId}",
                operationName,
                productId);

            throw new CatalogUnavailableException(ex);
        }
    }

    public async Task<CatalogCallMeasurement> MeasureGetProductByIdAsync(
        string productId,
        CatalogCommunicationMode mode,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationName = GetOperationName(mode);

        try
        {
            await GetProductCoreAsync(productId, mode, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "CatalogService {OperationName} for product {ProductId} succeeded in {ElapsedMilliseconds} ms.",
                operationName,
                productId,
                stopwatch.ElapsedMilliseconds);

            return new CatalogCallMeasurement(true, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogInformation(
                ex,
                "CatalogService {OperationName} for product {ProductId} failed after {ElapsedMilliseconds} ms.",
                operationName,
                productId,
                stopwatch.ElapsedMilliseconds);

            return new CatalogCallMeasurement(false, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<CatalogProduct?> GetProductCoreAsync(
        string productId,
        CatalogCommunicationMode mode,
        CancellationToken cancellationToken)
    {
        return mode switch
        {
            CatalogCommunicationMode.Rest => await GetProductUsingRestAsync(productId, cancellationToken),
            CatalogCommunicationMode.Grpc => await GetProductUsingGrpcAsync(productId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported catalog communication mode.")
        };
    }

    private async Task<CatalogProduct?> GetProductUsingRestAsync(string productId, CancellationToken cancellationToken)
    {
        using var response = await _catalogHttpClient.GetAsync(
            $"/products/{Uri.EscapeDataString(productId)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"CatalogService returned {(int)response.StatusCode} for product '{productId}'.");
        }

        var product = await response.Content.ReadFromJsonAsync<CatalogProductResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("CatalogService returned an empty product response.");

        return new CatalogProduct(product.Id, product.Name, product.Price);
    }

    private async Task<CatalogProduct?> GetProductUsingGrpcAsync(string productId, CancellationToken cancellationToken)
    {
        var product = await _catalogGrpcClient.GetProductByIdAsync(
            new GetProductByIdRequest
            {
                Id = productId
            },
            deadline: DateTime.UtcNow.AddSeconds(5),
            cancellationToken: cancellationToken);

        return product.Found
            ? new CatalogProduct(product.Id, product.Name, (decimal)product.Price, product.Description)
            : null;
    }

    private static string GetOperationName(CatalogCommunicationMode mode)
    {
        return mode switch
        {
            CatalogCommunicationMode.Rest => "REST GetProductByIdAsync",
            CatalogCommunicationMode.Grpc => "gRPC GetProductByIdAsync",
            _ => mode.ToString()
        };
    }

    private static bool IsUnavailableException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            RpcException => true,
            _ => false
        };
    }
}
