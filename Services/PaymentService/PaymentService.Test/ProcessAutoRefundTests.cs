using System.Reflection;
using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Refunds;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho ca sử dụng hoàn tiền tự động khi lệch thanh toán và booking (US-034).
/// Quy tắc đặt tên phương thức: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class ProcessAutoRefundTests
{
    /// <summary>
    /// Giả lập kho dữ liệu hoàn tiền trong bộ nhớ RAM để cô lập tầng Application.
    /// </summary>
    private sealed class FakeRefundRepository : IRefundRepository
    {
        public readonly Dictionary<int, Payment> Payments = [];
        public readonly List<Refund> SavedRefunds = [];
        public readonly List<OutboxMessage> SavedOutboxMessages = [];

        public Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken)
        {
            Payments.TryGetValue(paymentId, out var payment);
            return Task.FromResult(payment);
        }

        public Task<Refund?> GetRefundByIdAsync(int refundId, CancellationToken cancellationToken)
        {
            var refund = SavedRefunds.FirstOrDefault(r => r.Id == refundId);
            return Task.FromResult(refund);
        }

        public Task SaveAutoRefundTransactionAsync(
            Payment payment, 
            Refund refund, 
            OutboxMessage? outboxMessage, 
            CancellationToken cancellationToken)
        {
            Payments[payment.Id] = payment;
            SavedRefunds.Add(refund);
            if (outboxMessage != null)
            {
                SavedOutboxMessages.Add(outboxMessage);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Giả lập TimeProvider với thời gian cố định.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 4, 14, 0, 0, TimeSpan.Zero);

    private static Payment CreateSamplePayment(
        int id = 1,
        PaymentStatus status = PaymentStatus.Succeeded,
        decimal amount = 50_000m,
        decimal refundedAmount = 0m,
        int? bookingId = 100)
    {
        var payment = new Payment
        {
            BookingId = bookingId,
            ParkingLotId = 1,
            OwnerProfileId = 2,
            UserId = 10,
            Purpose = PaymentPurpose.Booking,
            Method = PaymentMethod.Vnpay,
            Status = status,
            Amount = amount,
            RefundedAmount = refundedAmount,
            Code = $"PAY-TEST-{id}"
        };

        // Gán Id được bảo vệ của BaseEntity phục vụ việc test
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(payment, id);
        return payment;
    }

    [Fact]
    public async Task ExecuteAsync_ZeroPaymentId_ThrowsValidationException()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(0, IsLotFull: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("PaymentId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NegativePaymentId_ThrowsValidationException()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(-10, IsLotFull: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("PaymentId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(999, IsLotFull: true);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Cancelled)]
    public async Task ExecuteAsync_PaymentNotSucceeded_ThrowsValidationException(PaymentStatus invalidStatus)
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: invalidStatus);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Giao dịch phải ở trạng thái 'Succeeded'", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentAlreadyFullyRefunded_ThrowsValidationException()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 50_000m, refundedAmount: 50_000m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("đã được hoàn đủ 100% từ trước", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_LotStillAvailable_ReturnsRetryBookingActionWithoutRefund()
    {
        // Arrange: Bãi xe vẫn còn chỗ (IsLotFull = false) -> Ưu tiên kích hoạt lại booking (AC1)
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 60_000m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: false, FailureReason: "Timeout kết nối slot");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal("RetryNewBooking", result.ActionTaken);
        Assert.Equal("Retrying", result.Status);
        Assert.Equal(0m, result.RefundAmount);
        Assert.Null(result.RefundId);
        Assert.Empty(fakeRepo.SavedRefunds);
        Assert.Empty(fakeRepo.SavedOutboxMessages);
    }

    [Fact]
    public async Task ExecuteAsync_LotFullAndNoPriorRefund_CreatesFullRefundAndSetsPaymentRefunded()
    {
        // Arrange: Bãi hết chỗ (IsLotFull = true) -> Hoàn trả 100% (AC1 & AC2)
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 75_000m, refundedAmount: 0m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true, FailureReason: "Bãi đã hết chỗ");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal("Refunded100Percent", result.ActionTaken);
        Assert.Equal(75_000m, result.RefundAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(75_000m, payment.RefundedAmount);

        Assert.Single(fakeRepo.SavedRefunds);
        var refund = fakeRepo.SavedRefunds[0];
        Assert.Equal(75_000m, refund.Amount);
        Assert.Equal(RefundReason.PaymentBookingMismatch, refund.Reason);
        Assert.Null(refund.RequestedByUserId); // SYSTEM tự thực hiện
        Assert.Equal(RefundStatus.Completed, refund.Status);
        Assert.Equal(FixedNow.UtcDateTime, refund.ProcessedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_LotFullAndPartiallyRefunded_RefundsRemainingAmountAndSetsPaymentRefunded()
    {
        // Arrange: Đơn đã từng được hoàn 20,000đ -> Lần này hoàn nốt 30,000đ còn lại
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.PartiallyRefunded, amount: 50_000m, refundedAmount: 20_000m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal(30_000m, result.RefundAmount);
        Assert.Equal(50_000m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Single(fakeRepo.SavedRefunds);
        Assert.Equal(30_000m, fakeRepo.SavedRefunds[0].Amount);
    }

    [Fact]
    public async Task ExecuteAsync_AutoRefundSuccess_EmitsRefundIssuedIntegrationEvent()
    {
        // Arrange: Kiểm tra OutboxMessage phát sinh đúng sự kiện RefundIssued (AC3)
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 100_000m, bookingId: 456);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.Single(fakeRepo.SavedOutboxMessages);
        var outboxMsg = fakeRepo.SavedOutboxMessages[0];
        Assert.Equal(nameof(RefundIssued), outboxMsg.EventType);
        Assert.Equal(FixedNow.UtcDateTime, outboxMsg.OccurredAtUtc);

        var payload = JsonSerializer.Deserialize<RefundIssued>(outboxMsg.PayloadJson);
        Assert.NotNull(payload);
        Assert.Equal(payment.Id, payload.PaymentId);
        Assert.Equal(456, payload.BookingId);
        Assert.Equal(100_000m, payload.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_CustomFailureReason_PopulatesRefundNoteWithFailureReason()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 50_000m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var customReason = "Cháy cảm biến cổng bãi số 3";
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true, FailureReason: customReason);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.Single(fakeRepo.SavedRefunds);
        Assert.Contains(customReason, fakeRepo.SavedRefunds[0].Note);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultFailureReason_PopulatesStandardRefundNote()
    {
        // Arrange
        var fakeRepo = new FakeRefundRepository();
        var payment = CreateSamplePayment(1, status: PaymentStatus.Succeeded, amount: 50_000m);
        fakeRepo.Payments[1] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessAutoRefundUseCase(fakeRepo, timeProvider);
        var command = new ProcessAutoRefundCommand(1, IsLotFull: true, FailureReason: null);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.Single(fakeRepo.SavedRefunds);
        Assert.Contains("Hoàn tiền tự động 100% do bãi hết chỗ", fakeRepo.SavedRefunds[0].Note);
    }
}
