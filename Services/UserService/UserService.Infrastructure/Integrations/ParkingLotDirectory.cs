using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Staff;

namespace UserService.Infrastructure.Integrations;

/// <summary>Adapter gọi ParkingService: GET /api/v1/parking-lots/owner/{ownerProfileId}.</summary>
public sealed class ParkingLotDirectory(IHttpClientFactory clients, IConfiguration configuration) : IOwnerLotDirectory
{
    public async Task<IReadOnlyList<OwnerLot>> GetLotsAsync(int ownerProfileId, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration["Integrations:ParkingServiceUrl"], UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
            throw new DependencyUnavailableException("Chưa cấu hình ParkingService.");
        try
        {
            var client = clients.CreateClient("IdentityIntegrations");
            var lots = await client.GetFromJsonAsync<LotDto[]>(new Uri(baseUri, $"/api/v1/parking-lots/owner/{ownerProfileId}"), ct);
            return (lots ?? []).Select(lot => new OwnerLot(lot.Id, lot.Name)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Không thể xác minh danh sách bãi của chủ bãi.");
        }
    }

    private sealed record LotDto(int Id, string? Name);
}
