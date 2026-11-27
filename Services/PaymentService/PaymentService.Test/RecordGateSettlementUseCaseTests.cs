using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho ca sử dụng thanh toán tại bãi/cổng do nhân viên xác nhận hoặc quét VietQR (US-035, US-048, UC-22 A2, UC-23).
/// Tuân thủ quy tắc đặt tên phương thức kiểm thử: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class RecordGateSettlementUseCaseTests
{
    /// <summary>
    /// Giả lập kho lưu trữ ghi nhận giao dịch thanh toán trong bộ nhớ RAM để đảm bảo cô lập tầng Application.
    /// </summary>
    private sealed class FakePaymentWriter : IPaymentWriter
    {
        public readonly Dictionary<string, Payment> PaymentsByIdempotencyKey = [];
        public readonly List<Payment> AddedPayments = [];
        public bool IsSaved { get; private set; }

        public Task<Payment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        {
            PaymentsByIdempotencyKey.TryGetValue(idempotencyKey, out var payment);
            return Task.FromResult(payment);
        }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            AddedPayments.Add(payment);
            if (!string.IsNullOrEmpty(payment.IdempotencyKey))
            {
                PaymentsByIdempotencyKey[payment.IdempotencyKey] = payment;
            }
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            IsSaved = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Giả lập TimeProvider với thời gian UTC cố định để kiểm thử tính chính xác của thời điểm thanh toán.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_NullOrEmptySessionCode_ThrowsValidationException(string? invalidSessionCode)
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: invalidSessionCode!,
            ParkingSessionId: 101,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 30_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động & Kiểm chứng (Act & Assert)
        var exception = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("SessionCode không được để trống.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ExecuteAsync_ZeroOrNegativeParkingLotId_ThrowsValidationException(int invalidParkingLotId)
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-20261105-001",
            ParkingSessionId: 101,
            ParkingLotId: invalidParkingLotId,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 30_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động & Kiểm chứng (Act & Assert)
        var exception = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("parkingLotId phải là số nguyên dương.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeAmount_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-20261105-001",
            ParkingSessionId: 101,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: -5_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động & Kiểm chứng (Act & Assert)
        var exception = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Số tiền thu tại cổng không được âm.", exception.Message);
    }

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("ZaloPay")]
    [InlineData("Crypto")]
    [InlineData("InvalidMethod")]
    public async Task ExecuteAsync_InvalidPaymentMethod_ThrowsValidationException(string invalidMethod)
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-20261105-001",
            ParkingSessionId: 101,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 30_000m,
            Method: invalidMethod,
            ConfirmedByStaffId: 4);

        // Hành động & Kiểm chứng (Act & Assert)
        var exception = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("không hợp lệ (Cash hoặc VietQr)", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCashSettlement_ReturnsSucceededPaymentDto()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-20261105-001",
            ParkingSessionId: 101,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 20_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("Cash", result.Method);
        Assert.Equal(20_000m, result.Amount);
        Assert.StartsWith("PM-", result.Code);
        Assert.True(writer.IsSaved);
        Assert.Single(writer.AddedPayments);

        var savedPayment = writer.AddedPayments[0];
        Assert.Equal(PaymentStatus.Succeeded, savedPayment.Status);
        Assert.Equal(PaymentMethod.Cash, savedPayment.Method);
        Assert.Equal(PaymentPurpose.WalkIn, savedPayment.Purpose);
        Assert.Equal(4, savedPayment.ConfirmedByStaffId);
        Assert.Equal(FixedNow.UtcDateTime, savedPayment.PaidAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_ValidVietQrSettlement_ReturnsSucceededPaymentDto()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-20261105-002",
            ParkingSessionId: 102,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 35_000m,
            Method: "VietQr",
            ConfirmedByStaffId: 4);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("VietQr", result.Method);
        Assert.Equal(35_000m, result.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_WalkInSessionWithoutBookingCode_PurposeIsWalkIn()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-WALKIN-123",
            ParkingSessionId: 105,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null, // Không có mã booking -> khách vãng lai
            Amount: 15_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        var savedPayment = writer.AddedPayments[0];
        Assert.Equal(PaymentPurpose.WalkIn, savedPayment.Purpose);
        Assert.Null(savedPayment.BookingCode);
    }

    [Fact]
    public async Task ExecuteAsync_OverstaySessionWithBookingCode_PurposeIsOverstay()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-OVERSTAY-456",
            ParkingSessionId: 106,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: "BK-20261105-001", // Có mã booking -> phụ thu quá giờ tại cổng
            Amount: 25_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        var savedPayment = writer.AddedPayments[0];
        Assert.Equal(PaymentPurpose.Overstay, savedPayment.Purpose);
        Assert.Equal("BK-20261105-001", savedPayment.BookingCode);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateCallWithSameSessionCode_ReturnsExistingPaymentWithoutDuplicate()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-IDEMPOTENT-777",
            ParkingSessionId: 107,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 50_000m,
            Method: "Cash",
            ConfirmedByStaffId: 4);

        // Hành động lần 1: Tạo mới thành công
        var firstResult = await useCase.ExecuteAsync(command);

        // Hành động lần 2: Gọi lặp cùng SessionCode (do mất mạng, bấm 2 lần trên ứng dụng)
        var secondResult = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.Equal(firstResult.Code, secondResult.Code);
        Assert.Equal(firstResult.Amount, secondResult.Amount);
        Assert.Single(writer.AddedPayments); // Không được thêm bản ghi thứ 2 vào kho
    }

    [Fact]
    public async Task ExecuteAsync_ValidSettlement_SetsConfirmedByStaffIdAndCorrectUtcTime()
    {
        // Sắp xếp (Arrange)
        var writer = new FakePaymentWriter();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new RecordGateSettlementUseCase(writer, timeProvider);

        var command = new GateSettlementCommand(
            SessionCode: "SES-STAFF-999",
            ParkingSessionId: 108,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            BookingCode: null,
            Amount: 10_000m,
            Method: "Cash",
            ConfirmedByStaffId: 99);

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        var savedPayment = writer.AddedPayments[0];
        Assert.Equal(99, savedPayment.ConfirmedByStaffId);
        Assert.Equal(FixedNow.UtcDateTime, savedPayment.PaidAtUtc);
        Assert.Equal("SES-STAFF-999", savedPayment.TransferContent);
        Assert.Equal("gate-session:SES-STAFF-999", savedPayment.IdempotencyKey);
    }
}
