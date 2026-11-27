using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.Shifts;

/// <summary>Quyền thu tiền mặt tại cổng: trả về ca sẽ ghi khoản thu.</summary>
public interface ICashDrawer
{
    Task<int> AuthorizeCollectionAsync(CancellationToken ct);
}

/// <summary>
/// Mỗi bãi một két: chỉ ca giữ két được thu, và chỉ khi không còn ca giữ két trước đó đang mở / chờ bàn giao
/// (nếu không tiền của hai ca trộn trong cùng một lần đếm).
/// </summary>
public sealed class CashDrawer(CurrentShift current, IShiftRepository shifts) : ICashDrawer
{
    public async Task<int> AuthorizeCollectionAsync(CancellationToken ct)
    {
        var shift = current.Value ?? throw new ConflictException("Chưa có ca đang trực. Hãy bắt đầu ca để thu tiền.", ShiftErrors.NotActive);
        var lotId = shift.ParkingLotId;
        if (!shift.IsCashHolder)
        {
            var holders = await shifts.WhereAsync(s => s.ParkingLotId == lotId && s.IsCashHolder && s.Status == ShiftStatus.Open, ct);
            throw holders.Count > 0
                ? new ConflictException($"Tiền mặt của ca này do người giữ két (nhân viên #{holders[0].StaffUserId}) thu.", ShiftErrors.NotCashHolder)
                : new ConflictException("Hiện không có ai giữ két tại bãi.", ShiftErrors.NoCashHolder);
        }

        var id = shift.Id;
        var started = shift.StartedAtUtc;
        var blocking = await shifts.WhereAsync(s => s.ParkingLotId == lotId && s.IsCashHolder && s.Id != id
            && ((s.Status == ShiftStatus.Open && s.StartedAtUtc < started)
                || s.Status == ShiftStatus.HandoverPending
                || (s.Status == ShiftStatus.AutoClosed && s.HandoverToShiftId == id && s.HandoverAcceptedAtUtc == null)), ct);
        if (blocking.Count > 0)
            throw new ConflictException($"Hãy nhận bàn giao két của nhân viên #{blocking[0].StaffUserId} trước khi thu tiền.",
                ShiftErrors.HandoverNotAccepted);
        return id;
    }
}

/// <summary>Các phép tính két dùng chung cho Start/End, bàn giao và job.</summary>
public static class ShiftCash
{
    /// <summary>Tiền đầu ca = tổng số đã đếm lại của các bàn giao ca này đã nhận.</summary>
    public static async Task<decimal> OpeningAsync(IShiftRepository shifts, int shiftId, CancellationToken ct)
        => (await shifts.WhereAsync(s => s.HandoverToShiftId == shiftId && s.HandoverAcceptedAtUtc != null, ct))
            .Sum(s => s.CashReceived ?? 0);

    /// <summary>Ca giữ két kế tiếp nhận bàn giao: bắt đầu quanh giờ kết thúc, hoặc đã mở sau ca này. null = chủ bãi nhận.</summary>
    public static async Task<Shift?> NextHolderAsync(IShiftRepository shifts, Shift shift, ShiftOptions options, CancellationToken ct)
    {
        var (lotId, id, started) = (shift.ParkingLotId, shift.Id, shift.StartedAtUtc);
        var from = shift.ScheduledEndUtc - options.Grace;
        var to = shift.ScheduledEndUtc + options.Grace;
        return (await shifts.WhereAsync(s => s.ParkingLotId == lotId && s.IsCashHolder && s.Id != id
                && (((s.Status == ShiftStatus.Scheduled || s.Status == ShiftStatus.Open) && s.ScheduledStartUtc >= from && s.ScheduledStartUtc <= to)
                    || (s.Status == ShiftStatus.Open && s.StartedAtUtc > started)), ct))
            .OrderBy(s => s.ScheduledStartUtc).FirstOrDefault();
    }

    /// <summary>Ghi nhận lệch tiền (nếu có) và phát cảnh báo cho chủ bãi.</summary>
    public static void Check(IShiftRepository shifts, Shift shift, decimal difference, string note)
    {
        if (difference == 0) return;
        shift.DiscrepancyStatus = ShiftDiscrepancyStatus.Open;
        shift.DiscrepancyNote = $"{note}: lệch {difference:N0}đ";
        shifts.Enqueue(new CashDiscrepancyRaised(shift.Id, shift.ParkingLotId, shift.OwnerProfileId, shift.StaffUserId, difference));
    }
}
