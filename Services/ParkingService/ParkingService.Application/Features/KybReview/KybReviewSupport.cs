using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybReview;

/// <summary>Bước dùng chung của các use case Admin KYB: kiểm tra tham số, nạp hồ sơ, trả chi tiết sau khi lưu.</summary>
internal static class KybReviewSupport
{
    public static void EnsureIds(int id, int adminUserId)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (adminUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    public static async Task<KybApplication> LoadAsync(IKybReviewRepository repository, int id, CancellationToken cancellationToken)
        => await repository.GetForReviewAsync(id, cancellationToken) ?? throw new NotFoundException("Hồ sơ KYB", id);

    public static async Task<AdminKybApplicationDetailDto> ReloadAsync(IAdminKybQueries queries, int id, CancellationToken cancellationToken)
        => (await queries.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Hồ sơ KYB", id)).ToDto();

    /// <summary>Chuẩn hóa thời điểm client gửi về UTC (không có Kind = coi là UTC, đúng hợp đồng *Utc của API).</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
