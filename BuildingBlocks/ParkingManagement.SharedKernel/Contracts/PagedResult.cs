namespace ParkingManagement.SharedKernel.Contracts;

/// <summary>Kết quả phân trang dùng chung cho mọi API danh sách.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
