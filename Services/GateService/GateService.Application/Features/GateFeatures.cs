using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features;

public sealed record ParkingSessionDto(int Id, string Code, int ParkingLotId, string PlateNumber, string PlateDisplay, bool IsWalkIn,
    string? BookingCode, string? SlotCode, string Status, DateTime EntryAtUtc, DateTime? ExitAtUtc, int MinutesParked,
    string CheckInMethod, double? EntryOcrConfidence, decimal? Fee);

public interface IParkingSessionQueries
{
    Task<IReadOnlyList<ParkingSessionDto>> ListByLotAsync(int parkingLotId, ParkingSessionStatus? status, DateTime nowUtc, CancellationToken cancellationToken);
    Task<ParkingSessionDto?> FindActiveByPlateAsync(int parkingLotId, string normalizedPlate, DateTime nowUtc, CancellationToken cancellationToken);
}

public interface IListLotSessionsUseCase
{
    Task<IReadOnlyList<ParkingSessionDto>> ExecuteAsync(int parkingLotId, ParkingSessionStatus? status, CancellationToken cancellationToken = default);
}

/// <summary>UC-20: Nhân viên cổng xem các lượt xe trong bãi (mặc định: xe đang đỗ).</summary>
public sealed class ListLotSessionsUseCase(IParkingSessionQueries queries, TimeProvider timeProvider) : IListLotSessionsUseCase
{
    public Task<IReadOnlyList<ParkingSessionDto>> ExecuteAsync(int parkingLotId, ParkingSessionStatus? status, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        return queries.ListByLotAsync(parkingLotId, status, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}

public interface ILookupVehicleAtGateUseCase
{
    Task<ParkingSessionDto?> ExecuteAsync(int parkingLotId, string plate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cổng ra: camera/nhân viên đưa biển số (có thể lẫn lỗi OCR) → chuẩn hoá → tìm lượt đang đỗ.
/// Trả null nếu xe chưa vào bãi (Gate console hiển thị nút CHECK-IN thay vì CHECK-OUT).
/// </summary>
public sealed class LookupVehicleAtGateUseCase(IParkingSessionQueries queries, TimeProvider timeProvider) : ILookupVehicleAtGateUseCase
{
    public Task<ParkingSessionDto?> ExecuteAsync(int parkingLotId, string plate, CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.TryCorrect(plate)
            ?? throw new ValidationException($"Không đọc được biển số hợp lệ từ '{plate}'. Nhân viên kiểm tra và nhập tay.");
        return queries.FindActiveByPlateAsync(parkingLotId, normalized, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
