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
/// Nếu bãi chưa từng có hồ sơ, tự động khởi tạo hồ sơ ở trạng thái Draft
/// với quy tắc khảo sát thực địa: FieldSurveyRequired = (lot.TotalSlots > 50).
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

        // Tự động khởi tạo hồ sơ Draft nếu bãi chưa có hồ sơ
        var draft = new KybApplication
        {
            ParkingLotId = lot.Id,
            Status = KybStatus.Draft,
            FieldSurveyRequired = lot.TotalSlots > 50
        };

        await kybRepository.AddAsync(draft, cancellationToken);
        await kybRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã khởi tạo hồ sơ KYB Draft cho bãi đỗ Id={LotId} (FieldSurveyRequired={Required})",
            lot.Id, draft.FieldSurveyRequired);

        return draft.ToDto();
    }
}
