using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Staff;

namespace UserService.Infrastructure.Integrations;

/// <summary>Adapter gọi ParkingService: GET /api/v1/parking-lots/owner/{ownerProfileId}.</summary>
public sealed class ParkingLotDirectory(IHttpClientFactory clients, IConfiguration configuration, IHttpContextAccessor accessor)
    : IOwnerLotDirectory
{
    public async Task<IReadOnlyList<OwnerLot>> GetLotsAsync(int ownerProfileId, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration["Integrations:ParkingServiceUrl"], UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
            throw new DependencyUnavailableException("Chưa cấu hình ParkingService.");
        try
        {
            var client = clients.CreateClient("IdentityIntegrations");
            // ParkingService chỉ cho đúng chủ bãi (hoặc Admin) đọc: chuyển tiếp JWT của người đang thao tác.
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, $"/api/v1/parking-lots/owner/{ownerProfileId}"));
            if (accessor.HttpContext?.Request.Headers.Authorization.ToString() is { Length: > 0 } authorization)
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var lots = await response.Content.ReadFromJsonAsync<LotDto[]>(ct);
            return (lots ?? []).Select(lot => new OwnerLot(lot.Id, lot.Name, lot.Status)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Không thể xác minh danh sách bãi của chủ bãi.");
        }
    }

    private sealed record LotDto(int Id, string? Name, string? Status);
}
