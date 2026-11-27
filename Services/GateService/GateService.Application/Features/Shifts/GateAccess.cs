using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.Shifts;

/// <summary>Ca đang trực của request hiện tại (scoped): do <see cref="GateAccess"/> đặt trước mọi thao tác ghi ở cổng.</summary>
public sealed class CurrentShift
{
    public Shift? Value { get; set; }
}

/// <summary>
/// Phân quyền theo bãi: Staff theo claim ParkingLotId, LotOwner theo bãi mình sở hữu, Admin chỉ đọc.
/// Chưa đăng nhập → 401, sai quyền → 403, thao tác ghi không có ca đang mở → 409 SHIFT_NOT_ACTIVE.
/// </summary>
public sealed class GateAccess(ICurrentUser user, ILotDirectory lots, IShiftRepository shifts, CurrentShift current)
{
    public int UserId => user.IsAuthenticated && user.UserId is { } id ? id : throw new AuthenticationException("Chưa đăng nhập.");
    public bool IsAdmin => user.Roles.Contains("Admin");

    public async Task<bool> OwnsAsync(int parkingLotId, CancellationToken ct)
        => user.Roles.Contains("LotOwner") && user.OwnerProfileId is { } owner
            && (await lots.GetAsync(parkingLotId, ct)).OwnerProfileId == owner;

    private async Task<bool> OperatesAsync(int parkingLotId, CancellationToken ct)
        => (user.Roles.Contains("Staff") && user.ParkingLotIds.Contains(parkingLotId)) || await OwnsAsync(parkingLotId, ct);

    public async Task RequireReadAsync(int parkingLotId, CancellationToken ct)
    {
        _ = UserId;
        if (!IsAdmin && !await OperatesAsync(parkingLotId, ct)) throw Forbidden();
    }

    public async Task RequireOperatorAsync(int parkingLotId, CancellationToken ct)
    {
        _ = UserId;
        if (!await OperatesAsync(parkingLotId, ct)) throw Forbidden();
    }

    public async Task<LotInfo> RequireOwnerAsync(int parkingLotId, CancellationToken ct)
    {
        _ = UserId;
        if (!user.Roles.Contains("LotOwner") || user.OwnerProfileId is not { } owner) throw Forbidden();
        var lot = await lots.GetAsync(parkingLotId, ct);
        return lot.OwnerProfileId == owner ? lot : throw Forbidden();
    }

    public async Task RequireOwnerOrAdminAsync(int parkingLotId, CancellationToken ct)
    {
        _ = UserId;
        if (!IsAdmin) await RequireOwnerAsync(parkingLotId, ct);
    }

    /// <summary>Thao tác ghi ở cổng. parkingLotId = null: thao tác không gắn bãi (đọc ảnh biển số) – cần có ca đang mở bất kỳ.</summary>
    public async Task<Shift> RequireActiveShiftAsync(int? parkingLotId, CancellationToken ct)
    {
        var userId = UserId;
        if (parkingLotId is { } requested) await RequireOperatorAsync(requested, ct);
        var shift = (await shifts.WhereAsync(s => s.StaffUserId == userId && s.Status == ParkingManagement.SharedKernel.Enums.ShiftStatus.Open, ct))
            .FirstOrDefault()
            ?? throw new ConflictException("Chưa có ca đang trực. Hãy bắt đầu ca để thao tác ở cổng.", ShiftErrors.NotActive);
        if (parkingLotId is { } lotId && shift.ParkingLotId != lotId)
            throw new ForbiddenException("Chỉ được thao tác tại bãi của ca đang trực.");
        if (parkingLotId is null) await RequireOperatorAsync(shift.ParkingLotId, ct);
        return current.Value = shift;
    }

    private static ForbiddenException Forbidden() => new("Bạn không có quyền với bãi xe này.");
}
