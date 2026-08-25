using System.Net.Http.Json;
using System.Net;
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

    public async Task<DiscountReservationResult> ReserveAsync(string couponCode, Guid orderId, Guid customerId, decimal orderAmount, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/_internal/discounts/reservations", new { couponCode, orderId, customerId, orderAmount, expiresAtUtc }, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            {
                var rejected = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken);
                return new DiscountReservationResult(false, rejected?.Reservation?.Id, couponCode, 0, orderAmount, rejected?.Message ?? "Coupon reservation was rejected.");
            }
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("DiscountService returned an empty reservation response.");
            return new DiscountReservationResult(result.IsReserved, result.Reservation?.Id, result.Reservation?.CouponCode ?? couponCode, result.Reservation?.DiscountAmount ?? 0, result.Reservation?.FinalAmount ?? orderAmount, result.Message);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            throw new DiscountUnavailableException(ex);
        }
    }

    public Task RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default) => OperateAsync(reservationId, orderId, "redeem", null, cancellationToken);
    public Task ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default) => OperateAsync(reservationId, orderId, "release", reason, cancellationToken);

    private async Task OperateAsync(Guid reservationId, Guid orderId, string operation, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"/_internal/discounts/reservations/{reservationId:D}/{operation}", new { orderId, reason }, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
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

    private sealed record ReservationResponse(bool IsReserved, ReservationDto? Reservation, string Message);
    private sealed record ReservationDto(Guid Id, string CouponCode, Guid OrderId, decimal DiscountAmount, decimal FinalAmount, string Status, DateTime ExpiresAtUtc);
}
