using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Application.Features.Settlements;

// ===== Cấu hình =====

/// <summary>
/// "Settlements": { "BiweeklyAnchor": "2026-01-05", "MaxCatchUpPeriods": 60, ... } – BiweeklyAnchor phải là Thứ Hai.
/// MaxCatchUpPeriods chặn số kỳ được tạo trong một lượt (service tắt lâu ngày không sinh hàng trăm kỳ một lúc).
/// </summary>
public sealed record SettlementOptions(DateOnly BiweeklyAnchor, int MaxCatchUpPeriods = SettlementOptions.DefaultMaxCatchUpPeriods)
{
    public const int DefaultMaxCatchUpPeriods = 60;
    public static readonly DateOnly DefaultBiweeklyAnchor = new(2026, 1, 5);
}

// ===== Nguồn ngoài (lỗi → DependencyUnavailableException: không sinh / không tính, lượt sau thử lại, không đoán số) =====

/// <summary>Giá trị SETTLEMENT_CYCLE có hiệu lực tại một thời điểm (AdminService, khóa nội bộ).</summary>
public sealed record SettlementCycleQuote(string Value, DateTime? EffectiveFromUtc);

public interface ISettlementCycleClient
{
    Task<SettlementCycleQuote> ResolveAsync(DateTime atUtc, CancellationToken cancellationToken);
}

/// <summary>Trạng thái booking (BookingService). StatusSinceUtc = lúc chuyển sang trạng thái hiện tại (lịch sử), null khi không có lịch sử.</summary>
public sealed record BookingStatusInfo(bool Found, string? Status, DateTime? StatusSinceUtc)
{
    public static readonly BookingStatusInfo NotFound = new(false, null, null);
}

public interface IBookingStatusClient
{
    /// <summary>Trạng thái của từng mã booking; mã không tồn tại → <see cref="BookingStatusInfo.NotFound"/>.</summary>
    Task<IReadOnlyDictionary<string, BookingStatusInfo>> GetStatusesAsync(IReadOnlyCollection<string> bookingCodes,
        CancellationToken cancellationToken);
}

/// <summary>Khoản giữ đang mở do tranh chấp (SupportService GET /api/v1/complaints/payout-holds).</summary>
public sealed record PayoutHoldItem(string ComplaintCode, string Status, int ParkingLotId, int? OwnerProfileId, string? BookingCode,
    decimal Amount, DateTime HeldSinceUtc);

public interface IPayoutHoldClient
{
    /// <summary>Chỉ gọi được trong request của Admin (chuyển tiếp JWT Admin); null = mọi chủ bãi.</summary>
    Task<IReadOnlyList<PayoutHoldItem>> ListOpenHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken);
}

// ===== Dữ liệu đọc từ pm_payment cho một chủ bãi =====

/// <summary>Giao dịch thu tiền trước mốc chốt chưa nằm ở kỳ nào.</summary>
public sealed record UnsettledPaymentRow(int PaymentId, string Code, string? BookingCode, int ParkingLotId, PaymentPurpose Purpose,
    PaymentMethod Method, decimal Amount, DateTime PaidAtUtc);

/// <summary>Lần hoàn Completed trước mốc chốt chưa nằm ở kỳ nào; PaymentSettled = giao dịch gốc đã ở một kỳ khác.</summary>
public sealed record UnsettledRefundRow(int RefundId, int PaymentId, decimal Amount, DateTime ProcessedAtUtc, bool PaymentSettled);

public sealed record VoucherRow(string Code, decimal Amount, int IssuedByUserId, VoucherStatus Status, DateTime? UpdatedAtUtc);

/// <summary>Kỳ khác của chủ bãi liên quan tới kỳ đang sinh.</summary>
public sealed record OwnerSettlementState(int? ExistingId, SettlementStatus? ExistingStatus, bool HasOpenEarlier, bool HasLater);

/// <summary>Mọi dữ liệu pm_payment cần để tính kỳ của một chủ bãi (đã loại trừ chính kỳ đang tính lại).</summary>
public sealed record SettlementSnapshot(
    IReadOnlyList<UnsettledPaymentRow> Payments,
    IReadOnlyList<UnsettledRefundRow> Refunds,
    IReadOnlyList<SettledPaymentInfo> SettledPayments,
    IReadOnlyList<PaymentAccrual> Accruals,
    IReadOnlyList<PriorRefundState> PriorRefunds,
    IReadOnlyList<HeldElsewhereInput> HeldElsewhere,
    IReadOnlyList<VoucherRow> Vouchers,
    IReadOnlyList<VoucherChargeState> VoucherCharges,
    IReadOnlyList<AttachableAdjustment> Adjustments,
    CarryOverInput? CarryOver);

// ===== PORT ghi / đọc =====

/// <summary>Ghi trùng với dữ liệu instance / thao tác khác vừa ghi (PostgreSQL 23505 trên các index duy nhất của quyết toán).</summary>
public sealed class SettlementWriteConflictException(string message, Exception? inner = null) : Exception(message, inner);

public interface ISettlementGenerationRepository
{
    /// <summary>Khóa một lượt sinh kỳ giữa nhiều instance / giữa job và Admin (pg_try_advisory_lock). null = đang có lượt khác.</summary>
    Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken);

    Task<SettlementPeriod?> GetLatestPeriodAsync(CancellationToken cancellationToken);
    Task<SettlementPeriod?> FindPeriodAsync(DateOnly periodStart, CancellationToken cancellationToken);
    /// <summary>Thêm kỳ vào lịch; trùng ngày bắt đầu (instance khác vừa thêm) → trả kỳ đã có.</summary>
    Task<SettlementPeriod> AddPeriodAsync(SettlementPeriod period, CancellationToken cancellationToken);

    /// <summary>Chủ bãi có thứ cần quyết toán trước mốc chốt (giao dịch / hoàn tiền chưa quyết toán, điều chỉnh đã duyệt chưa gắn kỳ,
    /// voucher chưa trừ, số âm chưa mang sang, khoản giữ chưa giải phóng).</summary>
    Task<IReadOnlyList<int>> ListCandidateOwnerIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    Task<OwnerSettlementState> GetOwnerStateAsync(int ownerProfileId, DateOnly periodStart, CancellationToken cancellationToken);

    Task<SettlementSnapshot> LoadSnapshotAsync(int ownerProfileId, DateTime cutoffUtc, DateTime nowUtc, int? excludeSettlementId,
        CancellationToken cancellationToken);

    /// <summary>Kỳ theo Id (không theo dõi) – để kiểm tra trước khi tính lại.</summary>
    Task<Settlement?> FindSettlementAsync(int settlementId, CancellationToken cancellationToken);

    /// <summary>Thêm kỳ mới + bút toán voucher mới + gắn bút toán đã duyệt + audit trong MỘT SaveChanges. 23505 → SettlementWriteConflictException.</summary>
    Task AddAsync(Settlement settlement, IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds,
        OutboxMessage audit, CancellationToken cancellationToken);

    /// <summary>
    /// Tính lại kỳ Draft trong một transaction: xóa dòng cũ + gỡ bút toán khỏi kỳ (kiểm tra RowVersion → ConflictException),
    /// rồi <paramref name="apply"/> ghi kết quả mới, thêm bút toán voucher mới, gắn bút toán đã duyệt và audit.
    /// </summary>
    Task<Settlement> ReplaceDraftAsync(int settlementId, byte[] expectedRowVersion, Action<Settlement> apply,
        IReadOnlyList<FinancialAdjustment> newAdjustments, IReadOnlyList<int> attachAdjustmentIds, Func<Settlement, OutboxMessage> audit,
        CancellationToken cancellationToken);
}

// ===== DTO kết quả =====

/// <summary>Kết quả cho một chủ bãi: Outcome = Created | Regenerated | Skipped (Reason cho biết vì sao).</summary>
public sealed record OwnerSettlementOutcomeDto(int OwnerProfileId, string Outcome, string? Reason, int? SettlementId, string? Code,
    decimal? NetPayout, bool? IsBlocked, IReadOnlyList<string> BlockedReasons, string? RowVersion);

public sealed record SettlementGenerationResultDto(DateOnly PeriodStart, DateOnly PeriodEnd, string Cycle,
    IReadOnlyList<OwnerSettlementOutcomeDto> Owners);

public static class SettlementOutcomes
{
    public const string Created = "Created";
    public const string Regenerated = "Regenerated";
    public const string Skipped = "Skipped";

    /// <summary>Không có gì để quyết toán (booking chưa kết thúc, không có phát sinh).</summary>
    public const string NothingToSettle = "NothingToSettle";
    /// <summary>Chủ bãi còn kỳ trước ở Draft / PendingReview – phải duyệt xong mới sinh kỳ sau (số mang sang, khoản giữ đúng thứ tự).</summary>
    public const string OpenEarlierSettlement = "OpenEarlierSettlement";
    /// <summary>Đã có kỳ sau kỳ được chọn.</summary>
    public const string LaterSettlementExists = "LaterSettlementExists";
    /// <summary>Đã có kỳ này (job không tính lại Draft – có thể làm mất khoản giữ Admin đã nạp).</summary>
    public const string AlreadyGenerated = "AlreadyGenerated";
    /// <summary>Kỳ này đã gửi duyệt / duyệt / chi – bất biến.</summary>
    public const string NotDraft = "NotDraft";
    /// <summary>Instance khác vừa ghi cùng dữ liệu (23505) – lượt sau sẽ thấy.</summary>
    public const string Conflict = "Conflict";
}
