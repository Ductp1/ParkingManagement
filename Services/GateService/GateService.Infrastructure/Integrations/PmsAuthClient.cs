using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.PmsIntegration;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// US-052: xác thực API key của PMS qua endpoint verify của ParkingService
/// (POST api/v1/parking-lots/integrations/verify – ParkingService so hash, cập nhật LastSyncAtUtc).
/// 401/403/404 → null (key không hợp lệ); lỗi mạng/5xx → DependencyUnavailableException để PMS retry.
/// </summary>
public sealed class PmsAuthClient(HttpClient http) : IPmsAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PmsLotIdentity?> VerifyAsync(string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/parking-lots/integrations/verify",
                new { apiKey }, JsonOptions, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"ParkingService trả HTTP {(int)response.StatusCode} khi xác thực API key PMS.");

            var dto = await response.Content.ReadFromJsonAsync<VerifyResultJson>(JsonOptions, cancellationToken);
            return dto is null ? null : new PmsLotIdentity(dto.ParkingLotId, dto.IntegrationId, dto.ProviderName);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DependencyUnavailableException($"Không gọi được ParkingService để xác thực API key PMS. {ex.Message}");
        }
    }

    private sealed record VerifyResultJson(int ParkingLotId, int IntegrationId, string ProviderName);
}
