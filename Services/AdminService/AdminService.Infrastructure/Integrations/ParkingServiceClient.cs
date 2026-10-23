using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.LotContracts;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-116: cài đặt port IParkingServiceClient bằng HttpClient. BaseAddress = Gateway; dùng route Admin KYB của ParkingService
/// (api/v1/parking-lots/kyb-applications) với JWT Admin được chuyển tiếp – AdminService không tham chiếu project / DB của ParkingService.
/// </summary>
public sealed class ParkingServiceClient(HttpClient http, ILogger<ParkingServiceClient> logger) : IParkingServiceClient
{
    private const string BasePath = "api/v1/parking-lots/kyb-applications";
    private const string UnavailableMessage = "ParkingService hiện không phản hồi, vui lòng thử lại sau.";
    private const string UnauthorizedMessage = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
    private const string ForbiddenMessage = "Tài khoản không có quyền thực hiện thao tác này.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<KybLotInfo?> GetKybAsync(int kybApplicationId, CancellationToken cancellationToken)
    {
        var path = $"{BasePath}/{kybApplicationId}";
        var (found, dto) = await SendAsync<KybDetailJson>(HttpMethod.Get, path, body: null, cancellationToken);
        if (!found || dto is null) return null;
        var lot = dto.ParkingLot ?? throw Unavailable($"ParkingService trả hồ sơ KYB thiếu thông tin bãi ({path}).");
        return new KybLotInfo(dto.Id, dto.Status, lot.Id, lot.Name, lot.Address, lot.City, lot.District, lot.TotalSlots, lot.OpenTime,
            lot.CloseTime, lot.Status, lot.OwnerProfileId);
    }

    public async Task ApproveKybAsync(int kybApplicationId, string contractNumber, string? note, CancellationToken cancellationToken)
    {
        var path = $"{BasePath}/{kybApplicationId}/approve";
        var (found, _) = await SendAsync<object>(HttpMethod.Post, path, new { note, contractNumber }, cancellationToken, readBody: false);
        if (!found) throw new NotFoundException("Hồ sơ KYB", kybApplicationId);
    }

    /// <summary>
    /// 404 kèm ProblemDetails → Found = false (không có bản ghi); 401 / 403 → lỗi xác thực; 409 / 400 → ConflictException với thông điệp
    /// của ParkingService; 404 trống (chưa có route) và mọi lỗi khác → 503.
    /// </summary>
    private async Task<(bool Found, T? Value)> SendAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken, bool readBody = true)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (IsJson(response)) return (false, default);
                throw Unavailable($"ParkingService chưa có endpoint {method} {path} (HTTP 404 không có nội dung).");
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new AuthenticationException(UnauthorizedMessage);
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ForbiddenException(ForbiddenMessage);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
                throw new ConflictException(await DetailAsync(response, cancellationToken)
                                            ?? "ParkingService từ chối thao tác trên hồ sơ KYB.");
            if (!response.IsSuccessStatusCode)
                throw Unavailable($"ParkingService trả HTTP {(int)response.StatusCode} khi gọi {method} {path}.");

            if (!readBody) return (true, default);
            return (true, await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw Unavailable($"ParkingService trả nội dung rỗng khi gọi {method} {path}."));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable($"Không gọi được ParkingService ({method} {path}).", ex);
        }
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsJson(response)) return null;
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions, cancellationToken);
        return string.IsNullOrWhiteSpace(problem?.Detail) ? null : problem.Detail;
    }

    private static bool IsJson(HttpResponseMessage response)
        => response.Content.Headers.ContentType?.MediaType is { } mediaType
           && mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase);

    private DependencyUnavailableException Unavailable(string detail, Exception? cause = null)
    {
        logger.LogWarning(cause, "{Detail}", detail);
        return new DependencyUnavailableException(UnavailableMessage);
    }

    private sealed record ProblemJson(string? Detail);

    private sealed record KybDetailJson(int Id, string Status, KybLotJson? ParkingLot);

    private sealed record KybLotJson(int Id, string Name, string Address, string City, string? District, int TotalSlots,
        TimeOnly OpenTime, TimeOnly CloseTime, string Status, int OwnerProfileId);
}
