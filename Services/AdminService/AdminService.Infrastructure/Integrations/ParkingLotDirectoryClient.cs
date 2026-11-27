using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.Sanctions;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-097: đọc danh sách bãi của chủ bãi từ ParkingService qua Gateway (GET api/v1/parking-lots/owner/{ownerProfileId}, JWT Admin
/// chuyển tiếp) để xác minh bãi được chọn thuộc đúng chủ bãi. AdminService không tham chiếu project / DB của ParkingService.
/// </summary>
public sealed class ParkingLotDirectoryClient(HttpClient http, ILogger<ParkingLotDirectoryClient> logger) : IParkingLotDirectory
{
    private const string UnavailableMessage = "ParkingService hiện không phản hồi, vui lòng thử lại sau.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<OwnerLotInfo>> ListOwnerLotsAsync(int ownerProfileId, CancellationToken cancellationToken)
    {
        var path = $"api/v1/parking-lots/owner/{ownerProfileId}";
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AuthenticationException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ForbiddenException("Tài khoản không có quyền thực hiện thao tác này.");
            if (!response.IsSuccessStatusCode)
                throw Unavailable($"ParkingService trả HTTP {(int)response.StatusCode} khi gọi GET {path}.");

            var lots = await response.Content.ReadFromJsonAsync<List<OwnerLotJson>>(JsonOptions, cancellationToken)
                ?? throw Unavailable($"ParkingService trả nội dung rỗng khi gọi GET {path}.");
            return lots.Select(l => new OwnerLotInfo(l.Id, l.Name ?? string.Empty, l.Status ?? string.Empty)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable($"Không gọi được ParkingService (GET {path}).", ex);
        }
    }

    private DependencyUnavailableException Unavailable(string detail, Exception? cause = null)
    {
        logger.LogWarning(cause, "{Detail}", detail);
        return new DependencyUnavailableException(UnavailableMessage);
    }

    private sealed record OwnerLotJson(int Id, string? Name, string? Status);
}
