using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybMisdeclaration;

// ===== DTO trả về (US-056) =====

/// <summary>Bãi đang xử lý khai báo sai (đọc cả khi Suspended).</summary>
public sealed record MisdeclarationLotDto(int Id, string Name, string Address, int OwnerProfileId, string Status, bool IsPubliclyVisible);

/// <summary>Một case khai báo sai. IsOverdue: case đang mở đã quá DueAtUtc (chỉ gắn cờ, không tự xử lý).</summary>
public sealed record LotMisdeclarationCaseDto(int Id, string Severity, string Category, string Status, string Finding,
    DateTime DueAtUtc, bool IsOverdue, int OpenedByUserId, DateTime OpenedAtUtc, string? EscalationNote, DateTime? EscalatedAtUtc,
    int? ClosedByUserId, DateTime? ClosedAtUtc, string? ResolutionNote);

/// <summary>
/// Màn xử lý khai báo sai của một bãi: bãi, case đang mở (tối đa 1) và lịch sử case (mới trước).
/// MinorCorrectionDays / ResponseWindowHours: giá trị mặc định cho form của Admin.
/// </summary>
public sealed record LotMisdeclarationDto(MisdeclarationLotDto ParkingLot, LotMisdeclarationCaseDto? OpenCase,
    IReadOnlyList<LotMisdeclarationCaseDto> Cases, int MinorCorrectionDays, int ResponseWindowHours);

// ===== Request (người thao tác lấy từ JWT, không nằm trong body) =====

/// <summary>
/// Mở case: Severity = Minor (cảnh báo, hạn sửa CorrectionDays 1–30, mặc định theo cấu hình) hoặc Suspected (tạm khóa,
/// hạn giải trình 24h cố định – không nhận CorrectionDays). Category: Address | Photos | Capacity | Documents | Other.
/// </summary>
public sealed record OpenLotMisdeclarationCaseRequest(string? Severity, string? Category, string? Finding, int? CorrectionDays = null);

public sealed record EscalateLotMisdeclarationCaseRequest(string? Note);

public sealed record ResolveLotMisdeclarationCaseRequest(string? Note);

/// <summary>Xác nhận cố ý gian lận (AC2) – lý do bắt buộc, ghi vào ResolutionNote của các case bị đóng và audit.</summary>
public sealed record BanLotOwnerRequest(string? Reason);

/// <summary>
/// Kết quả cấm chủ bãi (US-056 AC2): màn xử lý của bãi gốc + các bãi bị khóa vĩnh viễn, hồ sơ KYB bị từ chối, case bị đóng.
/// Khóa tài khoản chủ bãi KHÔNG làm ở đây – Admin dùng Lock owner (US-096, AdminService).
/// </summary>
public sealed record OwnerBanResultDto(LotMisdeclarationDto Lot, int OwnerProfileId, IReadOnlyList<int> SuspendedLotIds,
    IReadOnlyList<int> RejectedKybApplicationIds, IReadOnlyList<int> BannedCaseIds);

// ===== Cấu hình =====

/// <summary>Cấu hình "KybMisdeclaration" (appsettings): số ngày hạn sửa mặc định cho sai sót nhẹ (AC3), 1–30, mặc định 7.</summary>
public sealed record KybMisdeclarationSettings
{
    public const string SectionName = "KybMisdeclaration";
    public const int DefaultMinorCorrectionDays = 7;

    public int MinorCorrectionDays { get; }

    public KybMisdeclarationSettings(int minorCorrectionDays = DefaultMinorCorrectionDays)
    {
        if (minorCorrectionDays is < LotMisdeclarationCase.MinCorrectionDays or > LotMisdeclarationCase.MaxCorrectionDays)
            throw new ValidationException($"{SectionName}:MinorCorrectionDays phải từ {LotMisdeclarationCase.MinCorrectionDays} " +
                                          $"đến {LotMisdeclarationCase.MaxCorrectionDays}.");
        MinorCorrectionDays = minorCorrectionDays;
    }
}
