using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.History;

namespace UserService.Infrastructure.Integrations;

/// <summary>
/// Adapter đọc BookingService (GET /api/v1/bookings?userId=) và PaymentService (GET /api/v1/payments?bookingId=).
/// userId do Use Case lấy từ token; chuyển tiếp Bearer token của người dùng để service đích tự kiểm tra quyền khi có.
/// </summary>
public sealed class DriverActivitySource(IHttpClientFactory clients, IConfiguration configuration, IHttpContextAccessor http)
    : IDriverActivitySource
{
    public async Task<IReadOnlyList<BookingRecord>> BookingsAsync(int userId, CancellationToken ct)
    {
        var rows = await GetAsync<BookingDto[]>("Integrations:BookingServiceUrl", $"/api/v1/bookings?userId={userId}",
            "Không thể tải lịch sử đặt chỗ từ BookingService.", ct);
        return (rows ?? []).Select(b => new BookingRecord(b.Id, b.Code, b.Status, b.ParkingLotName, b.SlotCode,
            b.PlateNumber, b.StartAtUtc, b.EndAtUtc, b.TotalAmount)).ToList();
    }

    public async Task<IReadOnlyList<PaymentRecord>> PaymentsAsync(int bookingId, CancellationToken ct)
    {
        var rows = await GetAsync<PaymentDto[]>("Integrations:PaymentServiceUrl", $"/api/v1/payments?bookingId={bookingId}",
            "Không thể tải giao dịch từ PaymentService.", ct);
        return (rows ?? []).Select(p => new PaymentRecord(p.Code, p.Method, p.Status, p.Amount, p.RefundedAmount,
            p.PaidAtUtc, p.InvoiceNumber)).ToList();
    }

    private async Task<T?> GetAsync<T>(string key, string path, string failure, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration[key], UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
            throw new DependencyUnavailableException($"Chưa cấu hình {key}.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
            var header = http.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(header) && AuthenticationHeaderValue.TryParse(header, out var auth))
                request.Headers.Authorization = auth;
            using var response = await clients.CreateClient("IdentityIntegrations").SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException(failure);
        }
    }

    private sealed record BookingDto(int Id, string Code, string Status, string ParkingLotName, string? SlotCode,
        string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);
    private sealed record PaymentDto(int Id, string Code, int? BookingId, string? BookingCode, string Method, string Status,
        decimal Amount, decimal RefundedAmount, string? ProviderTransactionId, DateTime? PaidAtUtc, string? InvoiceNumber);
}
