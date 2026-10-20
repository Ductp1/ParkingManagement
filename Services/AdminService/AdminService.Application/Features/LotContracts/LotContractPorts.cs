using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Application.Features.LotContracts;

// ===== DTO =====

/// <summary>Dòng danh sách hợp đồng bãi (L0). Điều khoản lấy từ phiên bản mới nhất.</summary>
public sealed record LotContractSummaryDto(int Id, string ContractNumber, string Status, int ParkingLotId, int KybApplicationId,
    string LotName, string LotCity, int OwnerProfileId, string BusinessName, int? LatestVersion, decimal? CommissionRate,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>Một phiên bản hợp đồng bãi (v1 khi lập, v+1 khi sửa).</summary>
public sealed record LotContractVersionDto(int Id, int Version, int TemplateVersion, decimal CommissionRate, DateOnly EffectiveFrom,
    DateOnly EffectiveTo, int TermMonths, string FileContentType, string FileSha256, string Status, string? RevisionReason,
    DateTime CreatedAtUtc, int CreatedByAdminId);

/// <summary>Chi tiết hợp đồng bãi (L0b): ảnh chụp bãi + Bên B lúc lập, trạng thái ký, các phiên bản (mới nhất trước).</summary>
public sealed record LotContractDetailDto(
    int Id, string ContractNumber, string Status, int ParkingLotId, int KybApplicationId, string LotName, string LotAddress,
    string LotCity, string? LotDistrict, int LotTotalSlots, string LotOpeningHours, int OwnerProfileId, int PartnerApplicationId,
    string ApplicationCode, string BusinessName, string TaxCode, string BusinessAddress, string LegalRepresentativeName,
    string? LegalRepresentativePosition, string Email, string Phone, PartnerBankAccountDto Bank, string? SignedFileContentType,
    string? SignedFileSha256, DateTime? SignedAtUtc, string? CancelReason, int CreatedByAdminId, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc, IReadOnlyList<LotContractVersionDto> Versions);

/// <summary>Vị trí + SHA-256 của file một phiên bản.</summary>
public sealed record LotContractVersionFileInfo(string ContractNumber, int Version, string FileKey, string FileContentType, string FileSha256);

// ===== PORT: ParkingService (qua Gateway, chuyển tiếp JWT Admin) =====

/// <summary>Hồ sơ KYB + bãi đỗ đọc từ ParkingService (GET api/v1/parking-lots/kyb-applications/{id}).</summary>
public sealed record KybLotInfo(int KybApplicationId, string KybStatus, int ParkingLotId, string LotName, string LotAddress,
    string LotCity, string? LotDistrict, int TotalSlots, TimeOnly OpenTime, TimeOnly CloseTime, string LotStatus, int OwnerProfileId);

/// <summary>
/// US-116: cổng gọi ParkingService. Lỗi: 401 / 403 → AuthenticationException / ForbiddenException, 409 → ConflictException
/// (thông điệp của ParkingService), không gọi được hoặc ParkingService chưa có endpoint → DependencyUnavailableException (503).
/// </summary>
public interface IParkingServiceClient
{
    /// <summary>Null khi ParkingService báo không có hồ sơ KYB.</summary>
    Task<KybLotInfo?> GetKybAsync(int kybApplicationId, CancellationToken cancellationToken);

    /// <summary>Kích hoạt bãi sau khi ký hợp đồng (idempotent: bãi đã Active thì vẫn thành công).</summary>
    Task ActivateLotAsync(int kybApplicationId, string contractNumber, CancellationToken cancellationToken);
}

// ===== PORT (ghi) =====

public interface ILotContractRepository
{
    /// <summary>Hợp đồng có tracking, kèm các phiên bản.</summary>
    Task<LotContract?> FindTrackedAsync(int id, CancellationToken cancellationToken);

    /// <summary>Hồ sơ đối tác đã cấp tài khoản cho chủ bãi này (AccountIssued / Activated), null nếu không có.</summary>
    Task<PartnerApplication?> FindPartnerByOwnerProfileAsync(int ownerProfileId, CancellationToken cancellationToken);

    /// <summary>Bãi đã có hợp đồng chưa hủy (Pending / Signed / Terminated).</summary>
    Task<bool> HasOpenContractAsync(int parkingLotId, CancellationToken cancellationToken);

    /// <summary>Số hợp đồng đã lập cho bãi (kể cả đã hủy) – để đặt hậu tố số hợp đồng khi lập lại.</summary>
    Task<int> CountForLotAsync(int parkingLotId, CancellationToken cancellationToken);

    /// <summary>Mẫu hợp đồng đang áp dụng (IsActive), null nếu chưa có.</summary>
    Task<ContractTemplate?> GetActiveTemplateAsync(CancellationToken cancellationToken);

    void Add(LotContract contract);

    /// <summary>Ghi thay đổi đang tracking + audit log trong CÙNG 1 lần SaveChanges. RowVersion lệch → ConflictException.</summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

// ===== PORT (đọc) =====

public interface ILotContractQueries
{
    /// <summary>Lọc trạng thái / bãi / chủ bãi, tìm theo số hợp đồng / tên bãi / tên doanh nghiệp, mới nhất trước.</summary>
    Task<PagedResult<LotContractSummaryDto>> ListAsync(LotContractStatus? status, int? parkingLotId, int? ownerProfileId, string? search,
        int page, int pageSize, CancellationToken cancellationToken);

    Task<LotContractDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken);

    Task<LotContractVersionFileInfo?> GetVersionFileAsync(int id, int version, CancellationToken cancellationToken);
}
