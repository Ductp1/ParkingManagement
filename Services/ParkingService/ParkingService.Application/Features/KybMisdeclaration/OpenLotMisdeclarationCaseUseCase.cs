using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.KybMisdeclaration;

public interface IOpenLotMisdeclarationCaseUseCase
{
    Task<LotMisdeclarationDto> ExecuteAsync(int lotId, OpenLotMisdeclarationCaseRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-056 AC1 + AC3: mở case khai báo sai cho bãi đang Active. Minor → cảnh báo, hạn sửa N ngày (mặc định theo cấu hình);
/// Suspected → bãi Active → Suspended, hạn giải trình 24h. Mỗi bãi tối đa 1 case đang mở (409).
/// </summary>
public sealed class OpenLotMisdeclarationCaseUseCase(ILotMisdeclarationRepository repository, ILotMisdeclarationQueries queries,
    KybMisdeclarationSettings settings, TimeProvider timeProvider) : IOpenLotMisdeclarationCaseUseCase
{
    public async Task<LotMisdeclarationDto> ExecuteAsync(int lotId, OpenLotMisdeclarationCaseRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        MisdeclarationSupport.EnsureIds(lotId, performedByUserId);
        var severity = MisdeclarationSupport.ParseEnum<MisdeclarationSeverity>(request.Severity, "Mức độ khai sai");
        var category = MisdeclarationSupport.ParseEnum<MisdeclarationCategory>(request.Category, "Loại nội dung khai sai");
        if (severity == MisdeclarationSeverity.Suspected && request.CorrectionDays is not null)
            throw new ValidationException("Tạm khóa có hạn giải trình cố định 24 giờ, không nhận số ngày hạn sửa.");
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var lot = await repository.GetLotAsync(lotId, cancellationToken) ?? throw new NotFoundException("Bãi đỗ", lotId);
        if (await repository.HasOpenCaseAsync(lotId, cancellationToken))
            throw new ConflictException($"Bãi #{lotId} đang có case khai báo sai chưa xử lý xong.");

        var before = MisdeclarationEvents.Snapshot(lot, null);
        var created = severity == MisdeclarationSeverity.Minor
            ? LotMisdeclarationCase.Warn(lot, category, request.Finding ?? string.Empty,
                request.CorrectionDays ?? settings.MinorCorrectionDays, performedByUserId, now)
            : LotMisdeclarationCase.Suspend(lot, category, request.Finding ?? string.Empty, performedByUserId, now);
        repository.Add(created);

        var action = severity == MisdeclarationSeverity.Minor ? MisdeclarationEvents.Warned : MisdeclarationEvents.Suspended;
        await repository.SaveWithEventsAsync(
            [MisdeclarationEvents.Audit(lot, action, performedByUserId, before, MisdeclarationEvents.Snapshot(lot, created),
                created.Finding, now)],
            cancellationToken);
        return await MisdeclarationSupport.ReloadAsync(queries, settings, lotId, now, cancellationToken);
    }
}
