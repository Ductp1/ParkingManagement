using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.SanctionStates;

public interface IUpsertSanctionStateUseCase
{
    Task<SanctionStateDto> ExecuteAsync(int sanctionId, UpsertSanctionStateRequest request, int adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: AdminService đẩy hiệu lực chế tài cấp 2 / cấp 3 theo bãi. Idempotent theo SanctionId: lần đầu tạo, gọi lại (retry) chỉ cập nhật
/// thời gian. Không đổi ParkingLot.Status – tìm kiếm và đặt chỗ đọc bảng SanctionStates.
/// </summary>
public sealed class UpsertSanctionStateUseCase(ISanctionStateRepository repository, ISanctionStateQueries queries, TimeProvider timeProvider)
    : IUpsertSanctionStateUseCase
{
    public async Task<SanctionStateDto> ExecuteAsync(int sanctionId, UpsertSanctionStateRequest request, int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (sanctionId <= 0) throw new ValidationException("Mã chế tài không hợp lệ.");
        if (request.OwnerProfileId <= 0) throw new ValidationException("Mã chủ bãi không hợp lệ.");
        if (request.ParkingLotId is <= 0) throw new ValidationException("Mã bãi không hợp lệ.");
        if (request.Kind is not { } kind || !Enum.IsDefined(kind))
            throw new ValidationException("Loại chế tài phải là RankingPenalty hoặc ListingSuspension.");
        if (request.StartsAtUtc is not { } rawStartsAt) throw new ValidationException("Thiếu thời điểm bắt đầu (startsAtUtc).");

        var startsAt = AsUtc(rawStartsAt);
        DateTime? endsAt = request.EndsAtUtc is { } rawEndsAt ? AsUtc(rawEndsAt) : null;
        if (endsAt is { } end && end <= startsAt) throw new ValidationException("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        if (kind == SanctionStateKind.RankingPenalty && endsAt is null)
            throw new ValidationException("Hạ thứ hạng phải có thời điểm kết thúc.");
        if (kind == SanctionStateKind.ListingSuspension && request.ParkingLotId is null)
            throw new ValidationException("Tạm ngưng nhận đặt chỗ phải chỉ định bãi (parkingLotId).");

        if (request.ParkingLotId is { } lotId)
        {
            var lot = await queries.FindLotAsync(lotId, cancellationToken)
                      ?? throw new NotFoundException("Bãi đỗ xe", lotId);
            if (lot.OwnerProfileId != request.OwnerProfileId)
                throw new ValidationException($"Bãi #{lotId} không thuộc chủ bãi #{request.OwnerProfileId}.");
        }

        var state = await repository.FindBySanctionIdAsync(sanctionId, cancellationToken);
        if (state is null)
        {
            state = SanctionState.Create(sanctionId, request.OwnerProfileId, request.ParkingLotId, kind, startsAt, endsAt, adminUserId);
            repository.Add(state);
        }
        else
        {
            state.Reapply(request.OwnerProfileId, request.ParkingLotId, kind, startsAt, endsAt, adminUserId);
        }

        await repository.SaveAsync(cancellationToken);
        return LotSanctionEffects.ToDto(state, timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>JSON không kèm múi giờ → coi là UTC (cột timestamptz chỉ nhận UTC).</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
