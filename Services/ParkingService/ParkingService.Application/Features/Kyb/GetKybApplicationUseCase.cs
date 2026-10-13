using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Kyb;

public interface IGetKybApplicationUseCase
{
    Task<KybApplicationDto> ExecuteAsync(GetKybApplicationQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xem thông tin hồ sơ thẩm định KYB của bãi đỗ xe (US-054).
/// Nếu bãi chưa từng có hồ sơ, trả về DTO nháp mặc định với quy tắc khảo sát thực địa:
/// FieldSurveyRequired = (lot.TotalSlots > 50) mà không ghi CSDL (tuân thủ nguyên tắc Safe & Idempotent của HTTP GET).
/// </summary>
public sealed class GetKybApplicationUseCase(
    IKybApplicationRepository kybRepository,
    IParkingLotManagementRepository lotRepository,
    ILogger<GetKybApplicationUseCase> logger) : IGetKybApplicationUseCase
{
    public async Task<KybApplicationDto> ExecuteAsync(GetKybApplicationQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (query.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        var lot = await lotRepository.GetByIdAsync(query.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", query.ParkingLotId);

        if (lot.OwnerProfileId != query.OwnerProfileId)
            throw new ConflictException("Bạn không có quyền truy cập hồ sơ KYB của bãi đỗ này.");

        var existing = await kybRepository.GetLatestByLotIdAsync(query.ParkingLotId, cancellationToken);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        // Tự động trả về thông tin hồ sơ Draft ban đầu nếu bãi chưa có hồ sơ (chỉ đọc, không ghi DB để tránh duplicate/side-effect trong HTTP GET)
        logger.LogInformation("Bãi đỗ Id={LotId} chưa có hồ sơ KYB, trả về DTO Draft mặc định (FieldSurveyRequired={Required})",
            lot.Id, lot.TotalSlots > 50);

        return new KybApplicationDto(
            Id: 0,
            ParkingLotId: lot.Id,
            Status: KybStatus.Draft.ToString(),
            BusinessLicenseUrl: null,
            SitePhotoUrls: [],
            PhotoLatitude: null,
            PhotoLongitude: null,
            FireSafetyCertificateUrl: null,
            FieldSurveyRequired: lot.TotalSlots > 50,
            FieldSurveyAtUtc: null,
            FieldSurveyNote: null,
            SubmittedAtUtc: null,
            ReviewedAtUtc: null,
            RejectReason: null);
    }
}
