using System.Net;
using System.Net.Http.Json;
using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure.Clients;

public sealed class HttpOrderPaymentClient : IOrderPaymentClient
{
    private readonly HttpClient _httpClient;

    public HttpOrderPaymentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderPaymentSnapshot?> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/orders/{orderId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OrderServiceUnavailableException(
                $"OrderingService returned {(int)response.StatusCode} while reading order {orderId:D}.");
        }

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken: cancellationToken)
            ?? throw new OrderServiceUnavailableException("OrderingService returned an empty order response.");

        return new OrderPaymentSnapshot(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Currency,
            order.Status);
    }

    private sealed record OrderResponse(
        Guid Id,
        Guid CustomerId,
        DateTime CreatedAtUtc,
        string Status,
        decimal TotalAmount,
        string Currency,
        IReadOnlyList<OrderItemResponse> Items)
    ;

    private sealed record OrderItemResponse(
        Guid Id,
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal TotalPrice);
}

public sealed class OrderServiceUnavailableException : Exception
{
    public OrderServiceUnavailableException(string message) : base(message)
    {
    }
}
