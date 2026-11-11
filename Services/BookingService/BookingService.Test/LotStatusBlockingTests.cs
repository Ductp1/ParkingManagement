using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using BookingService.Domain.Rules;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Test;

/// <summary>
/// US-069: chặn tạo booking mới khi bãi đang TẠM ĐÓNG KHẨN CẤP + FAIL-OPEN khi ParkingService
/// không phản hồi (spec assumption 3 — ParkingService chết không được chặn toàn bộ booking).
/// </summary>
public class LotStatusBlockingTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);

    static LotStatusBlockingTests()
    {
        // QrTokenService đọc secret từ env — khớp cách BookingTests cấu hình cho unit test.
        Environment.SetEnvironmentVariable("QRTOKEN_SECRET", "test-secret-key-for-unit-tests-12345");
    }

    private static CreateBookingCommand SampleCommand(int lotId = 1) => new(
        UserId: 5,
        VehicleId: 1,
        PlateNumber: "51F-123.45",
        VehicleType: VehicleType.Sedan,
        ParkingLotId: lotId,
        OwnerProfileId: 1,
        ParkingLotName: "Bãi xe Vincom Đồng Khởi",
        ZoneId: 1,
        SlotId: 2,
        SlotCode: "B1-A02",
        AllocationMode: AllocationMode.Dynamic,
        StartAtUtc: FixedNow.AddHours(1),
        EndAtUtc: FixedNow.AddHours(3),
        TotalAmount: 60000,
        PromotionCode: null);

    [Fact]
    public async Task CreateBooking_blocked_when_lot_is_emergency_stopped()
    {
        var provider = new FakeLotStatus(new LotOperationalStatusSnapshot(1, "Bãi A", "Suspended", EmergencyStopped: true, "Sự cố cháy"));
        var useCase = new CreateBookingUseCase(new FakeBookingRepo(), new QrTokenService(), new FakeEventPublisher(),
            new BookingAbuseGuard(new AllowAbuseQueries(), new FixedClock(FixedNow)), new NoPmsLotIntegration(),
            new FixedClock(FixedNow), provider);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(SampleCommand()));

        Assert.Contains("TẠM ĐÓNG KHẨN CẤP", ex.Message);
        Assert.Contains("Sự cố cháy", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_fail_open_when_parking_service_unavailable()
    {
        var provider = new FakeLotStatus(null); // ParkingService chết / lỗi HTTP → null
        var repo = new FakeBookingRepo();
        var useCase = new CreateBookingUseCase(repo, new QrTokenService(), new FakeEventPublisher(),
            new BookingAbuseGuard(new AllowAbuseQueries(), new FixedClock(FixedNow)), new NoPmsLotIntegration(),
            new FixedClock(FixedNow), provider);

        var result = await useCase.ExecuteAsync(SampleCommand());

        Assert.Equal("PendingPayment", result.Status);
        Assert.Single(repo.SavedBookings);
    }

    [Fact]
    public async Task CreateBooking_allowed_when_lot_is_active()
    {
        var provider = new FakeLotStatus(new LotOperationalStatusSnapshot(1, "Bãi A", "Active", EmergencyStopped: false, null));
        var useCase = new CreateBookingUseCase(new FakeBookingRepo(), new QrTokenService(), new FakeEventPublisher(),
            new BookingAbuseGuard(new AllowAbuseQueries(), new FixedClock(FixedNow)), new NoPmsLotIntegration(),
            new FixedClock(FixedNow), provider);

        var result = await useCase.ExecuteAsync(SampleCommand());

        Assert.Equal("PendingPayment", result.Status);
    }

    private sealed class FakeLotStatus(LotOperationalStatusSnapshot? snapshot) : ILotOperationalStatusProvider
    {
        public Task<LotOperationalStatusSnapshot?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    /// <summary>US-103: guard chống lạm dụng cho phép mọi tài khoản (test US-069 chỉ quan tâm chặn bãi đóng).</summary>
    private sealed class AllowAbuseQueries : IBookingAbuseQueries
    {
        public Task<int> CountCreatedSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<int> CountCancellationsSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<NoShowStatistics> GetNoShowStatisticsAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new NoShowStatistics(0, 0));
    }

    /// <summary>US-052: bãi không cấu hình PMS — push luôn trả false, không ảnh hưởng booking.</summary>
    private sealed class NoPmsLotIntegration : ILotIntegrationClient
    {
        public Task<bool> PushBookingCreatedAsync(PmsBookingPush booking, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeBookingRepo : IBookingRepository
    {
        public List<Booking> SavedBookings { get; } = [];

        public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Code == code));

        public Task<Booking?> GetEntityByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Id == id));

        public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            SavedBookings.Add(booking);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeEventPublisher : IBookingEventPublisher
    {
        public List<IntegrationEvent> Events { get; } = [];
        public void Enqueue(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}