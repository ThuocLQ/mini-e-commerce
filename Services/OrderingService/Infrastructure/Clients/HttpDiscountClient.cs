using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Discounts;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpDiscountClient : IDiscountClient
{
    private readonly HttpClient _httpClient;

    public HttpDiscountClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DiscountApplicationResult> ApplyAsync(
        string couponCode,
        decimal orderAmount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/discounts/apply",
                new ApplyDiscountRequest(couponCode, orderAmount),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DiscountResponse>(
                cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("DiscountService returned an empty response.");

            return new DiscountApplicationResult(
                result.CouponCode,
                result.IsValid,
                result.DiscountAmount,
                result.FinalAmount,
                result.Message);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException)
        {
            throw new DiscountUnavailableException(ex);
        }
    }

    private sealed record ApplyDiscountRequest(string CouponCode, decimal OrderAmount);

    private sealed record DiscountResponse(
        string CouponCode,
        bool IsValid,
        decimal OrderAmount,
        decimal DiscountAmount,
        decimal FinalAmount,
        string Message);
}
