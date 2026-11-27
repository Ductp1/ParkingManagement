using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.LotContracts;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// US-055 AC2: đọc bảng giá của bãi từ PaymentService qua Gateway (GET api/v1/rate-cards?parkingLotId=, JWT Admin chuyển tiếp).
/// "Đang hiệu lực" giống PaymentService khi báo giá: isActive và effectiveFrom ≤ thời điểm &lt; effectiveTo.
/// </summary>
public sealed class RateCardLookupClient(HttpClient http, ILogger<RateCardLookupClient> logger) : IRateCardLookup
{
    private const string UnavailableMessage = "PaymentService hiện không phản hồi, chưa kiểm được bảng giá. Vui lòng thử lại sau.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HasActiveRateCardAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
    {
        var path = $"api/v1/rate-cards?parkingLotId={parkingLotId}";
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AuthenticationException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ForbiddenException("Tài khoản không có quyền thực hiện thao tác này.");
            if (!response.IsSuccessStatusCode)
                throw Unavailable($"PaymentService trả HTTP {(int)response.StatusCode} khi gọi GET {path}.");

            var cards = await response.Content.ReadFromJsonAsync<List<RateCardJson>>(JsonOptions, cancellationToken)
                ?? throw Unavailable($"PaymentService trả nội dung rỗng khi gọi GET {path}.");
            return cards.Any(c => c.IsActive && c.EffectiveFromUtc <= atUtc && (c.EffectiveToUtc is null || c.EffectiveToUtc > atUtc));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable($"Không gọi được PaymentService (GET {path}).", ex);
        }
    }

    private DependencyUnavailableException Unavailable(string detail, Exception? cause = null)
    {
        logger.LogWarning(cause, "{Detail}", detail);
        return new DependencyUnavailableException(UnavailableMessage);
    }

    private sealed record RateCardJson(bool IsActive, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
}
