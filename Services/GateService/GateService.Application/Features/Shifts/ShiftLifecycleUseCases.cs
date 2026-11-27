using System.Linq.Expressions;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.Shifts;

/// <summary>Chuyển trạng thái tự động: Missed, đóng cưỡng bức (sự kiện từ service khác / quên End) và bù tổng tiền còn thiếu.</summary>
public sealed class ShiftLifecycleUseCases(IShiftRepository shifts, IShiftCashSource cash, ShiftOptions options, TimeProvider clock)
{
    /// <summary>Đóng ngay mọi ca đang mở khớp điều kiện. Không gọi PaymentService ở đây: tổng tiền do job điền sau.</summary>
    public async Task<int> AutoCloseAsync(Expression<Func<Shift, bool>> filter, ShiftClosedReason reason, CancellationToken ct)
    {
        var open = (await shifts.WhereAsync(filter, ct)).Where(s => s.Status == ShiftStatus.Open).ToList();
        var closing = open.Select(s => s.Id).ToHashSet();
        foreach (var shift in open) await CloseAsync(shift, reason, closing, ct);
        if (open.Count > 0) await shifts.SaveAsync(ct);
        return open.Count;
    }

    public async Task RunDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var deadline = now - options.Grace;

        var missed = await shifts.WhereAsync(s => s.Status == ShiftStatus.Scheduled && s.ScheduledStartUtc < deadline, ct);
        foreach (var shift in missed)
        {
            shift.Status = ShiftStatus.Missed;
            shifts.Enqueue(new ShiftMissed(shift.Id, shift.ParkingLotId, shift.OwnerProfileId, shift.StaffUserId,
                shift.ScheduledStartUtc, shift.ScheduledEndUtc));
        }
        if (missed.Count > 0) await shifts.SaveAsync(ct);

        await AutoCloseAsync(s => s.Status == ShiftStatus.Open && s.ScheduledEndUtc < deadline, ShiftClosedReason.Forgot, ct);

        var waiting = await shifts.WhereAsync(s => s.Status == ShiftStatus.AutoClosed && s.IsCashHolder && s.CashSystem == null, ct);
        foreach (var shift in waiting)
        {
            decimal total;
            try { total = await cash.CashTotalAsync(shift.Id, ct); }
            catch (DependencyUnavailableException) { break; } // PaymentService chưa sẵn sàng: lượt sau thử lại.
            shift.CashSystem = total;
            if (shift.CashCounted is null && shift.CashReceived is { } received)
                ShiftCash.Check(shifts, shift, received - (await ShiftCash.OpeningAsync(shifts, shift.Id, ct) + total),
                    "Ca tự đóng, người nhận đếm");
            await shifts.SaveAsync(ct);
        }
    }

    private async Task CloseAsync(Shift shift, ShiftClosedReason reason, IReadOnlySet<int> closing, CancellationToken ct)
    {
        var receiver = shift.IsCashHolder ? await ShiftCash.NextHolderAsync(shifts, shift, options, ct) : null;
        if (receiver is not null && closing.Contains(receiver.Id)) receiver = null; // ca nhận cũng đang bị đóng → chủ bãi nhận
        shift.Status = ShiftStatus.AutoClosed;
        shift.EndedAtUtc = clock.GetUtcNow().UtcDateTime;
        shift.ClosedReason = reason;
        shift.HandoverToShiftId = receiver?.Id;
        shifts.Enqueue(new ShiftAutoClosed(shift.Id, shift.ParkingLotId, shift.OwnerProfileId, shift.StaffUserId, reason.ToString()));
        if (!shift.IsCashHolder) return;
        if (reason == ShiftClosedReason.Forgot)
        {
            shift.DiscrepancyStatus = ShiftDiscrepancyStatus.Open;
            shift.DiscrepancyNote = "Ca tự đóng: chưa có số đếm tiền.";
        }
        shifts.Enqueue(new HandoverPending(shift.Id, shift.ParkingLotId, shift.OwnerProfileId, shift.StaffUserId, receiver?.StaffUserId));
    }
}
