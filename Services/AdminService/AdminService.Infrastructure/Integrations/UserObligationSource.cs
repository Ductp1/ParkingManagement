using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Features.Users;
using AdminService.Domain.Enums;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Integrations;

/// <summary>
/// Nghĩa vụ của user trước khi xóa (US-096). Booking + settlement: GET qua Gateway bằng JWT Admin chuyển tiếp (BookingService liệt kê theo
/// userId + một status mỗi lần; PaymentService theo ownerProfileId + một status mỗi lần). Hợp đồng Signed + chế tài Active: pm_admin.
/// Không gọi được / mã lạ → 503 để KHÔNG xóa khi chưa chắc chắn.
/// </summary>
public sealed class UserObligationSource(HttpClient http, AdminDbContext db, ILogger<UserObligationSource> logger) : IUserObligationSource
{
    private static readonly string[] OpenBookingStatuses = ["PendingPayment", "PendingOwnerApproval", "Confirmed", "CheckedIn", "Parking"];
    private static readonly string[] UnpaidSettlementStatuses = ["Draft", "PendingReview", "Approved"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HasOpenBookingsAsync(int userId, CancellationToken cancellationToken)
    {
        foreach (var status in OpenBookingStatuses)
        {
            var items = await GetAsync<JsonElement>($"api/v1/bookings?userId={userId}&status={status}&page=1&pageSize=1", cancellationToken);
            if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0) return true;
        }
        return false;
    }

    public async Task<int> CountUnpaidSettlementsAsync(int ownerProfileId, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var status in UnpaidSettlementStatuses)
        {
            var page = await GetAsync<PagedResult<JsonElement>>(
                $"api/v1/payments/admin/settlements?status={status}&ownerProfileId={ownerProfileId}&page=1&pageSize=1", cancellationToken);
            total += page.TotalCount;
        }
        return total;
    }

    public Task<int> CountSignedContractsAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.LotContracts.CountAsync(c => c.OwnerProfileId == ownerProfileId && c.Status == LotContractStatus.Signed, cancellationToken);

    public Task<int> CountActiveSanctionsAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.Sanctions.CountAsync(s => s.OwnerProfileId == ownerProfileId && s.Status == SanctionStatus.Active, cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new AuthenticationException("Phiên đăng nhập không hợp lệ.");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ForbiddenException("Không có quyền kiểm tra nghĩa vụ của người dùng.");
            if (!response.IsSuccessStatusCode) throw Unavailable($"HTTP {(int)response.StatusCode} khi gọi GET {path}.");
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw Unavailable($"Nội dung rỗng khi gọi GET {path}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable($"Không gọi được dịch vụ kiểm tra nghĩa vụ (GET {path}).", ex);
        }
    }

    private DependencyUnavailableException Unavailable(string detail, Exception? cause = null)
    {
        logger.LogWarning(cause, "{Detail}", detail);
        return new DependencyUnavailableException("Chưa kiểm tra được nghĩa vụ của người dùng (booking / quyết toán) nên chưa thể xóa. Vui lòng thử lại sau.");
    }
}
