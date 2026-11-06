using VehicleService.Application.Interfaces;

namespace VehicleService.Infrastructure.Integration;

/// <summary>
/// Adapter giao tiếp với BookingService.
/// Trong Sprint 0, dùng bản stub mặc định; sẵn sàng tích hợp gRPC/HTTP client khi BookingService hoàn tất.
/// </summary>
public sealed class BookingIntegrationService : IBookingIntegrationService
{
    public Task<bool> HasActiveBookingAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        // Mặc định ở Sprint 0 chưa có booking active nào từ service ngoài
        return Task.FromResult(false);
    }
}
