using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminService.Application.Features.Dashboard;
using Microsoft.Extensions.Logging;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-081: GET một endpoint tổng hợp của service nguồn qua Gateway và ánh xạ kết quả cho dashboard (không ném exception, không retry):
/// 2xx → Ok; 404 / 501 (endpoint chưa có, kể cả Gateway chưa định tuyến) → NoSource; 401 / 403 / mã lỗi khác / mạng / JSON sai /
/// HttpClient.Timeout → Error. Request bị hủy (người dùng rời trang hoặc timeout riêng của nguồn) thì để OperationCanceledException nổi lên.
/// </summary>
public static class DashboardSourceHttp
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<SourceResult<T>> GetAsync<T>(HttpClient http, string service, string path, ILogger logger,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            var code = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NotImplemented)
                return SourceResult<T>.NoSource($"{service} chưa có endpoint GET /{Route(path)} (HTTP {code}).");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Fail<T>(logger, service, path, $"{service} từ chối truy vấn (HTTP {code}) – phiên hoặc quyền không hợp lệ.");
            if (!response.IsSuccessStatusCode)
                return Fail<T>(logger, service, path, $"{service} trả HTTP {code}.");

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value is null ? Fail<T>(logger, service, path, $"{service} trả nội dung rỗng.") : SourceResult<T>.Ok(value);
        }
        catch (HttpRequestException ex)
        {
            return Fail<T>(logger, service, path, $"Không kết nối được {service}.", ex);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Fail<T>(logger, service, path, $"{service} trả dữ liệu không đúng định dạng.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(logger, service, path, $"{service} không phản hồi kịp.", ex);
        }
    }

    /// <summary>Route không kèm query (thông báo cho Admin không lộ tham số).</summary>
    private static string Route(string path) => path.Split('?')[0];

    private static SourceResult<T> Fail<T>(ILogger logger, string service, string path, string detail, Exception? cause = null) where T : class
    {
        logger.LogWarning(cause, "Dashboard: {Service} GET /{Route} lỗi – {Detail}", service, Route(path), detail);
        return SourceResult<T>.Error(detail);
    }
}
