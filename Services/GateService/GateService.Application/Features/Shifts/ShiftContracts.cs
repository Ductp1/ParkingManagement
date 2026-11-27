using System.Linq.Expressions;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Application.Features.Shifts;

/// <summary>"Gate:Shift:*" – được Start sớm bao lâu; quá giờ bao lâu thì Missed / AutoClosed.</summary>
public sealed record ShiftOptions(TimeSpan StartEarly, TimeSpan Grace)
{
    public static readonly ShiftOptions Default = new(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30));
}

/// <summary>ProblemDetails.code của nghiệp vụ ca trực.</summary>
public static class ShiftErrors
{
    public const string Overlap = "SHIFT_OVERLAP";
    public const string CashHolderOverlap = "CASH_HOLDER_OVERLAP";
    public const string StaffNotInLot = "STAFF_NOT_IN_LOT";
    public const string StartTooEarly = "SHIFT_START_TOO_EARLY";
    public const string Expired = "SHIFT_EXPIRED";
    public const string AlreadyOpen = "SHIFT_ALREADY_OPEN";
    public const string AlreadyStarted = "SHIFT_ALREADY_STARTED";
    public const string NotActive = "SHIFT_NOT_ACTIVE";
    public const string LotNotOperational = "LOT_NOT_OPERATIONAL";
    public const string HandoverNotAccepted = "HANDOVER_NOT_ACCEPTED";
    public const string NotCashHolder = "NOT_CASH_HOLDER";
    public const string NoCashHolder = "NO_CASH_HOLDER";
    public const string CashCountRequired = "CASH_COUNT_REQUIRED";
    public const string CashReceivedRequired = "CASH_RECEIVED_REQUIRED";
    public const string CoverageGap = "COVERAGE_GAP";
    public const string NoCashHolderCoverage = "NO_CASH_HOLDER_COVERAGE";
}

// ===== PORT =====
public sealed record LotInfo(int ParkingLotId, int OwnerProfileId, string Name, string Status);
public sealed record TimeRange(DateTime StartUtc, DateTime EndUtc);

/// <summary>Thông tin bãi từ ParkingService. Không tìm thấy → NotFoundException; lỗi hạ tầng → DependencyUnavailableException.</summary>
public interface ILotDirectory
{
    Task<LotInfo> GetAsync(int parkingLotId, CancellationToken ct);
    /// <summary>Các khoảng mở cửa (UTC) của tuần bắt đầu từ weekStart (ngày giờ Việt Nam).</summary>
    Task<IReadOnlyList<TimeRange>> OpeningHoursAsync(int parkingLotId, int ownerProfileId, DateOnly weekStart, CancellationToken ct);
}

/// <summary>UserService: nhân viên còn được phân công tại bãi không (gọi bằng JWT của chủ bãi đang thao tác).</summary>
public interface IStaffDirectory
{
    Task<bool> IsAssignedAsync(int parkingLotId, int staffUserId, CancellationToken ct);
}

/// <summary>Tổng tiền mặt PaymentService đã ghi cho một ca.</summary>
public interface IShiftCashSource
{
    Task<decimal> CashTotalAsync(int shiftId, CancellationToken ct);
}

public sealed record ShiftEventCounts(int Entries, int Exits, int Exceptions, int ManualBarrierOpens, int NoShowCancels);

/// <summary>Ca trực, mẫu ca và outbox – cùng một DbContext nên SaveAsync lưu nguyên tử.</summary>
public interface IShiftRepository
{
    Task<Shift?> FindAsync(int id, CancellationToken ct);
    /// <summary>Entity có tracking.</summary>
    Task<List<Shift>> WhereAsync(Expression<Func<Shift, bool>> predicate, CancellationToken ct);
    Task<(IReadOnlyList<Shift> Items, int Total)> PageAsync(Expression<Func<Shift, bool>> predicate, int page, int pageSize, CancellationToken ct);
    void Add(Shift shift);
    void Remove(Shift shift);
    Task<List<ShiftTemplate>> TemplatesAsync(int parkingLotId, CancellationToken ct);
    Task<ShiftTemplate?> FindTemplateAsync(int id, CancellationToken ct);
    void Add(ShiftTemplate template);
    void Remove(ShiftTemplate template);
    Task<ShiftEventCounts> CountEventsAsync(int shiftId, CancellationToken ct);
    Task<int> CountVehiclesInLotAsync(int parkingLotId, CancellationToken ct);
    Task<int> CountPendingPmsAsync(int parkingLotId, CancellationToken ct);
    void Enqueue(IntegrationEvent integrationEvent);
    Task SaveAsync(CancellationToken ct);
}

// ===== DTO =====
public sealed record ShiftView(int Id, int ParkingLotId, int StaffUserId, DateTime ScheduledStartUtc, DateTime ScheduledEndUtc,
    DateTime? StartedAtUtc, DateTime? EndedAtUtc, ShiftStatus Status, DateOnly BusinessDate, int? TemplateId, bool IsCashHolder,
    bool IsFlagged, ShiftClosedReason? ClosedReason, decimal? CashSystem, decimal? CashCounted, decimal? CashReceived,
    ShiftDiscrepancyStatus? DiscrepancyStatus, string? DiscrepancyNote, int? HandoverToShiftId, DateTime? HandoverAcceptedAtUtc,
    string? HandoverNote)
{
    public static ShiftView From(Shift s) => new(s.Id, s.ParkingLotId, s.StaffUserId, s.ScheduledStartUtc, s.ScheduledEndUtc,
        s.StartedAtUtc, s.EndedAtUtc, s.Status, s.BusinessDate, s.TemplateId, s.IsCashHolder, s.IsFlagged, s.ClosedReason, s.CashSystem,
        s.CashCounted, s.CashReceived, s.DiscrepancyStatus, s.DiscrepancyNote, s.HandoverToShiftId, s.HandoverAcceptedAtUtc,
        s.HandoverNote);
}

public sealed record SaveShiftRequest(int StaffUserId, DateTime StartUtc, DateTime EndUtc, bool IsCashHolder = true, int? TemplateId = null);
public sealed record CreateShiftsRequest(IReadOnlyList<SaveShiftRequest>? Shifts);
public sealed record AdHocShiftRequest(int StaffUserId, int DurationMinutes, bool IsCashHolder = true);
public sealed record CopyWeekRequest(DateOnly FromWeekStart, DateOnly ToWeekStart);
public sealed record CoverageWarning(string Code, DateTime FromUtc, DateTime ToUtc);
public sealed record ShiftScheduleDto(IReadOnlyList<ShiftView> Shifts, IReadOnlyList<CoverageWarning> Warnings);
public sealed record CopyWeekResult(int Created, IReadOnlyList<string> Skipped);
public sealed record ShiftTemplateRequest(string? Name, TimeOnly StartTime, TimeOnly EndTime, bool IsCashHolder = true, bool IsActive = true);
public sealed record ShiftTemplateDto(int Id, int ParkingLotId, string Name, TimeOnly StartTime, TimeOnly EndTime, bool IsCashHolder, bool IsActive)
{
    public static ShiftTemplateDto From(ShiftTemplate t) => new(t.Id, t.ParkingLotId, t.Name, t.StartTime, t.EndTime, t.IsCashHolder, t.IsActive);
}
public sealed record ResolveDiscrepancyRequest(string? Note);
public sealed record EndShiftRequest(decimal? CashCounted, string? Note);
public sealed record AcceptHandoverRequest(decimal? CashReceived);
public sealed record ShiftSummaryDto(ShiftView Shift, int VehiclesInLot, int Entries, int Exits, decimal OpeningCash, decimal? CashSystem,
    decimal? ExpectedCash, int Exceptions, int PmsPending, int ManualBarrierOpens, int NoShowCancels, ShiftView? HandoverTo);
public sealed record MyShiftsDto(IReadOnlyList<ShiftView> Shifts, IReadOnlyList<ShiftView> IncomingHandovers);
public sealed record ShiftPageDto(IReadOnlyList<ShiftView> Items, int Total, int Page, int PageSize);
