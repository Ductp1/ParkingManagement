using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.PmsIntegration;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// US-052: xác thực API key của PMS qua endpoint verify của ParkingService
/// (POST api/v1/parking-lots/integrations/verify – ParkingService so hash, cập nhật LastSyncAtUtc).
/// Cache kết quả tốt 60 giây/key để giảm gọi liên service khi PMS gửi event dày.
/// </summary>
public sealed class PmsAuthClient(HttpClient http) : IPmsAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly Dictionary<string, (PmsLotIdentity Identity, DateTime CachedAt)> _cache = new(StringComparer.Ordinal);

    public async Task<PmsLotIdentity?> VerifyAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(apiKey, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
            return cached.Identity;

        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/parking-lots/integrations/verify",
                new { apiKey }, JsonOptions, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"ParkingService trả HTTP {(int)response.StatusCode} khi xác thực API key PMS.");

            var dto = await response.Content.ReadFromJsonAsync<VerifyResultJson>(JsonOptions, cancellationToken);
            if (dto is null) return null;

            var identity = new PmsLotIdentity(dto.ParkingLotId, dto.IntegrationId, dto.ProviderName);
            _cache[apiKey] = (identity, DateTime.UtcNow);
            return identity;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DependencyUnavailableException("Không gọi được ParkingService để xác thực API key PMS.", ex);
        }
    }

    private sealed record VerifyResultJson(int ParkingLotId, int IntegrationId, string ProviderName);
}