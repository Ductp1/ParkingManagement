using System.Reflection;
using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using Xunit;

namespace BookingService.Test;

/// <summary>
/// Unit Tests cho T-302: Saga Booking ➔ Payment (Lắng nghe sự kiện PaymentSucceeded / PaymentFailed).
/// Tuân thủ quy chuẩn 3A, kiểm tra chuyển trạng thái State Machine, tính Idempotency và giao dịch bù trừ.
/// </summary>
public class PaymentSagaTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static void SetEntityId(BaseEntity entity, int id)
    {
        typeof(BaseEntity).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, id);
    }

    [Fact]
    public async Task PaymentSucceeded_transitions_to_confirmed_and_updates_paid_amount()
    {
        // Arrange
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.PendingPayment,
            HoldExpiresAtUtc = FixedNow.AddMinutes(15),
            TotalAmount = 60000,
            PaidAmount = 0,
            RequiresOwnerApproval = false
        };
        SetEntityId(booking, 101);

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentSucceeded(
            PaymentId: 501,
            BookingId: 101,
            ParkingSessionId: null,
            Amount: 60000,
            Method: "VNPAY");

        // Act
        var result = await useCase.ProcessPaymentSucceededAsync(evt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(60000, booking.PaidAmount);
        Assert.Contains(booking.StatusLogs, l => l.ToStatus == BookingStatus.Confirmed && l.Reason!.Contains("Thanh toán thành công"));
    }

    [Fact]
    public async Task PaymentSucceeded_when_requires_owner_approval_transitions_to_pending_owner_approval()
    {
        // Arrange: Bãi Mức 0 -> thanh toán xong phải chuyển sang PendingOwnerApproval
        var booking = new Booking
        {
            Code = "BK-20261103-0002",
            UserId = 10,
            Status = BookingStatus.PendingPayment,
            HoldExpiresAtUtc = FixedNow.AddMinutes(15),
            TotalAmount = 50000,
            PaidAmount = 0,
            RequiresOwnerApproval = true
        };
        SetEntityId(booking, 102);

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentSucceeded(502, 102, null, 50000, "VNPAY");

        // Act
        var result = await useCase.ProcessPaymentSucceededAsync(evt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.PendingOwnerApproval, booking.Status);
        Assert.Equal(50000, booking.PaidAmount);
        Assert.Contains(booking.StatusLogs, l => l.ToStatus == BookingStatus.PendingOwnerApproval);
    }

    [Fact]
    public async Task PaymentSucceeded_when_hold_expired_transitions_to_expired_for_compensating_refund()
    {
        // Arrange: Thanh toán thành công nhưng đơn đã quá hạn giữ chỗ 15 phút
        var booking = new Booking
        {
            Code = "BK-20261103-0003",
            UserId = 10,
            Status = BookingStatus.PendingPayment,
            HoldExpiresAtUtc = FixedNow.AddMinutes(-5), // Đã quá hạn 5 phút
            TotalAmount = 70000,
            PaidAmount = 0
        };
        SetEntityId(booking, 103);

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentSucceeded(503, 103, null, 70000, "VNPAY");

        // Act
        var result = await useCase.ProcessPaymentSucceededAsync(evt);

        // Assert: Chuyển sang Expired để kích hoạt Saga bù trừ hoàn tiền
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.Contains(booking.StatusLogs, l => l.ToStatus == BookingStatus.Expired && !string.IsNullOrEmpty(l.Reason));
    }

    [Fact]
    public async Task PaymentSucceeded_is_idempotent_when_already_confirmed()
    {
        // Arrange: Đơn đã Confirmed trước đó (do webhook gửi lặp lại)
        var booking = new Booking
        {
            Code = "BK-20261103-0004",
            UserId = 10,
            Status = BookingStatus.Confirmed,
            TotalAmount = 60000,
            PaidAmount = 60000
        };
        SetEntityId(booking, 104);

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentSucceeded(504, 104, null, 60000, "VNPAY");

        // Act & Assert: Chạy lại không bị lỗi, trạng thái không đổi
        var result = await useCase.ProcessPaymentSucceededAsync(evt);
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task PaymentFailed_transitions_to_cancelled_and_logs_reason()
    {
        // Arrange
        var booking = new Booking
        {
            Code = "BK-20261103-0005",
            UserId = 10,
            Status = BookingStatus.PendingPayment,
            TotalAmount = 80000,
            PaidAmount = 0
        };
        SetEntityId(booking, 105);

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentFailed(PaymentId: 505, BookingId: 105, Reason: "Khách hàng hủy giao dịch trên VNPAY");

        // Act
        var result = await useCase.ProcessPaymentFailedAsync(evt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Contains(booking.StatusLogs, l => l.ToStatus == BookingStatus.Cancelled && l.Reason!.Contains("Khách hàng hủy giao dịch"));
    }

    [Fact]
    public async Task PaymentEvents_without_booking_id_throws_validation_exception()
    {
        var repo = new FakeBookingRepo();
        var queries = new FakeQueries(null);
        var clock = new FixedClock(FixedNow);
        var useCase = new ProcessPaymentResultUseCase(repo, queries, clock);

        var evt = new PaymentSucceeded(506, null, null, 50000, "VNPAY");

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ProcessPaymentSucceededAsync(evt));
    }

    // ==========================================
    // TEST DOUBLES (FAKES)
    // ==========================================

    private sealed class FakeQueries(Booking? entity) : IBookingQueries
    {
        public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            if (entity == null || entity.Code != code) return Task.FromResult<BookingDetailDto?>(null);
            return Task.FromResult<BookingDetailDto?>(new BookingDetailDto(
                entity.Id, entity.Code, entity.Status.ToString(), entity.UserId, entity.VehicleId,
                entity.PlateNumber, entity.VehicleType.ToString(), entity.ParkingLotId, entity.ParkingLotName,
                entity.ZoneId, entity.SlotId, entity.SlotCode, entity.StartAtUtc, entity.EndAtUtc,
                entity.HoldExpiresAtUtc, entity.TotalAmount, entity.DiscountAmount, entity.PaidAmount,
                entity.PromotionCode, null, [], entity.QrToken));
        }

        public Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingSummaryDto>>([]);

        public Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
            DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateBookingListItemDto>>([]);
        public Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
            int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApprovalLogDto>>([]);
    }

    private sealed class FakeBookingRepo : IBookingRepository
    {
        public List<Booking> SavedBookings { get; } = [];
        public List<(string EventType, string PayloadJson)> OutboxMessages { get; } = [];

        public Task<bool> IsSlotAssignedToActiveMonthlyPassAsync(int slotId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountCancellationsTodayAsync(int userId, DateTime todayUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task AddOutboxMessageAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
        {
            OutboxMessages.Add((eventType, payloadJson));
            return Task.CompletedTask;
        }

        public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Code == code));

        public Task<Booking?> GetEntityByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Id == id));

        public Task<bool> HasOverlappingBookingForSlotAsync(int slotId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasOverlappingBookingForUserAsync(int userId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            SavedBookings.Add(booking);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}
