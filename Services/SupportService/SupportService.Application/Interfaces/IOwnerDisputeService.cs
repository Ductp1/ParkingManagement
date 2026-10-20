using SupportService.Application.DTOs;

namespace SupportService.Application.Interfaces;

/// <summary>
/// US-089: chủ bãi xem và phản hồi tranh chấp về bãi của mình.
/// <c>ownerProfileId</c> / <c>ownerUserId</c> là của người đang đăng nhập (lấy từ JWT).
/// </summary>
public interface IOwnerDisputeService
{
    Task<ComplaintDetailDto> GetAsync(int ownerProfileId, string code, CancellationToken cancellationToken = default);

    Task<ComplaintDetailDto> RespondAsync(int ownerProfileId, int? ownerUserId, string code, OwnerResponseRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>AC3: khoản tiền đang bị phong tỏa. <paramref name="ownerProfileId"/> null = mọi chủ bãi (Admin).</summary>
    Task<IReadOnlyList<PayoutHoldDto>> ListPayoutHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>US-089 AC2: chuyển các tranh chấp quá 48 giờ chủ bãi chưa phản hồi cho nền tảng xử lý.</summary>
public interface IComplaintEscalationService
{
    /// <summary>Số ticket đã chuyển Escalated trong lượt chạy này.</summary>
    Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default);
}
