using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.Shifts;

/// <summary>Chủ bãi: mẫu ca, lịch tuần, ca nhanh, bàn giao và chênh lệch; Admin xem danh sách ca.</summary>
public sealed class ShiftPlanningUseCases(GateAccess access, IShiftRepository shifts, IStaffDirectory staff, ILotDirectory lots,
    TimeProvider clock)
{
    private static readonly TimeSpan MinLength = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxLength = TimeSpan.FromHours(24);

    // ===== Mẫu ca =====
    public async Task<IReadOnlyList<ShiftTemplateDto>> TemplatesAsync(int lotId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(lotId, ct);
        return (await shifts.TemplatesAsync(lotId, ct)).Select(ShiftTemplateDto.From).ToList();
    }

    public async Task<ShiftTemplateDto> SaveTemplateAsync(int lotId, int? templateId, ShiftTemplateRequest request, CancellationToken ct)
    {
        var lot = await access.RequireOwnerAsync(lotId, ct);
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100) throw new ValidationException("Tên mẫu ca từ 1 đến 100 ký tự.");
        if (request.StartTime == request.EndTime) throw new ValidationException("Giờ bắt đầu và kết thúc phải khác nhau.");
        var all = await shifts.TemplatesAsync(lotId, ct);
        if (all.Any(t => t.Id != templateId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Bãi đã có mẫu ca cùng tên.");
        var template = templateId is { } id
            ? all.FirstOrDefault(t => t.Id == id) ?? throw new NotFoundException("Mẫu ca", id)
            : new ShiftTemplate { ParkingLotId = lotId, OwnerProfileId = lot.OwnerProfileId };
        template.Name = name;
        template.StartTime = request.StartTime;
        template.EndTime = request.EndTime;
        template.IsCashHolder = request.IsCashHolder;
        template.IsActive = request.IsActive;
        if (templateId is null) shifts.Add(template);
        await shifts.SaveAsync(ct);
        return ShiftTemplateDto.From(template);
    }

    public async Task DeleteTemplateAsync(int lotId, int templateId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(lotId, ct);
        var template = await shifts.FindTemplateAsync(templateId, ct);
        if (template?.ParkingLotId != lotId) throw new NotFoundException("Mẫu ca", templateId);
        shifts.Remove(template);
        await shifts.SaveAsync(ct);
    }

    // ===== Lịch =====
    public async Task<ShiftScheduleDto> WeekAsync(int lotId, DateOnly weekStart, CancellationToken ct)
    {
        var lot = await access.RequireOwnerAsync(lotId, ct);
        var week = await WeekShiftsAsync(lotId, weekStart, ct);
        return new ShiftScheduleDto(week.Select(ShiftView.From).ToList(), await WarningsAsync(lot, weekStart, week, ct));
    }

    public async Task<ShiftScheduleDto> CreateAsync(int lotId, CreateShiftsRequest request, CancellationToken ct)
    {
        var lot = await access.RequireOwnerAsync(lotId, ct);
        if (request.Shifts is not { Count: > 0 and <= 100 }) throw new ValidationException("Cần từ 1 đến 100 ca.");
        List<Shift> created = [];
        foreach (var item in request.Shifts)
        {
            var shift = NewShift(lot, access.UserId);
            await ApplyAsync(shift, item, created, ct);
            created.Add(shift);
        }
        foreach (var shift in created) shifts.Add(shift);
        await shifts.SaveAsync(ct);
        var weekStart = MondayOf(created[0].BusinessDate);
        return new ShiftScheduleDto(created.Select(ShiftView.From).ToList(),
            await WarningsAsync(lot, weekStart, await WeekShiftsAsync(lotId, weekStart, ct), ct));
    }

    /// <summary>Ca nhanh: bắt đầu ngay bây giờ; người trực vẫn phải bấm Start.</summary>
    public async Task<ShiftView> AdHocAsync(int lotId, AdHocShiftRequest request, CancellationToken ct)
    {
        var lot = await access.RequireOwnerAsync(lotId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var shift = NewShift(lot, access.UserId);
        await ApplyAsync(shift, new SaveShiftRequest(request.StaffUserId, now, now.AddMinutes(request.DurationMinutes),
            request.IsCashHolder), [], ct);
        shifts.Add(shift);
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    public async Task<ShiftView> UpdateAsync(int shiftId, SaveShiftRequest request, CancellationToken ct)
    {
        var (shift, _) = await ScheduledShiftAsync(shiftId, ct);
        await ApplyAsync(shift, request, [], ct);
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    public async Task DeleteAsync(int shiftId, CancellationToken ct)
    {
        var (shift, _) = await ScheduledShiftAsync(shiftId, ct);
        foreach (var from in await shifts.WhereAsync(s => s.HandoverToShiftId == shiftId, ct)) from.HandoverToShiftId = null;
        shifts.Remove(shift);
        await shifts.SaveAsync(ct);
    }

    public async Task<CopyWeekResult> CopyWeekAsync(int lotId, CopyWeekRequest request, CancellationToken ct)
    {
        var lot = await access.RequireOwnerAsync(lotId, ct);
        var offset = request.ToWeekStart.DayNumber - request.FromWeekStart.DayNumber;
        if (offset == 0) throw new ValidationException("Tuần đích phải khác tuần nguồn.");
        List<Shift> created = [];
        List<string> skipped = [];
        foreach (var source in await WeekShiftsAsync(lotId, request.FromWeekStart, ct))
        {
            var shift = NewShift(lot, access.UserId);
            var item = new SaveShiftRequest(source.StaffUserId, source.ScheduledStartUtc.AddDays(offset),
                source.ScheduledEndUtc.AddDays(offset), source.IsCashHolder, source.TemplateId);
            try
            {
                await ApplyAsync(shift, item, created, ct);
                created.Add(shift);
            }
            catch (Exception ex) when (ex is ConflictException or ValidationException)
            {
                skipped.Add($"Nhân viên #{source.StaffUserId} {item.StartUtc:O}: {ex.Message}");
            }
        }
        foreach (var shift in created) shifts.Add(shift);
        await shifts.SaveAsync(ct);
        return new CopyWeekResult(created.Count, skipped);
    }

    // ===== Bàn giao & chênh lệch =====
    public async Task<IReadOnlyList<ShiftView>> HandoversAsync(int lotId, ShiftStatus? status, CancellationToken ct)
    {
        await access.RequireOwnerAsync(lotId, ct);
        var (items, _) = await shifts.PageAsync(s => s.ParkingLotId == lotId && s.IsCashHolder && s.EndedAtUtc != null
            && (status == null || s.Status == status), 1, 100, ct);
        return items.Select(ShiftView.From).ToList();
    }

    public async Task<ShiftView> ResolveDiscrepancyAsync(int shiftId, ResolveDiscrepancyRequest request, CancellationToken ct)
    {
        var shift = await shifts.FindAsync(shiftId, ct) ?? throw new NotFoundException("Ca trực", shiftId);
        await access.RequireOwnerAsync(shift.ParkingLotId, ct);
        if (shift.DiscrepancyStatus != ShiftDiscrepancyStatus.Open) throw new ConflictException("Ca không có chênh lệch đang mở.");
        var note = request.Note?.Trim() ?? "";
        if (note.Length is < 1 or > 500) throw new ValidationException("Ghi chú xử lý từ 1 đến 500 ký tự.");
        shift.DiscrepancyStatus = ShiftDiscrepancyStatus.Resolved;
        var combined = $"{shift.DiscrepancyNote} | Đã xử lý: {note}";
        shift.DiscrepancyNote = combined.Length <= 1000 ? combined : combined[^1000..];
        await shifts.SaveAsync(ct);
        return ShiftView.From(shift);
    }

    public async Task<ShiftPageDto> AdminListAsync(ShiftStatus? status, int? parkingLotId, DateTime? fromUtc, DateTime? toUtc,
        int page, int pageSize, CancellationToken ct)
    {
        if (!access.IsAdmin) throw new ForbiddenException("Chỉ Admin được xem toàn bộ ca trực.");
        if (page < 1 || pageSize is < 1 or > 100) throw new ValidationException("page ≥ 1, pageSize từ 1 đến 100.");
        var (items, total) = await shifts.PageAsync(s => (status == null || s.Status == status)
            && (parkingLotId == null || s.ParkingLotId == parkingLotId)
            && (fromUtc == null || s.ScheduledStartUtc >= fromUtc) && (toUtc == null || s.ScheduledStartUtc < toUtc), page, pageSize, ct);
        return new ShiftPageDto(items.Select(ShiftView.From).ToList(), total, page, pageSize);
    }

    // ===== Nội bộ =====
    private async Task<(Shift, LotInfo)> ScheduledShiftAsync(int shiftId, CancellationToken ct)
    {
        var shift = await shifts.FindAsync(shiftId, ct) ?? throw new NotFoundException("Ca trực", shiftId);
        var lot = await access.RequireOwnerAsync(shift.ParkingLotId, ct);
        return shift.Status == ShiftStatus.Scheduled ? (shift, lot)
            : throw new ConflictException("Ca đã bắt đầu hoặc đã đóng, không thể sửa / xóa.", ShiftErrors.AlreadyStarted);
    }

    private static Shift NewShift(LotInfo lot, int createdBy)
        => new() { ParkingLotId = lot.ParkingLotId, OwnerProfileId = lot.OwnerProfileId, CreatedByUserId = createdBy };

    /// <param name="pending">Các ca đang tạo trong cùng request, chưa có trong database.</param>
    private async Task ApplyAsync(Shift shift, SaveShiftRequest item, IReadOnlyList<Shift> pending, CancellationToken ct)
    {
        var start = DateTime.SpecifyKind(item.StartUtc.ToUniversalTime(), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(item.EndUtc.ToUniversalTime(), DateTimeKind.Utc);
        if (item.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        if (end - start < MinLength || end - start > MaxLength) throw new ValidationException("Ca dài từ 15 phút đến 24 giờ.");
        if (end <= clock.GetUtcNow().UtcDateTime) throw new ValidationException("Không thể xếp ca đã qua.");
        // Chủ bãi tự trực không cần phân công nhân viên.
        if (item.StaffUserId != access.UserId && !await staff.IsAssignedAsync(shift.ParkingLotId, item.StaffUserId, ct))
            throw new ValidationException("Nhân viên chưa được phân công tại bãi này.", ShiftErrors.StaffNotInLot);

        var (id, staffId, lotId) = (shift.Id, item.StaffUserId, shift.ParkingLotId);
        var clashes = await shifts.WhereAsync(s => s.Id != id && (s.StaffUserId == staffId || (s.ParkingLotId == lotId && s.IsCashHolder))
            && (s.Status == ShiftStatus.Scheduled || s.Status == ShiftStatus.Open)
            && s.ScheduledStartUtc < end && s.ScheduledEndUtc > start, ct);
        clashes.AddRange(pending.Where(s => s.ScheduledStartUtc < end && s.ScheduledEndUtc > start));
        if (clashes.Any(s => s.StaffUserId == staffId))
            throw new ConflictException("Nhân viên đã có ca trùng giờ.", ShiftErrors.Overlap);
        if (item.IsCashHolder && clashes.Any(s => s.ParkingLotId == lotId && s.IsCashHolder))
            throw new ConflictException("Mỗi thời điểm chỉ một ca được giữ két.", ShiftErrors.CashHolderOverlap);

        shift.StaffUserId = staffId;
        shift.ScheduledStartUtc = start;
        shift.ScheduledEndUtc = end;
        shift.BusinessDate = Shift.BusinessDateOf(start);
        shift.IsCashHolder = item.IsCashHolder;
        shift.TemplateId = item.TemplateId;
    }

    private async Task<List<Shift>> WeekShiftsAsync(int lotId, DateOnly weekStart, CancellationToken ct)
    {
        var end = weekStart.AddDays(7);
        return (await shifts.WhereAsync(s => s.ParkingLotId == lotId && s.BusinessDate >= weekStart && s.BusinessDate < end, ct))
            .OrderBy(s => s.ScheduledStartUtc).ToList();
    }

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private async Task<IReadOnlyList<CoverageWarning>> WarningsAsync(LotInfo lot, DateOnly weekStart, IReadOnlyList<Shift> week,
        CancellationToken ct)
    {
        IReadOnlyList<TimeRange> open;
        try { open = await lots.OpeningHoursAsync(lot.ParkingLotId, lot.OwnerProfileId, weekStart, ct); }
        catch (DependencyUnavailableException) { return []; } // Cảnh báo chỉ để tham khảo, không chặn lưu lịch.
        var planned = week.Where(s => s.Status is not (ShiftStatus.Missed or ShiftStatus.AutoClosed)).ToList();
        return ShiftCoverage.Gaps(open, planned.Select(Range))
            .Select(g => new CoverageWarning(ShiftErrors.CoverageGap, g.StartUtc, g.EndUtc))
            .Concat(ShiftCoverage.Gaps(open, planned.Where(s => s.IsCashHolder).Select(Range))
                .Select(g => new CoverageWarning(ShiftErrors.NoCashHolderCoverage, g.StartUtc, g.EndUtc)))
            .ToList();
        static TimeRange Range(Shift s) => new(s.ScheduledStartUtc, s.ScheduledEndUtc);
    }
}

public static class ShiftCoverage
{
    /// <summary>Phần giờ mở cửa không được khoảng nào phủ; các khoảng mở liền nhau được gộp trước.</summary>
    public static IReadOnlyList<TimeRange> Gaps(IEnumerable<TimeRange> open, IEnumerable<TimeRange> covers)
    {
        var covered = Merge(covers);
        List<TimeRange> gaps = [];
        foreach (var slot in Merge(open))
        {
            var cursor = slot.StartUtc;
            foreach (var c in covered.Where(c => c.EndUtc > slot.StartUtc && c.StartUtc < slot.EndUtc))
            {
                if (c.StartUtc > cursor) gaps.Add(new TimeRange(cursor, c.StartUtc));
                if (c.EndUtc > cursor) cursor = c.EndUtc;
            }
            if (cursor < slot.EndUtc) gaps.Add(new TimeRange(cursor, slot.EndUtc));
        }
        return gaps;
    }

    private static List<TimeRange> Merge(IEnumerable<TimeRange> ranges)
    {
        List<TimeRange> merged = [];
        foreach (var r in ranges.OrderBy(r => r.StartUtc))
        {
            if (merged.Count > 0 && r.StartUtc <= merged[^1].EndUtc)
            {
                if (r.EndUtc > merged[^1].EndUtc) merged[^1] = merged[^1] with { EndUtc = r.EndUtc };
            }
            else merged.Add(r);
        }
        return merged;
    }
}
