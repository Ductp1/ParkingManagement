using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== DTO =====

/// <summary>Dòng danh sách hồ sơ đối tác (A1).</summary>
public sealed record PartnerApplicationSummaryDto(int Id, string Code, string BusinessName, string TaxCode, string FullName,
    string Email, string Phone, int LotCount, string Status, int? ReviewerAdminId, string? ContractNumber, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Một phiên bản hợp đồng tự sinh đã gửi (T5 / T5b).</summary>
public sealed record ContractDraftDto(int Id, int Version, string ContractNumber, int TemplateVersion, decimal CommissionRate,
    DateOnly EffectiveFrom, DateOnly EffectiveTo, int TermMonths, string BankBin, string BankName, string BankAccountNumber,
    string BankAccountName, string FileContentType, string FileSha256, string Status, string? RevisionReason, DateTime SentAtUtc,
    string SentToEmail, int CreatedByAdminId);

/// <summary>
/// Chi tiết hồ sơ đối tác (A2). NextStatuses = trạng thái có thể chuyển tới theo bảng T2–T10 (giao diện bật/tắt nút);
/// ContractDrafts = mọi phiên bản hợp đồng, mới nhất trước.
/// </summary>
public sealed record PartnerApplicationDetailDto(
    int Id, string Code, string Status, string FullName, string Email, string Phone, string? Position, string BusinessType,
    string BusinessName, string TaxCode, string BusinessAddress, string LegalRepresentativeName, string? LegalRepresentativePosition,
    string? BusinessLicenseUrl, int LotCount, string PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots,
    string? OperatingHours, DateTime ConsentAcceptedAtUtc, int? ReviewerAdminId, string? InfoRequestNote, string? RejectReason,
    string? CancelReason, string? ContractNumber, int? CreatedUserId, int? CreatedOwnerProfileId, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc, IReadOnlyList<string> NextStatuses, IReadOnlyList<ContractDraftDto> ContractDrafts);

// ===== PORT (đọc) =====

public interface IPartnerApplicationQueries
{
    /// <summary>Lọc trạng thái, tìm theo mã / tên doanh nghiệp / MST / email / SĐT, mới nhất trước.</summary>
    Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken);

    Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>Thông tin file của một phiên bản hợp đồng (A8); null khi hồ sơ / phiên bản không tồn tại.</summary>
    Task<ContractDraftFileInfo?> GetContractFileAsync(int id, int version, CancellationToken cancellationToken);
}

/// <summary>Vị trí + SHA-256 của file hợp đồng một phiên bản.</summary>
public sealed record ContractDraftFileInfo(string ContractNumber, int Version, string FileKey, string FileContentType, string FileSha256);

// ===== USE CASE (đọc) =====

public interface IListPartnerApplicationsUseCase
{
    Task<PagedResult<PartnerApplicationSummaryDto>> ExecuteAsync(PartnerApplicationStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A1): Admin xem danh sách hồ sơ đăng ký đối tác.</summary>
public sealed class ListPartnerApplicationsUseCase(IPartnerApplicationQueries queries) : IListPartnerApplicationsUseCase
{
    public Task<PagedResult<PartnerApplicationSummaryDto>> ExecuteAsync(PartnerApplicationStatus? status, string? search, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is { Length: > 200 }) throw new ValidationException("Từ khóa tìm kiếm tối đa 200 ký tự.");
        return queries.ListAsync(status, term, page, pageSize, cancellationToken);
    }
}

public interface IGetPartnerApplicationUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>US-116 (A2): Admin xem chi tiết hồ sơ đối tác kèm các phiên bản hợp đồng.</summary>
public sealed class GetPartnerApplicationUseCase(IPartnerApplicationQueries queries) : IGetPartnerApplicationUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Hồ sơ đối tác", id);
    }
}
