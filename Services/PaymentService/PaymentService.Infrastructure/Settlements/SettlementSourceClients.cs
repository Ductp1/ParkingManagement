using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;
using PaymentService.Infrastructure.Commission;

namespace PaymentService.Infrastructure.Settlements;

/// <summary>
/// Gateway cho các nguồn đọc qua route công khai (BookingService, SupportService): "Services:GatewayBaseUrl" (mặc định http://localhost:5000)
/// và "Settlements:SourceTimeoutSeconds" (mặc định 10 giây – timeout mỗi lần gọi).
/// </summary>
public sealed record SettlementGatewayConnection(Uri BaseUrl, TimeSpan Timeout)
{
    public const string BaseUrlKey = "Services:GatewayBaseUrl";
    public const string DefaultBaseUrl = "http://localhost:5000/";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
}

internal static class SourceCall
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Gửi request với timeout riêng; mạng / timeout / JSON lỗi → DependencyUnavailableException (503, lượt sau thử lại).</summary>
    public static async Task<T> SendAsync<T>(HttpClient http, HttpRequestMessage request, TimeSpan timeout, string source,
        Func<HttpResponseMessage, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            using var response = await http.SendAsync(request, linked.Token);
            return await read(response, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException($"{source} không phản hồi trong {timeout.TotalSeconds:0.#} giây.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            throw new DependencyUnavailableException($"Không gọi được {source}: {ex.Message}");
        }
    }
}

/// <summary>
/// US-080: SETTLEMENT_CYCLE có hiệu lực tại một thời điểm – GET /internal/v1/system-configs/SETTLEMENT_CYCLE/resolve của AdminService
/// (gọi thẳng :5108, khóa nội bộ "Services:AdminService:ApiKey" như US-079 – chỉ qua biến môi trường).
/// </summary>
public sealed class AdminSettlementCycleClient(HttpClient http, AdminServiceConnection connection) : ISettlementCycleClient
{
    public const string ResolvePath = "internal/v1/system-configs/SETTLEMENT_CYCLE/resolve";

    private sealed record ResolveResponse(string? Key, string? Value, DateTime? EffectiveFromUtc);

    public async Task<SettlementCycleQuote> ResolveAsync(DateTime atUtc, CancellationToken cancellationToken)
    {
        if (!connection.IsConfigured)
            throw new DependencyUnavailableException(
                $"Chưa cấu hình {AdminServiceConnection.BaseUrlKey} / {AdminServiceConnection.ApiKeyKey} – không đọc được chu kỳ quyết toán.");

        var at = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(connection.BaseUrl!, $"{ResolvePath}?atUtc={Uri.EscapeDataString(at)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);

        return await SourceCall.SendAsync(http, request, connection.Timeout, "AdminService", async (response, ct) =>
        {
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"AdminService trả HTTP {(int)response.StatusCode} khi đọc SETTLEMENT_CYCLE.");
            var body = await response.Content.ReadFromJsonAsync<ResolveResponse>(SourceCall.Json, ct);
            if (string.IsNullOrWhiteSpace(body?.Value))
                throw new DependencyUnavailableException("AdminService trả SETTLEMENT_CYCLE rỗng.");
            return new SettlementCycleQuote(body.Value.Trim(),
                body.EffectiveFromUtc is { } from ? DateTime.SpecifyKind(from.ToUniversalTime(), DateTimeKind.Utc) : null);
        }, cancellationToken);
    }
}

/// <summary>
/// US-080: trạng thái booking – GET /api/v1/bookings/{code} qua Gateway (endpoint có sẵn, không cần token). Chỉ đọc status + lịch sử trạng
/// thái; bỏ qua mọi trường khác (biển số, người đặt…). Tối đa 4 lời gọi song song. 404 → NotFound; lỗi khác → DependencyUnavailableException.
/// </summary>
public sealed class BookingStatusClient(HttpClient http, SettlementGatewayConnection gateway) : IBookingStatusClient
{
    public const int MaxParallel = 4;

    private sealed record HistoryItem(string? ToStatus, DateTime? AtUtc);
    private sealed record BookingResponse(string? Status, IReadOnlyList<HistoryItem>? History);

    public async Task<IReadOnlyDictionary<string, BookingStatusInfo>> GetStatusesAsync(IReadOnlyCollection<string> bookingCodes,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, BookingStatusInfo>(StringComparer.OrdinalIgnoreCase);
        using var gate = new SemaphoreSlim(MaxParallel);
        var tasks = bookingCodes.Distinct(StringComparer.OrdinalIgnoreCase).Select(async code =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return (code, await GetOneAsync(code, cancellationToken));
            }
            finally
            {
                gate.Release();
            }
        }).ToList();
        foreach (var (code, info) in await Task.WhenAll(tasks)) results[code] = info;
        return results;
    }

    private async Task<BookingStatusInfo> GetOneAsync(string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(gateway.BaseUrl, $"api/v1/bookings/{Uri.EscapeDataString(code)}"));
        return await SourceCall.SendAsync(http, request, gateway.Timeout, "BookingService", async (response, ct) =>
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return BookingStatusInfo.NotFound;
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"BookingService trả HTTP {(int)response.StatusCode} khi đọc booking {code}.");
            var body = await response.Content.ReadFromJsonAsync<BookingResponse>(SourceCall.Json, ct);
            if (string.IsNullOrWhiteSpace(body?.Status))
                throw new DependencyUnavailableException($"BookingService trả booking {code} không có trạng thái.");
            // Lúc chuyển sang trạng thái hiện tại = lần chuyển muộn nhất có ToStatus đó; không có lịch sử → null (không đoán).
            DateTime? since = (body.History ?? [])
                .Where(h => h.AtUtc is not null && string.Equals(h.ToStatus, body.Status, StringComparison.OrdinalIgnoreCase))
                .Select(h => (DateTime?)DateTime.SpecifyKind(h.AtUtc!.Value.ToUniversalTime(), DateTimeKind.Utc))
                .Max();
            return new BookingStatusInfo(true, body.Status, since);
        }, cancellationToken);
    }
}

/// <summary>
/// US-080: khoản giữ tranh chấp đang mở – GET /api/v1/complaints/payout-holds qua Gateway, chuyển tiếp JWT Admin của request hiện tại
/// (endpoint chỉ nhận LotOwner / Admin). Không có JWT (job nền) → DependencyUnavailableException; job không gọi client này.
/// </summary>
public sealed class PayoutHoldClient(HttpClient http, SettlementGatewayConnection gateway, IHttpContextAccessor httpContextAccessor)
    : IPayoutHoldClient
{
    private const string BearerPrefix = "Bearer ";

    private sealed record HoldResponse(string? ComplaintCode, string? Status, int ParkingLotId, int? OwnerProfileId, string? BookingCode,
        decimal Amount, DateTime HeldSinceUtc);

    public async Task<IReadOnlyList<PayoutHoldItem>> ListOpenHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            throw new DependencyUnavailableException("Cần JWT Admin để đọc khoản giữ tranh chấp từ SupportService.");

        var path = "api/v1/complaints/payout-holds" + (ownerProfileId is { } owner ? $"?ownerProfileId={owner}" : string.Empty);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(gateway.BaseUrl, path));
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        return await SourceCall.SendAsync(http, request, gateway.Timeout, "SupportService", async (response, ct) =>
        {
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"SupportService trả HTTP {(int)response.StatusCode} khi đọc khoản giữ tranh chấp.");
            var body = await response.Content.ReadFromJsonAsync<List<HoldResponse>>(SourceCall.Json, ct)
                       ?? throw new DependencyUnavailableException("SupportService trả danh sách khoản giữ rỗng.");
            return (IReadOnlyList<PayoutHoldItem>)[.. body.Where(h => !string.IsNullOrWhiteSpace(h.ComplaintCode))
                .Select(h => new PayoutHoldItem(h.ComplaintCode!.Trim(), h.Status ?? string.Empty, h.ParkingLotId, h.OwnerProfileId, h.BookingCode,
                    h.Amount, DateTime.SpecifyKind(h.HeldSinceUtc.ToUniversalTime(), DateTimeKind.Utc)))];
        }, cancellationToken);
    }
}
