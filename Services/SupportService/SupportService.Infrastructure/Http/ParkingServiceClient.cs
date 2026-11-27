using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Http;

/// <summary>
/// Gọi ParkingService (GET /api/v1/parking-lots/owner/{ownerProfileId}) để biết các bãi của 1 chủ bãi (US-094).
/// Địa chỉ cấu hình ở appsettings → "Services:ParkingService".
/// </summary>
public sealed class ParkingServiceClient(HttpClient http, ILogger<ParkingServiceClient> logger) : IOwnerLotLookup
{
    public async Task<IReadOnlyCollection<int>> GetLotIdsAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var lots = await http.GetFromJsonAsync<List<LotResponse>>($"api/v1/parking-lots/owner/{ownerProfileId}", cancellationToken);
            return lots?.Select(l => l.Id).ToHashSet() ?? [];
        }
        // ParkingService tắt, lỗi 5xx hoặc quá thời gian chờ → 503 (không tính trường hợp client tự hủy request).
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được ParkingService khi tra bãi của chủ bãi {OwnerProfileId}", ownerProfileId);
            throw new DependencyUnavailableException("Không kết nối được ParkingService để xác minh bãi của chủ bãi, vui lòng thử lại sau.");
        }
    }

    /// <summary>Chỉ đọc Id trong OwnerParkingLotItemDto của ParkingService.</summary>
    private sealed record LotResponse(int Id);
}
