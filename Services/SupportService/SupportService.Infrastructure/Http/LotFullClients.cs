using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Http;

/// <summary>
/// US-091: gọi API CÓ SẴN của ParkingService (chỉ đọc) – GET /api/v1/parking-lots/{id} (US-018) và
/// GET /api/v1/parking-lots/search (US-014) để gợi ý bãi thay thế. Địa chỉ: appsettings → "Services:ParkingService".
/// </summary>
public sealed class ParkingLotSearchClient(HttpClient http, ILogger<ParkingLotSearchClient> logger) : IParkingLotSearch
{
    public async Task<LotLocation?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/parking-lots/{parkingLotId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var lot = await response.Content.ReadFromJsonAsync<LotResponse>(cancellationToken);
            return lot is null ? null : new LotLocation(lot.Id, lot.Name, lot.Latitude, lot.Longitude, lot.AvailableSlots);
        }
        // ParkingService tắt, lỗi 5xx hoặc quá thời gian chờ → 503 (không tính trường hợp client tự hủy request).
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được ParkingService khi tra bãi {ParkingLotId}", parkingLotId);
            throw new DependencyUnavailableException("Không kết nối được ParkingService, vui lòng thử lại sau.");
        }
    }

    public async Task<IReadOnlyList<AlternativeLotDto>> SearchAvailableAsync(double latitude, double longitude, double radiusKm,
        CancellationToken cancellationToken = default)
    {
        var url = FormattableString.Invariant(
            $"api/v1/parking-lots/search?lat={latitude}&lng={longitude}&radiusKm={radiusKm}&hasAvailableSlotsOnly=true&openNowOnly=true&limit=20");
        try
        {
            var items = await http.GetFromJsonAsync<List<SearchItem>>(url, cancellationToken) ?? [];
            return items.Select(i => new AlternativeLotDto(i.Id, i.Name, i.Address, i.DistanceKm, i.AvailableSlots, i.IsOpenNow)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được ParkingService khi tìm bãi thay thế");
            throw new DependencyUnavailableException("Không kết nối được ParkingService để tìm bãi thay thế, vui lòng thử lại sau.");
        }
    }

    /// <summary>Chỉ đọc các trường cần dùng trong ParkingLotDto / ParkingLotSearchItemDto của ParkingService.</summary>
    private sealed record LotResponse(int Id, string Name, double Latitude, double Longitude, int AvailableSlots);
    private sealed record SearchItem(int Id, string Name, string Address, double DistanceKm, int AvailableSlots, bool IsOpenNow);
}
