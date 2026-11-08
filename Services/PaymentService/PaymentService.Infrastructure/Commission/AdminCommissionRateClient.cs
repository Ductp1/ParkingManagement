using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentService.Application.Features.Commission;

namespace PaymentService.Infrastructure.Commission;

/// <summary>
/// US-079: kết nối tới AdminService cho job hoa hồng. Route /internal không đi qua Gateway → gọi thẳng AdminService.
/// "Services:AdminService:BaseUrl" (mặc định http://localhost:5108) và "Services:AdminService:ApiKey" – khóa CHỈ đặt qua biến môi trường
/// Services__AdminService__ApiKey (bằng InternalEvents__ApiKeys__PaymentService bên AdminService), không commit.
/// </summary>
public sealed record AdminServiceConnection(Uri? BaseUrl, string? ApiKey, TimeSpan Timeout)
{
    public const string BaseUrlKey = "Services:AdminService:BaseUrl";
    public const string ApiKeyKey = "Services:AdminService:ApiKey";
    public const string TimeoutKey = "Commission:AdminTimeoutSeconds";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public bool IsConfigured => BaseUrl is not null && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// POST /internal/v1/commission-rates/resolve, header Authorization: Bearer {khóa nội bộ}. Timeout riêng mỗi lần gọi; không retry
/// (lượt sau của job là retry). Mọi lỗi (chưa cấu hình, mạng, timeout, HTTP ≠ 200, JSON sai) → CommissionRateUnavailableException.
/// </summary>
public sealed class AdminCommissionRateClient(HttpClient http, AdminServiceConnection connection) : ICommissionRateClient
{
    public const string ResolvePath = "internal/v1/commission-rates/resolve";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record ResolveItem(int ParkingLotId, DateTime AtUtc);
    private sealed record ResolveRequest(IReadOnlyList<ResolveItem> Items);
    private sealed record ResolveResponse(IReadOnlyList<CommissionRateQuote>? Items);

    public async Task<IReadOnlyList<CommissionRateQuote>> ResolveAsync(IReadOnlyList<(int ParkingLotId, DateTime AtUtc)> items,
        CancellationToken cancellationToken)
    {
        if (!connection.IsConfigured)
            throw new CommissionRateUnavailableException($"Chưa cấu hình {AdminServiceConnection.BaseUrlKey} / {AdminServiceConnection.ApiKeyKey}.");
        if (items.Count == 0) return [];

        using var timeout = new CancellationTokenSource(connection.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(connection.BaseUrl!, ResolvePath))
        {
            Content = JsonContent.Create(new ResolveRequest([.. items.Select(i => new ResolveItem(i.ParkingLotId, i.AtUtc))]), options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);

        try
        {
            using var response = await http.SendAsync(request, linked.Token);
            if (!response.IsSuccessStatusCode)
                throw new CommissionRateUnavailableException($"AdminService trả HTTP {(int)response.StatusCode} khi tra tỷ lệ hoa hồng.");
            var body = await response.Content.ReadFromJsonAsync<ResolveResponse>(Json, linked.Token);
            return body?.Items ?? throw new CommissionRateUnavailableException("AdminService trả body rỗng khi tra tỷ lệ hoa hồng.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CommissionRateUnavailableException(
                $"AdminService không phản hồi trong {connection.Timeout.TotalSeconds:0.#} giây.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            throw new CommissionRateUnavailableException("Không gọi được AdminService để tra tỷ lệ hoa hồng.", ex);
        }
    }
}
