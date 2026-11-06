using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using Xunit;

namespace BookingService.Test;

/// <summary>
/// Unit Tests cho US-029 (Sửa booking trước Check-in) và US-030 (Gia hạn thời gian đỗ xe).
/// Tuân thủ chuẩn 3A (Arrange - Act - Assert), kiểm tra chặt chẽ các case thành công và ném ngoại lệ nghiệp vụ.
/// </summary>
public class BookingModificationAndExtensionTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    // ==========================================
    // US-029: SỬA BOOKING TRƯỚC CHECK-IN
    // ==========================================

    [Fact]
    public async Task ModifyBooking_success_updates_vehicle_and_time_and_records_modifications()
    {
        // Arrange
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            VehicleId = 1,
            PlateNumber = "51F-123.45",
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddHours(2), // 120 phút > 60 phút
            EndAtUtc = FixedNow.AddHours(4)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ModifyBookingUseCase(repo, queries, clock);

        var cmd = new ModifyBookingCommand(
            UserId: 10,
            NewVehicleId: 99,
            NewPlateNumber: "51H-999.99",
            NewStartAtUtc: FixedNow.AddHours(3),
            NewEndAtUtc: FixedNow.AddHours(5));

        // Act
        var result = await useCase.ExecuteAsync(booking.Code, cmd);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(FixedNow.AddHours(3), booking.StartAtUtc);
        Assert.Equal(FixedNow.AddHours(5), booking.EndAtUtc);
        Assert.Equal(99, booking.VehicleId);
        Assert.Equal("51H-999.99", booking.PlateNumber);

        // Kiểm tra audit log BookingModification
        Assert.Equal(2, booking.Modifications.Count);
        Assert.Contains(booking.Modifications, m => m.ModificationType == BookingModificationType.ChangeTime
            && m.OldStartAtUtc == FixedNow.AddHours(2)
            && m.NewStartAtUtc == FixedNow.AddHours(3)
            && m.NewEndAtUtc == FixedNow.AddHours(5));
        Assert.Contains(booking.Modifications, m => m.ModificationType == BookingModificationType.ChangeVehicle
            && m.OldVehicleId == 1
            && m.NewVehicleId == 99);
    }

    [Fact]
    public async Task ModifyBooking_under_60_minutes_before_start_throws_validation_exception()
    {
        // Arrange: Đặt giờ bắt đầu chỉ còn 45 phút (< 60 phút chống lách luật)
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddMinutes(45),
            EndAtUtc = FixedNow.AddHours(2)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ModifyBookingUseCase(repo, queries, clock);

        var cmd = new ModifyBookingCommand(
            UserId: 10,
            NewVehicleId: null,
            NewPlateNumber: null,
            NewStartAtUtc: FixedNow.AddMinutes(50),
            NewEndAtUtc: FixedNow.AddHours(3));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(booking.Code, cmd));
        Assert.Contains("60 phút", ex.Message);
    }

    [Fact]
    public async Task ModifyBooking_when_start_time_in_the_past_throws_validation_exception()
    {
        // Arrange: Giờ bắt đầu đã qua (quá khứ)
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddMinutes(-5),
            EndAtUtc = FixedNow.AddHours(2)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ModifyBookingUseCase(repo, queries, clock);

        var cmd = new ModifyBookingCommand(
            UserId: 10,
            NewVehicleId: null,
            NewPlateNumber: null,
            NewStartAtUtc: FixedNow.AddHours(1),
            NewEndAtUtc: FixedNow.AddHours(3));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(booking.Code, cmd));
        Assert.Contains("Không thể sửa booking khi đã đến hoặc quá giờ bắt đầu", ex.Message);
    }

    [Fact]
    public async Task ModifyBooking_when_status_is_checkedin_throws_validation_exception()
    {
        // Arrange: Xe đã check-in vào bãi thì không được sửa thông tin booking nữa
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.CheckedIn,
            StartAtUtc = FixedNow.AddHours(2),
            EndAtUtc = FixedNow.AddHours(4)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new ModifyBookingUseCase(repo, queries, clock);

        var cmd = new ModifyBookingCommand(
            UserId: 10,
            NewVehicleId: null,
            NewPlateNumber: null,
            NewStartAtUtc: FixedNow.AddHours(3),
            NewEndAtUtc: FixedNow.AddHours(5));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(booking.Code, cmd));
        Assert.Contains("Chỉ có thể sửa booking khi ở trạng thái Confirmed hoặc PendingPayment", ex.Message);
    }

    // ==========================================
    // US-030: GIA HẠN THỜI GIAN ĐỖ XE
    // ==========================================

    [Fact]
    public async Task ExtendBooking_success_extends_end_time_and_records_modification()
    {
        // Arrange: Xe đang đỗ (CheckedIn) muốn gia hạn thêm giờ
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.CheckedIn,
            StartAtUtc = FixedNow.AddHours(-1),
            EndAtUtc = FixedNow.AddHours(1)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var useCase = new ExtendBookingUseCase(repo, queries);

        var cmd = new ExtendBookingCommand(UserId: 10, NewEndAtUtc: FixedNow.AddHours(3));

        // Act
        var result = await useCase.ExecuteAsync(booking.Code, cmd);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(FixedNow.AddHours(3), booking.EndAtUtc);
        Assert.Single(booking.Modifications);

        var mod = booking.Modifications.First();
        Assert.Equal(BookingModificationType.ChangeTime, mod.ModificationType);
        Assert.Equal(FixedNow.AddHours(1), mod.OldEndAtUtc);
        Assert.Equal(FixedNow.AddHours(3), mod.NewEndAtUtc);
        Assert.Equal(10, mod.RequestedByUserId);
    }

    [Fact]
    public async Task ExtendBooking_with_earlier_or_equal_end_time_throws_validation_exception()
    {
        // Arrange: Gia hạn nhưng giờ mới lại bằng hoặc nhỏ hơn giờ kết thúc cũ
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.Parking,
            StartAtUtc = FixedNow.AddHours(-1),
            EndAtUtc = FixedNow.AddHours(2)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var useCase = new ExtendBookingUseCase(repo, queries);

        var cmd = new ExtendBookingCommand(UserId: 10, NewEndAtUtc: FixedNow.AddHours(2)); // Bằng giờ cũ

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(booking.Code, cmd));
        Assert.Contains("Thời gian gia hạn mới phải sau thời gian kết thúc hiện tại", ex.Message);
    }

    [Fact]
    public async Task ExtendBooking_when_status_is_cancelled_throws_validation_exception()
    {
        // Arrange: Booking đã bị hủy thì không được gia hạn
        var booking = new Booking
        {
            Code = "BK-20261103-0001",
            UserId = 10,
            Status = BookingStatus.Cancelled,
            StartAtUtc = FixedNow.AddHours(-2),
            EndAtUtc = FixedNow.AddHours(1)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var useCase = new ExtendBookingUseCase(repo, queries);

        var cmd = new ExtendBookingCommand(UserId: 10, NewEndAtUtc: FixedNow.AddHours(3));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(booking.Code, cmd));
        Assert.Contains("Chỉ có thể gia hạn khi đang đỗ hoặc đã xác nhận", ex.Message);
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

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}
