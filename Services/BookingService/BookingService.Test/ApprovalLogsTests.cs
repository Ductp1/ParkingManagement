using BookingService.Application.Features.Bookings;
using ParkingManagement.SharedKernel.Exceptions;
using Xunit;

namespace BookingService.Test;

/// <summary>
/// Unit Tests cho T-303: Tra cứu lịch sử duyệt / từ chối đơn đặt chỗ bãi Mức 0 (Owner / Admin Portal).
/// Tuân thủ quy chuẩn 3A, kiểm tra lọc theo bãi đỗ, kết quả duyệt và khoảng thời gian.
/// </summary>
public class ApprovalLogsTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetApprovalLogs_when_to_utc_before_from_utc_throws_validation_exception()
    {
        // Arrange
        var fakeQueries = new FakeApprovalLogQueries([]);
        var useCase = new GetApprovalLogsUseCase(fakeQueries);

        var fromUtc = FixedNow;
        var toUtc = FixedNow.AddDays(-1); // to < from -> Invalid

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(null, fromUtc, toUtc, null));
    }

    [Fact]
    public async Task GetApprovalLogs_returns_matching_records()
    {
        // Arrange
        var logs = new List<ApprovalLogDto>
        {
            new(1, 101, "BK-0001", "51A-11111", 1, "Bãi Quận 1", "Approved", 5, "Duyệt thành công", FixedNow.AddHours(-2)),
            new(2, 102, "BK-0002", "51A-22222", 1, "Bãi Quận 1", "Rejected", 5, "Bãi đầy đột xuất", FixedNow.AddHours(-1)),
            new(3, 103, "BK-0003", "51A-33333", 2, "Bãi Quận 3", "Approved", 6, "Duyệt thành công", FixedNow)
        };

        var fakeQueries = new FakeApprovalLogQueries(logs);
        var useCase = new GetApprovalLogsUseCase(fakeQueries);

        // Act
        var result = await useCase.ExecuteAsync(1, null, null, "Approved");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("BK-0001", result[0].BookingCode);
        Assert.Equal("Approved", result[0].Result);
    }

    [Fact]
    public async Task GetApprovalLogs_filters_by_parking_lot_id()
    {
        // Arrange
        var logs = new List<ApprovalLogDto>
        {
            new(1, 101, "BK-0001", "51A-11111", 1, "Bãi Quận 1", "Approved", 5, "Duyệt thành công", FixedNow.AddHours(-2)),
            new(2, 102, "BK-0002", "51A-22222", 1, "Bãi Quận 1", "Rejected", 5, "Bãi đầy đột xuất", FixedNow.AddHours(-1)),
            new(3, 103, "BK-0003", "51A-33333", 2, "Bãi Quận 3", "Approved", 6, "Duyệt thành công", FixedNow)
        };

        var fakeQueries = new FakeApprovalLogQueries(logs);
        var useCase = new GetApprovalLogsUseCase(fakeQueries);

        // Act
        var result = await useCase.ExecuteAsync(2, null, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2, result[0].ParkingLotId);
        Assert.Equal("BK-0003", result[0].BookingCode);
    }

    // ==========================================
    // TEST DOUBLES (FAKES)
    // ==========================================

    private sealed class FakeApprovalLogQueries(List<ApprovalLogDto> sampleLogs) : IBookingQueries
    {
        public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken)
            => Task.FromResult<BookingDetailDto?>(null);

        public Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, ParkingManagement.SharedKernel.Enums.BookingStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingSummaryDto>>([]);

        public Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateBookingListItemDto>>([]);

        public Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
            int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
        {
            IEnumerable<ApprovalLogDto> query = sampleLogs;

            if (parkingLotId.HasValue && parkingLotId > 0)
                query = query.Where(l => l.ParkingLotId == parkingLotId.Value);

            if (fromUtc.HasValue)
                query = query.Where(l => l.CreatedAtUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(l => l.CreatedAtUtc <= toUtc.Value);

            if (!string.IsNullOrWhiteSpace(result))
                query = query.Where(l => string.Equals(l.Result, result, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult<IReadOnlyList<ApprovalLogDto>>(query.OrderByDescending(l => l.CreatedAtUtc).ToList());
        }
    }
}

