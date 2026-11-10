namespace BookingService.Application.Features.Bookings;

/// <summary>
/// US-069: snapshot trạng thái vận hành của bãi pull từ ParkingService
/// (GET /api/v1/parking-lots/{lotId}/operations-status — endpoint nội bộ service-to-service).
/// </summary>
public sealed record LotOperationalStatusSnapshot(
    int ParkingLotId, string Name, string Status, bool EmergencyStopped, string? EmergencyStopReason);

/// <summary>
/// Port tra cứu trạng thái vận hành bãi. Caller (CreateBookingUseCase) FAIL-OPEN:
/// provider lỗi / ParkingService chết → null → booking vẫn được tạo (spec assumption 3:
/// ưu tiên availability — ParkingService chết không chặn toàn bộ booking).
/// </summary>
public interface ILotOperationalStatusProvider
{
    Task<LotOperationalStatusSnapshot?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default);
}