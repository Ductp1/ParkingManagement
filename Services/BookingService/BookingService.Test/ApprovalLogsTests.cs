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

    [Fact]
    public async Task GetApprovalLogs_filters_by_owner_profile_id_and_isolates_other_owners()
    {
        // Arrange
        var logs = new List<ApprovalLogDto>
        {
            new(1, 101, "BK-0001", "51A-11111", 1, "Bãi Quận 1", "Approved", 5, "Duyệt thành công", FixedNow.AddHours(-2), OwnerProfileId: 10),
            new(2, 102, "BK-0002", "51A-22222", 1, "Bãi Quận 1", "Rejected", 5, "Bãi đầy đột xuất", FixedNow.AddHours(-1), OwnerProfileId: 10),
            new(3, 103, "BK-0003", "51A-33333", 2, "Bãi Quận 3", "Approved", 6, "Duyệt thành công", FixedNow, OwnerProfileId: 20)
        };

        var fakeQueries = new FakeApprovalLogQueries(logs);
        var useCase = new GetApprovalLogsUseCase(fakeQueries);

        // Act - Chủ bãi 10 xem log (không truyền parkingLotId) -> chỉ thấy log của bãi mình (OwnerProfileId = 10)
        var resultOwner10 = await useCase.ExecuteAsync(null, null, null, null, ownerProfileId: 10);

        // Assert
        Assert.NotNull(resultOwner10);
        Assert.Equal(2, resultOwner10.Count);
        Assert.All(resultOwner10, l => Assert.Equal(10, l.OwnerProfileId));

        // Act - Chủ bãi 10 cố tình truyền parkingLotId = 2 của chủ bãi 20 -> trả về rỗng, không lộ log bãi khác
        var resultHacked = await useCase.ExecuteAsync(2, null, null, null, ownerProfileId: 10);
        Assert.Empty(resultHacked);

        // Act - Admin (ownerProfileId = null) xem tất cả -> thấy đủ cả 3
        var resultAdmin = await useCase.ExecuteAsync(null, null, null, null, ownerProfileId: null);
        Assert.Equal(3, resultAdmin.Count);
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

        public Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, int gracePeriodMinutes, string? search, int? ownerProfileId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GateBookingListItemDto>>([]);

        public Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
            int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, int? ownerProfileId = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<ApprovalLogDto> query = sampleLogs;

            if (ownerProfileId.HasValue && ownerProfileId > 0)
                query = query.Where(l => l.OwnerProfileId == ownerProfileId.Value);

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

