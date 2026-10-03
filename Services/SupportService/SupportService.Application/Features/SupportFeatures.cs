using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace SupportService.Application.Features;

// ===== DTO =====
public sealed record ComplaintDto(int Id, string Code, string Status, string Category, int UserId, string? BookingCode,
    int ParkingLotId, string Description, decimal? RequestedAmount, DateTime? OwnerResponseDueAtUtc, string? OwnerResponse, DateTime CreatedAtUtc);

public sealed record ReviewDto(int Id, string ReviewerName, byte Rating, string? Comment, string? OwnerReply, DateTime CreatedAtUtc);

public sealed record LotReviewsDto(int ParkingLotId, double AverageRating, int ReviewCount, IReadOnlyDictionary<int, int> StarBreakdown,
    IReadOnlyList<ReviewDto> Latest);

// ===== PORT =====
public interface ISupportQueries
{
    Task<IReadOnlyList<ComplaintDto>> ListComplaintsByOwnerAsync(int ownerProfileId, ComplaintStatus? status, CancellationToken cancellationToken);
    Task<LotReviewsDto> GetLotReviewsAsync(int parkingLotId, int take, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface IListOwnerComplaintsUseCase
{
    Task<IReadOnlyList<ComplaintDto>> ExecuteAsync(int ownerProfileId, ComplaintStatus? status, CancellationToken cancellationToken = default);
}

/// <summary>UC-38: Hộp tranh chấp của chủ bãi – khiếu nại chờ phản hồi đứng đầu, sắp quá hạn 48h trước.</summary>
public sealed class ListOwnerComplaintsUseCase(ISupportQueries queries) : IListOwnerComplaintsUseCase
{
    public Task<IReadOnlyList<ComplaintDto>> ExecuteAsync(int ownerProfileId, ComplaintStatus? status, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        return queries.ListComplaintsByOwnerAsync(ownerProfileId, status, cancellationToken);
    }
}

public interface IGetLotReviewsUseCase
{
    Task<LotReviewsDto> ExecuteAsync(int parkingLotId, int take = 10, CancellationToken cancellationToken = default);
}

/// <summary>UC-18: Điểm trung bình + phân bố sao + đánh giá mới nhất của 1 bãi (ẩn review bị Admin ẩn).</summary>
public sealed class GetLotReviewsUseCase(ISupportQueries queries) : IGetLotReviewsUseCase
{
    public Task<LotReviewsDto> ExecuteAsync(int parkingLotId, int take = 10, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        return queries.GetLotReviewsAsync(parkingLotId, Math.Clamp(take, 1, 50), cancellationToken);
    }
}
