using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Payments;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho ca sử dụng tạo thanh toán VNPAY (US-032).
/// Sử dụng Fake Repository và Fake Gateway trong bộ nhớ RAM để cô lập hoàn toàn logic tầng Application.
/// Quy tắc đặt tên phương thức kiểm thử: TenHam_TrangThai_KetQua.
/// </summary>
public class CreateVnpayPaymentUseCaseTests
{
    /// <summary>
    /// Giả lập kho dữ liệu thanh toán lưu trữ tạm trên danh sách RAM.
    /// </summary>
    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public readonly List<Payment> SavedPayments = [];

        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            SavedPayments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<Payment?> GetByCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            var found = SavedPayments.FirstOrDefault(p => p.Code == paymentCode);
            return Task.FromResult(found);
        }

        public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Giả lập cổng VNPAY sinh URL giả mà không cần cấu hình HMAC hay mạng internet.
    /// </summary>
    private sealed class FakeVnpayGateway : IVnpayGateway
    {
        public string LastGeneratedUrl { get; private set; } = string.Empty;

        public string GeneratePaymentUrl(
            string paymentCode,
            decimal amount,
            string orderDescription,
            string ipAddress,
            DateTime createdAtUtc,
            DateTime expiresAtUtc)
        {
            LastGeneratedUrl = $"https://sandbox.vnpayment.vn/test?vnp_TxnRef={paymentCode}&vnp_Amount={amount * 100}";
            return LastGeneratedUrl;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(-50000)]
    public async Task ExecuteAsync_ZeroOrNegativeAmount_ThrowsValidationException(decimal invalidAmount)
    {
        // Khởi tạo đối tượng cần test với repository và gateway giả
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var command = new CreateVnpayPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0001",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: invalidAmount,
            IpAddress: "127.0.0.1");

        // Xác nhận ném ngoại lệ ValidationException khi số tiền không hợp lệ
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Số tiền thanh toán phải lớn hơn 0 đồng", ex.Message);
        Assert.Empty(repo.SavedPayments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task ExecuteAsync_ZeroOrNegativeBookingId_ThrowsValidationException(int invalidBookingId)
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var command = new CreateVnpayPaymentCommand(
            BookingId: invalidBookingId,
            BookingCode: "BK-0001",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000m,
            IpAddress: "127.0.0.1");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Mã đặt chỗ (bookingId) không hợp lệ", ex.Message);
        Assert.Empty(repo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PersistsPaymentWithPendingStatusAndReturnsUrl()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var command = new CreateVnpayPaymentCommand(
            BookingId: 100,
            BookingCode: "BK-0002",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 70000m,
            IpAddress: "192.168.1.100");

        var result = await useCase.ExecuteAsync(command);

        // Kiểm tra URL và mã giao dịch trả về
        Assert.NotNull(result);
        Assert.StartsWith("PAY_", result.PaymentCode);
        Assert.Contains("sandbox.vnpayment.vn", result.PaymentUrl);
        Assert.Contains(result.PaymentCode, result.PaymentUrl);

        // Kiểm tra bản ghi lưu trong database
        Assert.Single(repo.SavedPayments);
        var savedPayment = repo.SavedPayments[0];
        Assert.Equal(result.PaymentCode, savedPayment.Code);
        Assert.Equal(100, savedPayment.BookingId);
        Assert.Equal("BK-0002", savedPayment.BookingCode);
        Assert.Equal(70000m, savedPayment.Amount);
        Assert.Equal(PaymentStatus.Pending, savedPayment.Status);
        Assert.Equal(PaymentMethod.Vnpay, savedPayment.Method);
        Assert.Equal(PaymentPurpose.Booking, savedPayment.Purpose);
        Assert.NotEmpty(savedPayment.IdempotencyKey);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_Sets15MinutesExpiration()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var beforeCall = DateTime.UtcNow;
        var command = new CreateVnpayPaymentCommand(
            BookingId: 101,
            BookingCode: "BK-0003",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 30000m,
            IpAddress: "127.0.0.1");

        var result = await useCase.ExecuteAsync(command);
        var afterCall = DateTime.UtcNow;

        // Phiên hết hạn phải cách thời điểm gọi xấp xỉ 15 phút
        Assert.True(result.ExpiresAtUtc >= beforeCall.AddMinutes(14).AddSeconds(50));
        Assert.True(result.ExpiresAtUtc <= afterCall.AddMinutes(15).AddSeconds(10));

        var savedPayment = repo.SavedPayments[0];
        Assert.Equal(result.ExpiresAtUtc, savedPayment.ExpiresAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_NullUserIdWalkInCustomer_SuccessfullyCreatesPayment()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        // Tài xế vãng lai vào bãi thanh toán tại cổng (UserId = null)
        var command = new CreateVnpayPaymentCommand(
            BookingId: 102,
            BookingCode: "BK-WALKIN-01",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: null,
            Amount: 20000m,
            IpAddress: "127.0.0.1");

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Null(repo.SavedPayments[0].UserId);
        Assert.Equal(PaymentStatus.Pending, repo.SavedPayments[0].Status);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOrWhitespaceIpAddress_PassesToGatewayWithoutError()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var command = new CreateVnpayPaymentCommand(
            BookingId: 103,
            BookingCode: "BK-0004",
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000m,
            IpAddress: "");

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Single(repo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_CalledTwice_GeneratesUniquePaymentCodesAndIdempotencyKeys()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway();
        var useCase = new CreateVnpayPaymentUseCase(repo, gateway);

        var command1 = new CreateVnpayPaymentCommand(1, "BK-0001", 1, 2, 5, 50000m, "127.0.0.1");
        var command2 = new CreateVnpayPaymentCommand(1, "BK-0001", 1, 2, 5, 50000m, "127.0.0.1");

        var result1 = await useCase.ExecuteAsync(command1);
        var result2 = await useCase.ExecuteAsync(command2);

        // 2 lần gọi phải sinh ra mã giao dịch khác nhau và IdempotencyKey khác nhau
        Assert.NotEqual(result1.PaymentCode, result2.PaymentCode);
        Assert.NotEqual(repo.SavedPayments[0].IdempotencyKey, repo.SavedPayments[1].IdempotencyKey);
    }
}
