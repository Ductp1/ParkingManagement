using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Services;

namespace ParkingService.Application.Features.Kyb;

public interface ISubmitKybApplicationUseCase
{
    Task<KybApplicationDto> ExecuteAsync(SubmitKybApplicationCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Nộp hồ sơ thẩm định KYB 4 bước chính thức (US-054).
/// Kiểm tra nghiêm ngặt tính đầy đủ và hợp lệ của cả 4 bước:
/// Bước 1: Giấy phép đăng ký kinh doanh.
/// Bước 2: Ảnh hiện trường bãi đỗ và tọa độ GPS khớp với vị trí bãi (<= 500m).
/// Bước 3: Giấy chứng nhận đủ điều kiện an toàn PCCC.
/// Bước 4: Lịch hẹn khảo sát thực địa đối với bãi có quy mô > 50 chỗ.
/// </summary>
public sealed class SubmitKybApplicationUseCase(
    IKybApplicationRepository kybRepository,
    IParkingLotManagementRepository lotRepository,
    ILogger<SubmitKybApplicationUseCase> logger) : ISubmitKybApplicationUseCase
{
    public async Task<KybApplicationDto> ExecuteAsync(SubmitKybApplicationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        var lot = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", command.ParkingLotId);

        if (lot.OwnerProfileId != command.OwnerProfileId)
            throw new ConflictException("Bạn không có quyền nộp hồ sơ KYB cho bãi đỗ này.");

        var app = await kybRepository.GetLatestByLotIdAsync(command.ParkingLotId, cancellationToken);
        var isNew = app is null;

        if (app is not null)
        {
            if (app.Status is KybStatus.Submitted or KybStatus.UnderReview)
                throw new ConflictException("Hồ sơ đang trong quá trình thẩm định, không thể nộp lại.");

            if (app.Status == KybStatus.Approved)
                throw new ConflictException("Bãi đỗ này đã được duyệt KYB thành công.");
        }

        // ==================== VALIDATE 4 BƯỚC THẨM ĐỊNH ====================

        // Bước 1: Pháp lý
        if (string.IsNullOrWhiteSpace(command.BusinessLicenseUrl))
            throw new ValidationException("Bước 1: Giấy phép đăng ký kinh doanh không được để trống.");

        // Bước 2: Địa điểm & Hiện trường
        var validPhotos = command.SitePhotoUrls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .ToList();

        if (validPhotos.Count == 0)
            throw new ValidationException("Bước 2: Cần cung cấp tối thiểu 1 ảnh hiện trường bãi đỗ.");

        if (command.PhotoLatitude.HasValue && command.PhotoLongitude.HasValue)
        {
            if (command.PhotoLatitude is < -90 or > 90 || command.PhotoLongitude is < -180 or > 180)
                throw new ValidationException("Bước 2: Tọa độ GPS của ảnh không hợp lệ.");

            var distanceKm = GeoDistance.HaversineKm(lot.Latitude, lot.Longitude, command.PhotoLatitude.Value, command.PhotoLongitude.Value);
            if (distanceKm > 0.5)
            {
                throw new ValidationException($"Bước 2: Tọa độ GPS của ảnh cách vị trí bãi đỗ {distanceKm:F2} km (vượt quá bán kính cho phép 500 m).");
            }
        }

        // Bước 3: PCCC
        if (string.IsNullOrWhiteSpace(command.FireSafetyCertificateUrl))
            throw new ValidationException("Bước 3: Giấy chứng nhận an toàn PCCC không được để trống.");

        // Bước 4: Khảo sát thực địa (bắt buộc nếu bãi > 50 chỗ)
        var fieldSurveyRequired = lot.TotalSlots > 50;
        if (fieldSurveyRequired)
        {
            if (!command.FieldSurveyAtUtc.HasValue)
                throw new ValidationException("Bước 4: Bãi đỗ có quy mô trên 50 chỗ bắt buộc phải đặt lịch khảo sát thực địa.");

            if (command.FieldSurveyAtUtc.Value <= DateTime.UtcNow)
                throw new ValidationException("Bước 4: Thời gian hẹn khảo sát thực địa phải ở trong tương lai.");
        }

        // ==================== CẬP NHẬT TRẠNG THÁI & DỮ LIỆU ====================
        if (app is null)
        {
            app = new KybApplication
            {
                ParkingLotId = lot.Id
            };
        }

        app.Status = KybStatus.Submitted;
        app.BusinessLicenseUrl = command.BusinessLicenseUrl.Trim();
        app.SitePhotoUrlsJson = JsonSerializer.Serialize(validPhotos);
        app.PhotoLatitude = command.PhotoLatitude;
        app.PhotoLongitude = command.PhotoLongitude;
        app.FireSafetyCertificateUrl = command.FireSafetyCertificateUrl.Trim();
        app.FieldSurveyRequired = fieldSurveyRequired;
        app.FieldSurveyAtUtc = command.FieldSurveyAtUtc;
        app.FieldSurveyNote = command.FieldSurveyNote?.Trim();
        app.SubmittedAtUtc = DateTime.UtcNow;
        app.RejectReason = null; // Đặt lại lý do từ chối nếu nộp lại

        if (isNew)
        {
            await kybRepository.AddAsync(app, cancellationToken);
        }
        else
        {
            await kybRepository.UpdateAsync(app, cancellationToken);
        }

        await kybRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Chủ bãi {OwnerId} đã nộp chính thức hồ sơ KYB Id={AppId} cho bãi Id={LotId}",
            command.OwnerProfileId, app.Id, lot.Id);

        return app.ToDto();
    }
}
