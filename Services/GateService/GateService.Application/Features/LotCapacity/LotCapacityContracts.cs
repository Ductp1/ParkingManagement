using GateService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.LotCapacity;

// =====================================================================
// US-066 – Cấu hình sức chứa & vùng đệm vãng lai: hợp đồng + validation.
// C = Capacity (tổng chỗ), B = TransientBuffer (trần tối đa xe vãng lai cùng lúc),
// Q = SubscriberQuota (hạn mức thuê bao hiệu lực). C - B - Q = vùng linh hoạt.
// Runtime: T = tổng xe đang đỗ, V = vãng lai đang đỗ, A = thuê bao hiệu lực.
// =====================================================================

/// <summary>Lỗi 1 field khi lưu cấu hình (400): mã lỗi ổn định + thông điệp tiếng Việt.</summary>
public sealed record CapacityFieldError(string Code, string Message);

/// <summary>
/// 400 kèm lỗi THEO TỪNG FIELD (một lỗi cần sửa trước cho mỗi ô) và/hoặc lỗi TỔNG HỢP
/// (không gắn field) — middleware API chuyển thành ProblemDetails:
/// extensions["errors"] = { field: [{code, message}] }, extensions["summary"] = { code, message }.
/// </summary>
public sealed class CapacityValidationException(
    IReadOnlyDictionary<string, CapacityFieldError[]> errors,
    CapacityFieldError? summary = null)
    : Exception(summary?.Message ?? "Cấu hình sức chứa không hợp lệ.")
{
    public IReadOnlyDictionary<string, CapacityFieldError[]> Errors { get; } = errors;
    public CapacityFieldError? Summary { get; } = summary;
}

/// <summary>409 – admin khác vừa lưu (version gửi lên khác version hiện tại) → errorCode CONFIG_VERSION_CONFLICT.</summary>
public sealed class StaleCapacityConfigVersionException(long expectedVersion, long currentVersion)
    : Exception($"Cấu hình đã được người khác sửa (version hiện tại {currentVersion}, bạn gửi {expectedVersion}). Vui lòng tải lại.");

public static class AdmissionRejectReasons
{
    public const string LotFull = "LOT_FULL";
    public const string TransientBufferFull = "TRANSIENT_BUFFER_FULL";
}

/// <summary>
/// Quyết định cho vào thuần (pre-check để trả lỗi rõ ràng). Authority chống race vẫn là
/// UPDATE nguyên tử có điều kiện trong LotAdmissionGate. Luật: thuê bao vào nếu T &lt; C;
/// vãng lai vào nếu T &lt; C VÀ V &lt; B.
/// </summary>
public static class LotAdmissionPolicy
{
    public static (bool Allowed, string? RejectReason) Decide(
        int totalParked, int transientParked, int capacity, int transientBuffer, AdmissionType type)
    {
        if (totalParked >= capacity)
            return (false, AdmissionRejectReasons.LotFull);
        if (type == AdmissionType.Transient && transientParked >= transientBuffer)
            return (false, AdmissionRejectReasons.TransientBufferFull);
        return (true, null);
    }
}

/// <summary>
/// Validator thuần (luật 1) — matrix unit test chạy trực tiếp trên class này.
/// Nhận int? vì API nhận body: thiếu field (null) → REQUIRED; giá trị sai → INVALID;
/// vượt tổng → EXCEEDS_CAPACITY (gắn vào ô tương ứng); Q + B &gt; C → lỗi TỔNG HỢP (không gắn field).
/// Mỗi ô chỉ giữ MỘT lỗi theo thứ tự ưu tiên: thiếu → sai định dạng → vượt tổng.
/// </summary>
public static class LotCapacityValidator
{
    public const string
        CodeCapacityRequired = "CAPACITY_REQUIRED",
        CodeCapacityInvalid = "CAPACITY_INVALID",
        CodeTransientRequired = "TRANSIENT_REQUIRED",
        CodeTransientInvalid = "TRANSIENT_INVALID",
        CodeTransientExceedsCapacity = "TRANSIENT_EXCEEDS_CAPACITY",
        CodeSubscriberRequired = "SUBSCRIBER_REQUIRED",
        CodeSubscriberInvalid = "SUBSCRIBER_INVALID",
        CodeSubscriberExceedsCapacity = "SUBSCRIBER_EXCEEDS_CAPACITY",
        CodeTotalExceedsCapacity = "TOTAL_EXCEEDS_CAPACITY",
        CodeCapacityBelowOccupancy = "CAPACITY_BELOW_CURRENT_OCCUPANCY";

    public sealed record Outcome(
        IReadOnlyDictionary<string, CapacityFieldError[]> Errors,
        CapacityFieldError? Summary)
    {
        public bool HasErrors => Errors.Count > 0 || Summary is not null;
    }

    public static Outcome Validate(int? capacity, int? transientBuffer, int? subscriberQuota)
    {
        var errors = new Dictionary<string, CapacityFieldError[]>();

        int? c = capacity, b = transientBuffer, q = subscriberQuota;

        // 1) Thiếu (trống) → REQUIRED.
        if (c is null)
            errors["capacity"] = [new CapacityFieldError(CodeCapacityRequired, "Hãy nhập tổng số chỗ của bãi (ví dụ: 100).")];
        if (b is null)
            errors["transientBuffer"] = [new CapacityFieldError(CodeTransientRequired, "Hãy nhập số chỗ cho xe vãng lai. Nhập 0 nếu không nhận.")];
        if (q is null)
            errors["subscriberQuota"] = [new CapacityFieldError(CodeSubscriberRequired, "Hãy nhập số chỗ cho khách đăng ký. Nhập 0 nếu không nhận.")];

        // 2) Sai giá trị (0/âm cho C; âm cho B, Q) → INVALID.
        if (c is not null && c <= 0)
            errors["capacity"] = [new CapacityFieldError(CodeCapacityInvalid, "Tổng số chỗ phải là số nguyên từ 1 trở lên.")];
        if (b is not null && b < 0)
            errors["transientBuffer"] = [new CapacityFieldError(CodeTransientInvalid, "Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.")];
        if (q is not null && q < 0)
            errors["subscriberQuota"] = [new CapacityFieldError(CodeSubscriberInvalid, "Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.")];

        // 3) Vượt tổng (chỉ khi giá trị đang hợp lệ) → gắn vào ô tương ứng.
        if (c is > 0)
        {
            if (b is not null && b >= 0 && b > c)
                errors["transientBuffer"] = [new CapacityFieldError(CodeTransientExceedsCapacity,
                    $"Chỗ cho xe vãng lai ({b}) nhiều hơn tổng số chỗ ({c}). Hãy giảm xuống tối đa {c}.")];
            if (q is not null && q >= 0 && q > c)
                errors["subscriberQuota"] = [new CapacityFieldError(CodeSubscriberExceedsCapacity,
                    $"Chỗ cho khách đăng ký ({q}) nhiều hơn tổng số chỗ ({c}). Hãy giảm xuống tối đa {c}.")];
        }

        // 4) Lỗi TỔNG HỢP: cả ba ô hợp lệ riêng lẻ nhưng B + Q > C → không gắn field.
        CapacityFieldError? summary = null;
        if (errors.Count == 0 && c is not null && b is not null && q is not null && b + q > c)
            summary = new CapacityFieldError(CodeTotalExceedsCapacity,
                $"Vãng lai ({b}) + đăng ký ({q}) = {b + q}, vượt tổng số chỗ {c}. Hãy giảm bớt ít nhất {b + q - c} chỗ.");

        return new Outcome(errors, summary);
    }
}

/// <summary>Snapshot runtime + cấu hình cho GET (luật 5: availableFor* = max(0, ...)).</summary>
public sealed record LotCapacityConfigDto(
    bool Configured,
    int Capacity,
    int TransientBuffer,
    int SubscriberQuota,
    int TotalParked,
    int TransientParked,
    int SubscriberParked,
    int ActiveSubscribers,
    int AvailableForTransient,
    int AvailableForSubscriber,
    long Version,
    DateTime? UpdatedAtUtc);

public sealed record UpdateLotCapacityCommand(
    int ParkingLotId,
    int? Capacity,
    int? TransientBuffer,
    int? SubscriberQuota,
    long Version,
    int? ChangedByUserId = null);

/// <summary>Kết quả PUT: đã lưu kèm warnings[] (luật 7: B &lt; V / Q &lt; A — chặn khách mới tới khi counters tụt xuống).</summary>
public sealed record UpdateLotCapacityResult(
    bool Saved,
    IReadOnlyList<string> Warnings,
    long Version,
    LotCapacityConfigDto Snapshot);