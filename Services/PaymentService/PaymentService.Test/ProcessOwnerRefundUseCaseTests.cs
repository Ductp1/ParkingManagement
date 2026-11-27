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
/// Bộ kiểm thử đơn vị cho ca sử dụng chủ bãi chủ động hoàn tiền khi lỗi tại bãi (US-041, UC-36).
/// Tuân thủ quy tắc đặt tên phương thức kiểm thử: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class ProcessOwnerRefundUseCaseTests
{
    /// <summary>
    /// Giả lập kho dữ liệu hoàn tiền trong bộ nhớ RAM để đảm bảo cô lập tuyệt đối tầng Application.
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

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 11, 0, 0, TimeSpan.Zero);

    private static Payment CreateSamplePayment(
        int id = 1,
        int ownerProfileId = 2,
        PaymentStatus status = PaymentStatus.Succeeded,
        decimal amount = 100_000m,
        decimal refundedAmount = 0m,
        int? bookingId = 200)
    {
        var payment = new Payment
        {
            Code = $"PM-SAMPLE-{id}",
            OwnerProfileId = ownerProfileId,
            ParkingLotId = 1,
            Status = status,
            Amount = amount,
            RefundedAmount = refundedAmount,
            BookingId = bookingId,
            BookingCode = bookingId.HasValue ? $"BK-{bookingId}" : null,
            Method = PaymentMethod.Vnpay,
            Purpose = PaymentPurpose.Booking
        };

        // Gán Id được bảo vệ của BaseEntity phục vụ việc test
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(payment, id);
        return payment;
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_ThrowsArgumentNullException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        // Hành động & Kiểm chứng (Act & Assert)
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task ExecuteAsync_ZeroOrNegativePaymentId_ThrowsValidationException(int invalidPaymentId)
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: invalidPaymentId,
            OwnerProfileId: 2,
            Amount: 50_000m,
            ReasonDescription: "Sự cố cúp điện bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("PaymentId phải là số nguyên dương.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_ZeroOrNegativeOwnerProfileId_ThrowsValidationException(int invalidOwnerId)
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: invalidOwnerId,
            Amount: 50_000m,
            ReasonDescription: "Sự cố cúp điện bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("OwnerProfileId không hợp lệ.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public async Task ExecuteAsync_ZeroOrNegativeAmount_ThrowsValidationException(decimal invalidAmount)
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: invalidAmount,
            ReasonDescription: "Sự cố cúp điện bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Số tiền hoàn bồi thường phải lớn hơn 0 VNĐ.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_NullOrWhiteSpaceReasonDescription_ThrowsValidationException(string? invalidReason)
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 50_000m,
            ReasonDescription: invalidReason!);

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Lý do hoàn tiền không được để trống.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentNotFound_ThrowsNotFoundException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 9999,
            OwnerProfileId: 2,
            Amount: 50_000m,
            ReasonDescription: "Sự cố hệ thống bãi");

        // Hành động & Kiểm chứng (Act & Assert)
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_WrongOwnerProfileId_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        // Owner 99 cố gắng hoàn tiền cho đơn thuộc Owner 2
        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 99,
            Amount: 50_000m,
            ReasonDescription: "Sự cố bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Chủ bãi không có quyền xử lý hoàn tiền cho giao dịch của bãi xe khác.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentStatusPending_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Pending);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 50_000m,
            ReasonDescription: "Sự cố bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Không thể hoàn tiền cho giao dịch có trạng thái 'Pending'", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentStatusFailed_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Failed);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 50_000m,
            ReasonDescription: "Sự cố bãi xe");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Không thể hoàn tiền cho giao dịch có trạng thái 'Failed'", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentAlreadyFullyRefunded_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Succeeded, amount: 100_000m, refundedAmount: 100_000m);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 10_000m,
            ReasonDescription: "Hoàn thêm");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Giao dịch này đã được hoàn đủ 100% trước đó, không thể hoàn thêm.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_AmountExceedsRemainingAmount_ThrowsValidationException()
    {
        // Sắp xếp (Arrange): Tổng 100.000đ, đã hoàn 70.000đ -> còn lại 30.000đ
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.PartiallyRefunded, amount: 100_000m, refundedAmount: 70_000m);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        // Yêu cầu hoàn 40.000đ (vượt quá 30.000đ còn lại)
        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 40_000m,
            ReasonDescription: "Hoàn quá số tiền còn lại");

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("vượt quá số tiền còn lại có thể hoàn", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PartialRefund_UpdatesStatusToPartiallyRefundedAndCompletedRefund()
    {
        // Sắp xếp (Arrange): Đơn 100.000đ, chưa hoàn đồng nào
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Succeeded, amount: 100_000m, refundedAmount: 0m);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        // Hoàn một phần 30.000đ
        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 30_000m,
            ReasonDescription: "Hỏng barrier phải chờ 15 phút",
            ComplaintId: 55);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng DTO phản hồi (Assert)
        Assert.NotNull(result);
        Assert.Equal(1, result.PaymentId);
        Assert.Equal(30_000m, result.RefundAmount);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("LotFault", result.Reason);
        Assert.Contains("Chủ bãi đã duyệt hoàn tiền", result.Message);

        // Kiểm chứng cập nhật trạng thái đơn Payment
        Assert.Equal(30_000m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);

        // Kiểm chứng bản ghi Refund đã lưu
        Assert.Single(repo.SavedRefunds);
        var savedRefund = repo.SavedRefunds[0];
        Assert.Equal(1, savedRefund.PaymentId);
        Assert.Equal(30_000m, savedRefund.Amount);
        Assert.Equal(RefundReason.LotFault, savedRefund.Reason);
        Assert.Equal(RefundStatus.Completed, savedRefund.Status);
        Assert.Equal(2, savedRefund.RequestedByUserId);
        Assert.Equal(55, savedRefund.ComplaintId);
        Assert.Equal("Hỏng barrier phải chờ 15 phút", savedRefund.Note);
        Assert.Equal(FixedNow.UtcDateTime, savedRefund.ProcessedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_FullRefund_UpdatesStatusToRefunded()
    {
        // Sắp xếp (Arrange): Đơn 100.000đ, hoàn toàn bộ 100.000đ
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Succeeded, amount: 100_000m, refundedAmount: 0m);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 100_000m,
            ReasonDescription: "Bãi xe cúp điện không thể vào gửi");

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.Equal(100_000m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal("Refunded", payment.Status.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_Success_GeneratesOutboxMessageWithRefundIssuedEvent()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRefundRepository();
        var payment = CreateSamplePayment(id: 1, ownerProfileId: 2, status: PaymentStatus.Succeeded, amount: 80_000m, bookingId: 300);
        repo.Payments[payment.Id] = payment;

        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new ProcessOwnerRefundUseCase(repo, timeProvider);

        var command = new ProcessOwnerRefundCommand(
            PaymentId: 1,
            OwnerProfileId: 2,
            Amount: 40_000m,
            ReasonDescription: "Sự cố nhân viên tính sai tiền");

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng OutboxMessage (Assert)
        Assert.Single(repo.SavedOutboxMessages);
        var outbox = repo.SavedOutboxMessages[0];
        Assert.Equal(nameof(RefundIssued), outbox.EventType);
        Assert.Equal(FixedNow.UtcDateTime, outbox.OccurredAtUtc);

        var eventPayload = JsonSerializer.Deserialize<RefundIssued>(outbox.PayloadJson);
        Assert.NotNull(eventPayload);
        Assert.Equal(1, eventPayload.PaymentId);
        Assert.Equal(300, eventPayload.BookingId);
        Assert.Equal(40_000m, eventPayload.Amount);
    }
}
