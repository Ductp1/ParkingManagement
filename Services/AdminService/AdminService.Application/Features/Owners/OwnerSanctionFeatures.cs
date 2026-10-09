using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Owners;

// ===== DTO =====
public sealed record SanctionDto(int Id, int OwnerProfileId, int? ParkingLotId, string Level, string Reason, DateTime StartsAtUtc,
    DateTime? EndsAtUtc, string Status, decimal? PenaltyAmount, int IssuedByUserId, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

// ===== PORT (đọc) =====
public interface ISanctionQueries
{
    /// <summary>Chế tài của một chủ bãi (cả cấp chủ bãi lẫn cấp bãi), mới nhất trước.</summary>
    Task<PagedResult<SanctionDto>> ListByOwnerAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken);
}

// ===== USE CASE (đọc) =====
public interface IGetOwnerSanctionsUseCase
{
    Task<PagedResult<SanctionDto>> ExecuteAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-096 (UC-40): Lịch sử vi phạm của chủ bãi = các chế tài đã áp trong pm_admin.
/// Chủ bãi chưa từng bị chế tài (hoặc Id không có thật) trả danh sách rỗng – không gọi UserService để kiểm tra.
/// </summary>
public sealed class GetOwnerSanctionsUseCase(ISanctionQueries queries) : IGetOwnerSanctionsUseCase
{
    public Task<PagedResult<SanctionDto>> ExecuteAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        return queries.ListByOwnerAsync(ownerProfileId, page, pageSize, cancellationToken);
    }
}
