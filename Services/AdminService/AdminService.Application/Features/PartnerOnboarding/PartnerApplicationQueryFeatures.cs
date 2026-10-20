using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== DTO =====

/// <summary>Dòng danh sách hồ sơ đối tác (A1).</summary>
public sealed record PartnerApplicationSummaryDto(int Id, string Code, string BusinessName, string TaxCode, string FullName,
    string Email, string Phone, int LotCount, string Status, int? ReviewerAdminId, int? CreatedOwnerProfileId, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Tài khoản nhận thanh toán mặc định của đối tác (nhập ở T6).</summary>
public sealed record PartnerBankAccountDto(string Bin, string Name, string AccountNumber, string AccountName);

/// <summary>
/// Chi tiết hồ sơ đối tác (A2). NextStatuses = trạng thái có thể chuyển tới theo bảng T2–T10 (giao diện bật/tắt nút);
/// PayoutBank = null khi chưa cấp tài khoản. Hợp đồng nằm ở từng bãi (api/v1/admin/lot-contracts).
/// </summary>
public sealed record PartnerApplicationDetailDto(
    int Id, string Code, string Status, string FullName, string Email, string Phone, string? Position, string BusinessType,
    string BusinessName, string TaxCode, string BusinessAddress, string LegalRepresentativeName, string? LegalRepresentativePosition,
    string? BusinessLicenseUrl, int LotCount, string PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots,
    string? OperatingHours, DateTime ConsentAcceptedAtUtc, int? ReviewerAdminId, string? InfoRequestNote, string? RejectReason,
    string? CancelReason, PartnerBankAccountDto? PayoutBank, int? CreatedUserId, int? CreatedOwnerProfileId, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc, IReadOnlyList<string> NextStatuses);

// ===== PORT (đọc) =====

public interface IPartnerApplicationQueries
{
    /// <summary>Lọc trạng thái, tìm theo mã / tên doanh nghiệp / MST / email / SĐT, mới nhất trước.</summary>
    Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken);

    Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken);
}

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
