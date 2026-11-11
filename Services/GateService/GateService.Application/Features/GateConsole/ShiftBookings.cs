using GateService.Application.Features;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

/// <summary>1 ca trực (đọc từ bảng Shifts có sẵn của GateService) để xác định khung giờ "trong ca".</summary>
public sealed record ShiftDto(int Id, int ParkingLotId, int StaffUserId, DateTime StartedAtUtc, DateTime? EndedAtUtc, string Status);

public interface IShiftQueries
{
    /// <summary>Ca đang mở (ShiftStatus.Open) của nhân viên — dùng xác định khung giờ và bãi được gán (UC-20).</summary>
    Task<ShiftDto?> FindOpenByStaffAsync(int staffUserId, CancellationToken cancellationToken);
}

public sealed record ShiftBookingsQuery(
    int? ParkingLotId,
    int? StaffUserId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search);

/// <summary>1 dòng trong danh sách ca trực: booking (từ BookingService) hoặc lượt vãng lai đang đỗ (từ pm_gate).</summary>
public sealed record ShiftBookingItemDto(
    string Code,
    string? BookingCode,
    string PlateDisplay,
    string? SlotCode,
    DateTime StartAtUtc,
    DateTime? EndAtUtc,
    string Status,
    bool IsOverdue,
    string Source,
    decimal? TotalAmount);

public interface IListShiftBookingsUseCase
{
    Task<IReadOnlyList<ShiftBookingItemDto>> ExecuteAsync(ShiftBookingsQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// UC-20 (US-076): staff xem booking của bãi được gán trong ca trực, tìm theo mã booking/biển số.
/// Khung giờ: from/to truyền vào, hoặc theo ca đang mở của staff, hoặc mặc định [hiện tại − 2h, hiện tại + 12h].
/// Gộp: booking sắp đến/đang đỗ/quá giờ (BookingService) + khách vãng lai đang đỗ (ParkingSessions).
/// Chỉ hiển thị biển số và thông tin vận hành — không SĐT/CCCD (business rule UC-20).
/// </summary>
public sealed class ListShiftBookingsUseCase(
    IShiftQueries shifts,
    IGateBookingClient bookings,
    IParkingSessionQueries sessions,
    TimeProvider timeProvider) : IListShiftBookingsUseCase
{
    public async Task<IReadOnlyList<ShiftBookingItemDto>> ExecuteAsync(ShiftBookingsQuery query, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var shift = query.StaffUserId is { } staffId ? await shifts.FindOpenByStaffAsync(staffId, cancellationToken) : null;

        var parkingLotId = query.ParkingLotId ?? shift?.ParkingLotId
            ?? throw new ValidationException("parkingLotId bắt buộc khi nhân viên chưa mở ca trực.");

        var from = query.FromUtc ?? shift?.StartedAtUtc ?? now.AddHours(-2);
        var to = query.ToUtc ?? now.AddHours(12);
        if (to <= from) throw new ValidationException("Khung giờ ca trực không hợp lệ (toUtc phải sau fromUtc).");

        var items = new List<ShiftBookingItemDto>();

        foreach (var b in await bookings.ListForLotAsync(parkingLotId, from, to, query.Search, cancellationToken))
        {
            items.Add(new ShiftBookingItemDto(b.Code, b.Code, PlateNormalizer.Format(b.PlateNumber), b.SlotCode,
                b.StartAtUtc, b.EndAtUtc, b.Status, b.IsOverdue, "Booking", b.TotalAmount));
        }

        foreach (var s in await sessions.ListByLotAsync(parkingLotId, ParkingSessionStatus.Active, now, cancellationToken))
        {
            if (!s.IsWalkIn) continue;                      // lượt có booking đã nằm trong danh sách phía trên
            var plate = PlateNormalizer.Format(s.PlateNumber);
            if (query.Search is not null &&
                !plate.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !s.Code.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new ShiftBookingItemDto(s.Code, null, plate, s.SlotCode,
                s.EntryAtUtc, null, s.Status, false, "WalkIn", s.Fee));
        }

        return items.OrderBy(i => i.StartAtUtc).ToList();
    }
}