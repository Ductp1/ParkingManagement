using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.Shifts;

/// <summary>US-076: người trực xem ca của mình, Start, xem tổng hợp, End và nhận bàn giao.</summary>
public sealed class ShiftDutyUseCases(GateAccess access, IShiftRepository shifts, IShiftCashSource cash,
    ILotOperationalStatusProvider lotStatus, ShiftOptions options, TimeProvider clock)
{
    public async Task<MyShiftsDto> MineAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        var userId = access.UserId;
        var now = clock.GetUtcNow().UtcDateTime;
        var from = fromUtc ?? now.AddDays(-1);
        var to = toUtc ?? now.AddDays(8);
        if (to <= from || to - from > TimeSpan.FromDays(62)) throw new ValidationException("Khoảng thời gian không hợp lệ (tối đa 62 ngày).");
        var mine = await shifts.WhereAsync(s => s.StaffUserId == userId
            && (s.Status == ShiftStatus.Open || (s.ScheduledStartUtc >= from && s.ScheduledStartUtc < to)), ct);
        var ids = mine.Select(s => (int?)s.Id).ToList();
        var incoming = await shifts.WhereAsync(s => ids.Contains(s.HandoverToShiftId) && s.HandoverAcceptedAtUtc == null
            && (s.Status == ShiftStatus.HandoverPending || s.Status == ShiftStatus.AutoClosed), ct);
        return new MyShiftsDto(mine.OrderBy(s => s.ScheduledStartUtc).Select(ShiftView.From).ToList(),
            incoming.Select(ShiftView.From).ToList());
    }

    public async Task<ShiftView> StartAsync(int shiftId, CancellationToken ct)
    {
        var shift = await OwnShiftAsync(shiftId, ct);
        await access.RequireOperatorAsync(shift.ParkingLotId, ct);
        if (shift.Status != ShiftStatus.Scheduled)
            throw new ConflictException($"Ca đang ở trạng thái {shift.Status}, không thể bắt đầu.", ShiftErrors.AlreadyStarted);
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < shift.ScheduledStartUtc - options.StartEarly)
            throw new ConflictException($"Có thể bắt đầu ca từ {shift.ScheduledStartUtc - options.StartEarly:O}.", ShiftErrors.StartTooEarly);
        if (now >= shift.ScheduledEndUtc) throw new ConflictException("Ca đã quá giờ kết thúc.", ShiftErrors.Expired);

        // Không biết trạng thái bãi thì không cho mở ca.
        var lot = await lotStatus.GetAsync(shift.ParkingLotId, ct)
            ?? throw new DependencyUnavailableException("Chưa kiểm tra được trạng thái bãi, thử lại sau ít phút.");
        if (lot.EmergencyStopped || !string.Equals(lot.Status, nameof(ParkingLotStatus.Active), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Bãi đang không hoạt động, không thể mở ca.", ShiftErrors.LotNotOperational);

        var userId = shift.StaffUserId;
        if ((await shifts.WhereAsync(s => s.StaffUserId == userId && s.Status == ShiftStatus.Open, ct)).Count > 0)
            throw new ConflictException("Bạn đang có một ca chưa kết thúc.", ShiftErrors.AlreadyOpen);

        shift.Status = ShiftStatus.Open;
        shift.StartedAtUtc = now;
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    public async Task<ShiftSummaryDto> SummaryAsync(int shiftId, CancellationToken ct)
    {
        var shift = await shifts.FindAsync(shiftId, ct) ?? throw new NotFoundException("Ca trực", shiftId);
        if (shift.StaffUserId != access.UserId) await access.RequireReadAsync(shift.ParkingLotId, ct);

        var counts = await shifts.CountEventsAsync(shift.Id, ct);
        var system = shift.CashSystem ?? await TryCashAsync(shift.Id, ct);
        var opening = await ShiftCash.OpeningAsync(shifts, shift.Id, ct);
        var next = shift.Status == ShiftStatus.Open && shift.IsCashHolder
            ? await ShiftCash.NextHolderAsync(shifts, shift, options, ct)
            : shift.HandoverToShiftId is { } to ? await shifts.FindAsync(to, ct) : null;
        return new ShiftSummaryDto(ShiftView.From(shift), await shifts.CountVehiclesInLotAsync(shift.ParkingLotId, ct), counts.Entries,
            counts.Exits, opening, system, shift.IsCashHolder ? opening + system : null, counts.Exceptions,
            await shifts.CountPendingPmsAsync(shift.ParkingLotId, ct), counts.ManualBarrierOpens, counts.NoShowCancels,
            next is null ? null : ShiftView.From(next));
    }

    public async Task<ShiftView> EndAsync(int shiftId, EndShiftRequest request, CancellationToken ct)
    {
        var shift = await OwnShiftAsync(shiftId, ct);
        await access.RequireOperatorAsync(shift.ParkingLotId, ct); // đã bị gỡ khỏi bãi thì không được ghi nữa
        if (shift.Status != ShiftStatus.Open) throw new ConflictException("Ca không ở trạng thái đang trực.", ShiftErrors.NotActive);
        var note = request.Note?.Trim();
        if (note?.Length > 1000) throw new ValidationException("Ghi chú tối đa 1000 ký tự.");
        var now = clock.GetUtcNow().UtcDateTime;

        if (shift.IsCashHolder)
        {
            var id = shift.Id;
            var pending = await shifts.WhereAsync(s => s.HandoverToShiftId == id && s.HandoverAcceptedAtUtc == null
                && (s.Status == ShiftStatus.HandoverPending || s.Status == ShiftStatus.AutoClosed), ct);
            if (pending.Count > 0)
                throw new ConflictException($"Hãy nhận bàn giao két của nhân viên #{pending[0].StaffUserId} trước khi kết ca.",
                    ShiftErrors.HandoverNotAccepted);
            if (request.CashCounted is not { } counted || counted < 0)
                throw new ValidationException("Hãy đếm két và nhập số tiền mặt.", ShiftErrors.CashCountRequired);

            // Không lấy được tổng tiền thì giữ ca mở (503), không đoán.
            var system = await cash.CashTotalAsync(shift.Id, ct);
            var receiver = await ShiftCash.NextHolderAsync(shifts, shift, options, ct);
            shift.CashSystem = system;
            shift.CashCounted = counted;
            shift.HandoverToShiftId = receiver?.Id;
            shift.Status = ShiftStatus.HandoverPending;
            ShiftCash.Check(shifts, shift, counted - (await ShiftCash.OpeningAsync(shifts, shift.Id, ct) + system), "Lệch cuối ca");
            shifts.Enqueue(new HandoverPending(shift.Id, shift.ParkingLotId, shift.OwnerProfileId, shift.StaffUserId, receiver?.StaffUserId));
        }
        else shift.Status = ShiftStatus.Completed;

        shift.EndedAtUtc = now;
        shift.ClosedReason = ShiftClosedReason.Normal;
        shift.HandoverNote = note;
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    /// <summary>Người giữ két kế tiếp (ca đang mở) hoặc chủ bãi đếm lại tiền và nhận bàn giao.</summary>
    public async Task<ShiftView> AcceptHandoverAsync(int shiftId, AcceptHandoverRequest request, CancellationToken ct)
    {
        var userId = access.UserId;
        var shift = await shifts.FindAsync(shiftId, ct) ?? throw new NotFoundException("Ca trực", shiftId);
        var receiver = shift.HandoverToShiftId is { } to ? await shifts.FindAsync(to, ct) : null;
        var asReceiver = receiver?.StaffUserId == userId;
        if (!asReceiver && !await access.OwnsAsync(shift.ParkingLotId, ct))
            throw new ForbiddenException("Chỉ ca nhận bàn giao hoặc chủ bãi được xác nhận.");
        if (asReceiver) await access.RequireOperatorAsync(shift.ParkingLotId, ct);
        if (asReceiver && receiver!.Status != ShiftStatus.Open)
            throw new ConflictException("Hãy bắt đầu ca trước khi nhận bàn giao.", ShiftErrors.NotActive);
        if (!shift.IsCashHolder || shift.HandoverAcceptedAtUtc is not null
            || shift.Status is not (ShiftStatus.HandoverPending or ShiftStatus.AutoClosed))
            throw new ConflictException("Ca này không chờ bàn giao.");
        if (request.CashReceived is not { } received || received < 0)
            throw new ValidationException("Hãy đếm tiền và nhập số đã nhận.", ShiftErrors.CashReceivedRequired);

        // Chủ bãi nhận thay: tiền rời két, không được tính vào tiền đầu ca của ca kế tiếp.
        if (!asReceiver) shift.HandoverToShiftId = null;
        shift.CashReceived = received;
        shift.HandoverAcceptedByUserId = userId;
        shift.HandoverAcceptedAtUtc = clock.GetUtcNow().UtcDateTime;
        if (shift.CashCounted is { } counted)
            ShiftCash.Check(shifts, shift, received - counted, "Lệch khi bàn giao");
        else if (shift.CashSystem is { } system) // AutoClosed: không có số đếm của người giao.
            ShiftCash.Check(shifts, shift, received - (await ShiftCash.OpeningAsync(shifts, shift.Id, ct) + system),
                "Ca tự đóng, người nhận đếm");
        if (shift.Status == ShiftStatus.HandoverPending) shift.Status = ShiftStatus.Completed;
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    private async Task<Shift> OwnShiftAsync(int shiftId, CancellationToken ct)
    {
        var userId = access.UserId;
        var shift = await shifts.FindAsync(shiftId, ct);
        return shift?.StaffUserId == userId ? shift : throw new NotFoundException("Ca trực", shiftId);
    }

    private async Task<decimal?> TryCashAsync(int shiftId, CancellationToken ct)
    {
        try { return await cash.CashTotalAsync(shiftId, ct); }
        catch (DependencyUnavailableException) { return null; }
    }
}
