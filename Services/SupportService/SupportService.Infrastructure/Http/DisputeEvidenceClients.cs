using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Http;

/// <summary>
/// US-090 AC1: lịch sử booking cho hồ sơ tranh chấp – GET /api/v1/bookings/{code} có sẵn của BookingService (chỉ đọc).
/// Địa chỉ cấu hình ở appsettings → "Services:BookingService".
/// </summary>
public sealed class BookingHistoryClient(HttpClient http, ILogger<BookingHistoryClient> logger) : IBookingHistoryLookup
{
    public async Task<BookingEvidenceDto?> GetAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/bookings/{Uri.EscapeDataString(bookingCode)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<BookingEvidenceDto>(cancellationToken)
                   ?? throw new DependencyUnavailableException("BookingService trả về dữ liệu rỗng.");
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được BookingService khi lấy lịch sử booking {BookingCode}", bookingCode);
            throw new DependencyUnavailableException("Không kết nối được BookingService để lấy lịch sử booking, vui lòng thử lại sau.");
        }
    }
}

/// <summary>
/// US-090 AC1: log check-in/out của booking – GET /api/v1/parking-sessions?parkingLotId&amp;status có sẵn của GateService (chỉ đọc).
/// API chỉ lọc theo bãi + trạng thái nên gọi lần lượt các trạng thái rồi lọc theo mã booking.
/// Địa chỉ cấu hình ở appsettings → "Services:GateService".
/// </summary>
public sealed class GateSessionClient(HttpClient http, ILogger<GateSessionClient> logger) : IGateSessionLookup
{
    private static readonly string[] Statuses = ["Active", "Completed", "Abnormal"];

    public async Task<IReadOnlyList<GateSessionEvidenceDto>> ListForBookingAsync(int parkingLotId, string bookingCode,
        CancellationToken cancellationToken = default)
    {
        var sessions = new List<GateSessionEvidenceDto>();
        try
        {
            foreach (var status in Statuses)
            {
                var rows = await http.GetFromJsonAsync<List<SessionResponse>>(
                    FormattableString.Invariant($"api/v1/parking-sessions?parkingLotId={parkingLotId}&status={status}"), cancellationToken);
                sessions.AddRange((rows ?? [])
                    .Where(s => string.Equals(s.BookingCode, bookingCode, StringComparison.OrdinalIgnoreCase))
                    .Select(s => new GateSessionEvidenceDto(s.Code, s.Status, s.PlateDisplay, s.EntryAtUtc, s.ExitAtUtc, s.MinutesParked,
                        s.CheckInMethod, s.EntryOcrConfidence, s.Fee)));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được GateService khi lấy log check-in/out của booking {BookingCode}", bookingCode);
            throw new DependencyUnavailableException("Không kết nối được GateService để lấy log check-in/out, vui lòng thử lại sau.");
        }
        return sessions.OrderBy(s => s.EntryAtUtc).ToList();
    }

    /// <summary>Chỉ đọc các trường cần dùng trong ParkingSessionDto của GateService.</summary>
    private sealed record SessionResponse(string Code, string? BookingCode, string Status, string PlateDisplay, DateTime EntryAtUtc,
        DateTime? ExitAtUtc, int MinutesParked, string CheckInMethod, double? EntryOcrConfidence, decimal? Fee);
}
