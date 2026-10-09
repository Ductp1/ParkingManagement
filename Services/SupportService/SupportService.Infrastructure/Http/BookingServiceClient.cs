using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Http;

/// <summary>
/// Gọi BookingService (GET /api/v1/bookings/{code}) để kiểm tra booking khi tạo khiếu nại.
/// Địa chỉ cấu hình ở appsettings → "Services:BookingService".
/// </summary>
public sealed class BookingServiceClient(HttpClient http, ILogger<BookingServiceClient> logger) : IBookingLookup
{
    public async Task<BookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        BookingResponse? dto;
        try
        {
            using var response = await http.GetAsync($"api/v1/bookings/{Uri.EscapeDataString(bookingCode)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            dto = await response.Content.ReadFromJsonAsync<BookingResponse>(cancellationToken);
        }
        // BookingService tắt, lỗi 5xx hoặc quá thời gian chờ → 503 thay vì 500 (không tính trường hợp client tự hủy request).
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được BookingService khi tra booking {BookingCode}", bookingCode);
            throw new DependencyUnavailableException("Không kết nối được BookingService để kiểm tra booking, vui lòng thử lại sau.");
        }

        if (dto is null) throw new DependencyUnavailableException("BookingService trả về dữ liệu rỗng.");

        return new BookingSnapshot(
            dto.Id, dto.Code, Enum.Parse<BookingStatus>(dto.Status, ignoreCase: true), dto.UserId, dto.ParkingLotId,
            dto.OwnerProfileId, dto.EndAtUtc, dto.History?.Max(h => (DateTime?)h.AtUtc), dto.PaidAmount);
    }

    /// <summary>Chỉ đọc các trường cần dùng trong BookingDetailDto của BookingService.</summary>
    private sealed record BookingResponse(int Id, string Code, string Status, int UserId, int ParkingLotId,
        int? OwnerProfileId, DateTime EndAtUtc, decimal PaidAmount, List<StatusLog>? History);

    private sealed record StatusLog(DateTime AtUtc);
}
