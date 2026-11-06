using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IGetAdminKybApplicationUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC1: chi tiết hồ sơ (giấy tờ, ảnh + so khớp GPS, PCCC, khảo sát) kèm bãi đang chờ duyệt.</summary>
public sealed class GetAdminKybApplicationUseCase(IAdminKybQueries queries) : IGetAdminKybApplicationUseCase
{
    public Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        return KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
