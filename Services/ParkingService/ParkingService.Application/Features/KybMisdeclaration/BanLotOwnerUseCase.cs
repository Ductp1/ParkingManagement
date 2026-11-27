using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybReview;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybMisdeclaration;

public interface IBanLotOwnerUseCase
{
    Task<OwnerBanResultDto> ExecuteAsync(int lotId, int caseId, BanLotOwnerRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-056 AC2: xác nhận cố ý gian lận từ một case đang mở → case Banned; MỌI bãi Active / PendingApproval của chủ bãi → Suspended
/// vĩnh viễn (không soft-delete); các case đang mở khác của chủ bãi → Banned (không còn đường khôi phục); hồ sơ KYB còn mở của
/// các bãi đó → Rejected. Tất cả + AuditRecorded (ParkingLot.OwnerBanned, KybApplication.RejectedByBan) trong MỘT SaveChanges.
/// Khóa tài khoản chủ bãi thuộc AdminService (US-096) – không gọi ở đây.
/// </summary>
public sealed class BanLotOwnerUseCase(ILotMisdeclarationRepository repository, ILotMisdeclarationQueries queries,
    KybMisdeclarationSettings settings, TimeProvider timeProvider) : IBanLotOwnerUseCase
{
    public async Task<OwnerBanResultDto> ExecuteAsync(int lotId, int caseId, BanLotOwnerRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        MisdeclarationSupport.EnsureIds(lotId, performedByUserId, caseId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var item = await MisdeclarationSupport.LoadCaseAsync(repository, lotId, caseId, cancellationToken);
        var ownerProfileId = item.ParkingLot.OwnerProfileId;

        var lots = (await repository.GetOwnerLotsAsync(ownerProfileId, cancellationToken))
            .Append(item.ParkingLot).DistinctBy(l => l.Id).ToList();
        var openCases = (await repository.GetOpenCasesByOwnerAsync(ownerProfileId, cancellationToken))
            .Where(c => c.Id != item.Id).Prepend(item).ToList();
        var kybs = await repository.GetOpenKybApplicationsAsync(lots.Select(l => l.Id).ToList(), cancellationToken);

        var caseByLot = openCases.ToDictionary(c => c.ParkingLotId);
        var before = lots.ToDictionary(l => l.Id, l => MisdeclarationEvents.Snapshot(l, caseByLot.GetValueOrDefault(l.Id)));
        var kybBefore = kybs.ToDictionary(k => k.Id, KybReviewEvents.Snapshot);

        // Case gốc trước: lý do rỗng / case đã đóng → 400 / 409 khi chưa đổi gì.
        item.Ban(performedByUserId, request.Reason ?? string.Empty, now);
        var reason = item.ResolutionNote!;
        foreach (var other in openCases.Skip(1)) other.Ban(performedByUserId, reason, now);
        var suspendedLotIds = lots.Where(l => l.SuspendForOwnerBan()).Select(l => l.Id).ToList();
        foreach (var kyb in kybs) kyb.RejectByOwnerBan(performedByUserId, now);

        List<IntegrationEvent> events = [];
        foreach (var lot in lots.Where(l => suspendedLotIds.Contains(l.Id) || caseByLot.ContainsKey(l.Id)))
            events.Add(MisdeclarationEvents.Audit(lot, MisdeclarationEvents.OwnerBanned, performedByUserId, before[lot.Id],
                MisdeclarationEvents.Snapshot(lot, caseByLot.GetValueOrDefault(lot.Id)), reason, now));
        foreach (var kyb in kybs)
            events.Add(KybReviewEvents.Audit(kyb, MisdeclarationEvents.KybRejectedByBan, performedByUserId, kybBefore[kyb.Id],
                KybApplication.OwnerBannedRejectReason, now));
        await repository.SaveWithEventsAsync(events, cancellationToken);

        var view = await MisdeclarationSupport.ReloadAsync(queries, settings, lotId, now, cancellationToken);
        return new OwnerBanResultDto(view, ownerProfileId, suspendedLotIds, kybs.Select(k => k.Id).ToList(),
            openCases.Select(c => c.Id).ToList());
    }
}
