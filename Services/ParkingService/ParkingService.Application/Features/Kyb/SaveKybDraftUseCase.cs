using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Kyb;

public interface ISaveKybDraftUseCase
{
    Task<KybApplicationDto> ExecuteAsync(SaveKybDraftCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lưu nháp tiến độ hồ sơ thẩm định KYB 4 bước (US-054).
/// Cho phép chủ bãi lưu dữ liệu dở dang của từng bước (1..4) mà không bắt buộc điền đủ tất cả các trường.
/// </summary>
public sealed class SaveKybDraftUseCase(
    IKybApplicationRepository kybRepository,
    IParkingLotManagementRepository lotRepository,
    ILogger<SaveKybDraftUseCase> logger) : ISaveKybDraftUseCase
{
    public async Task<KybApplicationDto> ExecuteAsync(SaveKybDraftCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        var lot = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", command.ParkingLotId);

        if (lot.OwnerProfileId != command.OwnerProfileId)
            throw new ConflictException("Bạn không có quyền chỉnh sửa hồ sơ KYB của bãi đỗ này.");

        var app = await kybRepository.GetLatestByLotIdAsync(command.ParkingLotId, cancellationToken);
        var isNew = app is null;

        if (app is null)
        {
            app = new KybApplication
            {
                ParkingLotId = lot.Id,
                Status = KybStatus.Draft,
                FieldSurveyRequired = lot.TotalSlots > 50
            };
        }
        else
        {
            // Kiểm tra trạng thái hiện tại: Không cho phép sửa nếu đang trong tiến trình thẩm định
            if (app.Status is KybStatus.Submitted or KybStatus.UnderReview)
                throw new ConflictException("Hồ sơ đang trong quá trình thẩm định, không thể chỉnh sửa nháp.");

            if (app.Status == KybStatus.Approved)
                throw new ConflictException("Hồ sơ KYB đã được phê duyệt thành công, không thể chỉnh sửa.");

            // Nếu hồ sơ đang ở trạng thái Rejected hoặc NeedsMoreInfo, chuyển về Draft khi chủ bãi lưu bản sửa mới
            if (app.Status is KybStatus.Rejected or KybStatus.NeedsMoreInfo)
            {
                app.Status = KybStatus.Draft;
            }
        }

        // Cập nhật các trường thông tin theo từng bước nếu có gửi dữ liệu
        // Bước 1: Pháp lý
        if (command.BusinessLicenseUrl is not null)
        {
            app.BusinessLicenseUrl = string.IsNullOrWhiteSpace(command.BusinessLicenseUrl)
                ? null
                : command.BusinessLicenseUrl.Trim();
        }

        // Bước 2: Hiện trường & Ảnh GPS
        if (command.SitePhotoUrls is not null)
        {
            app.SitePhotoUrlsJson = command.SitePhotoUrls.Count > 0
                ? JsonSerializer.Serialize(command.SitePhotoUrls)
                : null;
        }

        if (command.PhotoLatitude.HasValue)
            app.PhotoLatitude = command.PhotoLatitude.Value;

        if (command.PhotoLongitude.HasValue)
            app.PhotoLongitude = command.PhotoLongitude.Value;

        // Bước 3: PCCC
        if (command.FireSafetyCertificateUrl is not null)
        {
            app.FireSafetyCertificateUrl = string.IsNullOrWhiteSpace(command.FireSafetyCertificateUrl)
                ? null
                : command.FireSafetyCertificateUrl.Trim();
        }

        // Bước 4: Khảo sát thực địa
        app.FieldSurveyRequired = lot.TotalSlots > 50;

        if (command.FieldSurveyAtUtc.HasValue)
            app.FieldSurveyAtUtc = command.FieldSurveyAtUtc.Value;

        if (command.FieldSurveyNote is not null)
        {
            app.FieldSurveyNote = string.IsNullOrWhiteSpace(command.FieldSurveyNote)
                ? null
                : command.FieldSurveyNote.Trim();
        }

        if (isNew)
        {
            await kybRepository.AddAsync(app, cancellationToken);
        }
        else
        {
            await kybRepository.UpdateAsync(app, cancellationToken);
        }

        await kybRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Chủ bãi {OwnerId} đã lưu nháp hồ sơ KYB cho bãi Id={LotId}",
            command.OwnerProfileId, lot.Id);

        return app.ToDto();
    }
}
