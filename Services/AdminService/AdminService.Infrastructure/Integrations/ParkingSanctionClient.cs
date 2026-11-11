using System.Net;
using System.Net.Http.Json;
using AdminService.Application.Features.Sanctions;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-097: đẩy hiệu lực chế tài sang ParkingService qua Gateway – PUT / DELETE api/v1/parking-lots/sanction-states/{sanctionId}
/// (RequireAdmin, JWT Admin chuyển tiếp; cả hai idempotent nên gọi lại an toàn).
/// </summary>
public sealed class ParkingSanctionClient(HttpClient http, ILogger<ParkingSanctionClient> logger) : IParkingSanctionClient
{
    public Task ApplyAsync(ParkingSanctionCommand command, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Put, command.SanctionId, JsonContent.Create(new
        {
            ownerProfileId = command.OwnerProfileId,
            parkingLotId = command.ParkingLotId,
            kind = command.Kind,
            startsAtUtc = command.StartsAtUtc,
            endsAtUtc = command.EndsAtUtc,
        }), cancellationToken);

    public Task LiftAsync(int sanctionId, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Delete, sanctionId, null, cancellationToken);

    private async Task SendAsync(HttpMethod method, int sanctionId, HttpContent? content, CancellationToken cancellationToken)
    {
        var path = $"api/v1/parking-lots/sanction-states/{sanctionId}";
        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = content };
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AuthenticationException("ParkingService từ chối: thiếu hoặc hết hạn phiên đăng nhập Admin.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ForbiddenException("ParkingService từ chối: tài khoản không có quyền Admin.");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("ParkingService trả HTTP {Status} khi gọi {Method} {Path}: {Body}", (int)response.StatusCode, method, path, body);
                throw new DependencyUnavailableException($"ParkingService trả HTTP {(int)response.StatusCode}.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Không gọi được ParkingService ({Method} {Path}).", method, path);
            throw new DependencyUnavailableException("ParkingService hiện không phản hồi.");
        }
    }
}
