using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IListAdminKybApplicationsUseCase
{
    Task<PagedResult<AdminKybApplicationSummaryDto>> ExecuteAsync(KybStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC1: danh sách hồ sơ KYB cho Admin – lọc trạng thái, tìm tên / địa chỉ bãi, phân trang.</summary>
public sealed class ListAdminKybApplicationsUseCase(IAdminKybQueries queries) : IListAdminKybApplicationsUseCase
{
    public async Task<PagedResult<AdminKybApplicationSummaryDto>> ExecuteAsync(KybStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is { Length: > 200 }) throw new ValidationException("Từ khóa tìm kiếm tối đa 200 ký tự.");

        var rows = await queries.ListAsync(status, term, page, pageSize, cancellationToken);
        return new PagedResult<AdminKybApplicationSummaryDto>(rows.Items.Select(r => r.ToDto()).ToList(), rows.Page, rows.PageSize,
            rows.TotalCount);
    }
}
