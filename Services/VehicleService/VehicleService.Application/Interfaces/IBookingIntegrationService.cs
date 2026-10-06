namespace VehicleService.Application.Interfaces;

/// <summary>
/// Port giao tiếp với BookingService để kiểm tra trạng thái đặt chỗ của xe (US-011).
/// </summary>
public interface IBookingIntegrationService
{
    /// <summary>
    /// Kiểm tra xem xe có đang nằm trong booking hoạt động (Confirmed hoặc Check-in) không.
    /// </summary>
    Task<bool> HasActiveBookingAsync(int vehicleId, CancellationToken cancellationToken = default);
}
