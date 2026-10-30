using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GateService.Application.Features.GateConsole;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.HttpClients;

/// <summary>
/// Client HTTP gọi BookingService (TV3) phục vụ Gate Console (UC-20/UC-21/UC-22).
/// Lỗi hạ tầng (mạng, 5xx, timeout) → DependencyUnavailableException (HTTP 503); 404 → null cho tra cứu.
/// </summary>
public sealed class GateBookingClient(HttpClient http, ILogger<GateBookingClient> logger) : IGateBookingClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<GateBookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/v1/bookings/{Uri.EscapeDataString(bookingCode)}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, $"tra cứu booking {bookingCode}", cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<BookingApiResponse>(Json, cancellationToken)
            ?? throw new DependencyUnavailableException($"BookingService trả dữ liệu rỗng khi tra cứu booking {bookingCode}.");
        return new GateBookingSnapshot(dto.Code, dto.Status, dto.ParkingLotId, dto.PlateNumber, dto.VehicleType,
            dto.StartAtUtc, dto.EndAtUtc, dto.SlotCode, dto.PriceSnapshot?.FinalAmount ?? 0,
            dto.PriceSnapshot?.BillingGracePeriodMinutes ?? 15, dto.PriceSnapshot?.OverstayMultiplier ?? 1.5m);
    }

    public async Task<IReadOnlyList<GateBookingListItem>> ListForLotAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        string? search, CancellationToken cancellationToken = default)
    {
        var query = $"parkingLotId={parkingLotId}&fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}" +
                    $"&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search.Trim())}";

        using var response = await SendAsync(HttpMethod.Get, $"api/v1/bookings/gate?{query}", null, cancellationToken);
        await EnsureSuccessAsync(response, "lấy danh sách booking trong ca", cancellationToken);
        var items = await response.Content.ReadFromJsonAsync<List<GateBookingListItemApiResponse>>(Json, cancellationToken) ?? [];
        return items.Select(i => new GateBookingListItem(i.Code, i.PlateNumber, i.VehicleType, i.SlotCode,
            i.StartAtUtc, i.EndAtUtc, i.Status, i.IsOverdue, i.TotalAmount)).ToList();
    }

    public Task SyncCheckInAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
        => SyncGateActionAsync(bookingCode, "check-in", staffUserId, cancellationToken);

    public Task SyncCheckOutAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
        => SyncGateActionAsync(bookingCode, "check-out", staffUserId, cancellationToken);

    /// <summary>Gọi endpoint nội bộ TV7 của BookingService: POST /api/v1/bookings/{code}/check-in|check-out.</summary>
    private async Task SyncGateActionAsync(string bookingCode, string action, int? staffUserId, CancellationToken cancellationToken)
    {
        var body = new StringContent(JsonSerializer.Serialize(new { staffUserId, gateDeviceId = (int?)null }, Json),
            Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, $"api/v1/bookings/{Uri.EscapeDataString(bookingCode)}/{action}", body, cancellationToken);
        await EnsureSuccessAsync(response, $"đồng bộ booking {bookingCode} ({action})", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "BookingService không phản hồi khi gọi {Url}", url);
            throw new DependencyUnavailableException("BookingService chưa sẵn sàng, thử lại sau ít phút.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("BookingService phản hồi quá chậm (timeout), thử lại sau ít phút.");
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var detail = await SafeReadDetailAsync(response, ct);
        // Xung đột nghiệp vụ (booking vừa bị hủy/đổi trạng thái...) → 409 kèm lý do; còn lại coi là lỗi hạ tầng.
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ConflictException(detail ?? "BookingService từ chối do xung đột trạng thái booking.");
        logger.LogWarning("BookingService trả {Status} khi {Action}: {Detail}", (int)response.StatusCode, action, detail);
        throw new DependencyUnavailableException($"BookingService chưa sẵn sàng khi {action} (HTTP {(int)response.StatusCode}).");
    }

    private static async Task<string?> SafeReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailResponse>(Json, ct);
            return problem?.Detail ?? problem?.Title;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or FormatException)
        {
            return null;
        }
    }

    // Bản ghi khớp camelCase JSON của BookingService (BookingDetailDto).
    private sealed record BookingApiResponse(string Code, string Status, int ParkingLotId, string PlateNumber,
        string VehicleType, DateTime StartAtUtc, DateTime EndAtUtc, string? SlotCode, decimal TotalAmount,
        PriceSnapshotResponse? PriceSnapshot);
    private sealed record PriceSnapshotResponse(decimal FinalAmount, int BillingGracePeriodMinutes, decimal OverstayMultiplier);
    private sealed record GateBookingListItemApiResponse(string Code, string PlateNumber, string VehicleType, string? SlotCode,
        DateTime StartAtUtc, DateTime EndAtUtc, string Status, bool IsOverdue, decimal TotalAmount);
    private sealed record ProblemDetailResponse(string? Title, string? Detail);
}
