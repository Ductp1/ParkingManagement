using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.StaffCancellations;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// US-077: kết nối AdminService cho cảnh báo nội bộ. Route /internal không đi qua Gateway → gọi thẳng AdminService.
/// "Services:AdminService:BaseUrl" (mặc định http://localhost:5108) và "Services:AdminService:ApiKey" – khóa CHỈ đặt qua biến môi trường
/// Services__AdminService__ApiKey (bằng InternalEvents__ApiKeys__GateService bên AdminService), không commit.
/// </summary>
public sealed record AdminServiceConnection(Uri BaseUrl, string? ApiKey)
{
    public const string BaseUrlKey = "Services:AdminService:BaseUrl";
    public const string ApiKeyKey = "Services:AdminService:ApiKey";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// POST /internal/v1/risk-flags/staff-abnormal-cancel, header Authorization: Bearer {khóa nội bộ GateService}.
/// Chưa cấu hình khóa / lỗi mạng / HTTP ≠ 2xx → DependencyUnavailableException (StaffCancellationMonitor ghi log, không chặn nghiệp vụ).
/// </summary>
public sealed class AdminAlertClient(HttpClient http, AdminServiceConnection connection) : IAdminAlertClient
{
    public const string StaffAbnormalCancelPath = "internal/v1/risk-flags/staff-abnormal-cancel";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RaiseStaffAbnormalCancelAsync(StaffCancellationAlert alert, CancellationToken cancellationToken)
    {
        if (!connection.IsConfigured)
            throw new DependencyUnavailableException($"Chưa cấu hình {AdminServiceConnection.ApiKeyKey} – không gửi được cảnh báo sang AdminService.");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(connection.BaseUrl, StaffAbnormalCancelPath))
        {
            Content = JsonContent.Create(alert, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"AdminService trả HTTP {(int)response.StatusCode} khi nhận cảnh báo hủy No-show.");
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DependencyUnavailableException($"Không gọi được AdminService để gửi cảnh báo hủy No-show. {ex.Message}");
        }
    }
}
