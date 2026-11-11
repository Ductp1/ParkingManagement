using ParkingManagement.SharedKernel.Enums;

namespace GateService.Application.Features.GateEvents;

/// <summary>Dữ liệu tối thiểu để ghi một sự kiện tại cổng vào bảng GateEvents.</summary>
public sealed record GateEventDraft(int ParkingLotId, GateEventType EventType, int? ParkingSessionId = null,
    string? PlateNumber = null, int? PerformedByUserId = null, string? Note = null, string? ImagePath = null,
    int? ShiftId = null);

/// <summary>
/// Port ghi nhật ký GateEvents – mọi thao tác tay tại cổng (check-in, check-out, sửa biển số...)
/// đều phải đi qua đây để làm bằng chứng tranh chấp. Infrastructure lo việc lưu xuống database.
/// </summary>
public interface IGateEventWriter
{
    Task<GateEventRecord> WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>Bản ghi GateEvent trả về sau khi lưu (US-048 cần Id sự kiện cho audit/API).</summary>
public sealed record GateEventRecord(int Id, int ParkingLotId, string EventType, int? ParkingSessionId,
    string? PlateNumber, int? PerformedByUserId, string? Note, int? ShiftId, DateTime CreatedAtUtc);

// ===== READ SIDE (US-048 audit danh sách ngoại lệ + US-077 thống kê hủy No-show của Staff) =====
public sealed record GateEventDto(int Id, int ParkingLotId, string EventType, int? ParkingSessionId,
    string? PlateNumber, string? PlateDisplay, int? PerformedByUserId, string? Note, DateTime CreatedAtUtc);

/// <summary>US-077: số lượt hủy No-show theo Staff trong khoảng thời gian (nguồn: GateEvents.NoShowCancel).</summary>
public sealed record StaffNoShowCancelStat(int ParkingLotId, int? StaffUserId, int CancelCount, DateTime? LastCancelAtUtc);

public interface IGateEventQueries
{
    /// <summary>US-048: danh sách sự kiện tại cổng để audit/đối soát, lọc theo loại + khoảng thời gian.</summary>
    Task<IReadOnlyList<GateEventDto>> ListAsync(int parkingLotId, GateEventType? eventType,
        DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken cancellationToken);

    /// <summary>US-077: nhóm số lượt hủy No-show theo (bãi, staff) trong khoảng thời gian.</summary>
    Task<IReadOnlyList<StaffNoShowCancelStat>> NoShowCancellationStatsAsync(DateTime fromUtc, DateTime toUtc,
        int? parkingLotId, CancellationToken cancellationToken);
}
