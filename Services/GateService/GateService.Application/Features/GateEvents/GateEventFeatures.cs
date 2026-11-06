using ParkingManagement.SharedKernel.Enums;

namespace GateService.Application.Features.GateEvents;

/// <summary>Dữ liệu tối thiểu để ghi một sự kiện tại cổng vào bảng GateEvents.</summary>
public sealed record GateEventDraft(int ParkingLotId, GateEventType EventType, int? ParkingSessionId = null,
    string? PlateNumber = null, int? PerformedByUserId = null, string? Note = null);

/// <summary>
/// Port ghi nhật ký GateEvents – mọi thao tác tay tại cổng (check-in, check-out, sửa biển số...)
/// đều phải đi qua đây để làm bằng chứng tranh chấp. Infrastructure lo việc lưu xuống database.
/// </summary>
public interface IGateEventWriter
{
    Task WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default);
}
