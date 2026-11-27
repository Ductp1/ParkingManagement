using System.Reflection;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Payments;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho các ca sử dụng xử lý Callback và IPN Webhook VNPAY (US-033).
/// Tuân thủ nghiêm ngặt quy tắc đặt tên phương thức kiểm thử: TenHam_TrangThai_KetQua.
/// </summary>
public class ProcessVnpayCallbackTests
{
    #region Fakes trong bộ nhớ RAM

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public readonly List<Payment> Payments = [];
        public readonly List<PaymentCallbackLog> CallbackLogs = [];
        public readonly List<OutboxMessage> OutboxMessages = [];

        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<Payment?> GetByCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            var payment = Payments.FirstOrDefault(p => p.Code == paymentCode);
            return Task.FromResult(payment);
        }

        public Task SaveCallbackResultAsync(
            Payment? payment, 
            PaymentCallbackLog callbackLog, 
            OutboxMessage? outboxMessage, 
            CancellationToken cancellationToken = default)
        {
            CallbackLogs.Add(callbackLog);

            if (outboxMessage != null)
            {
                OutboxMessages.Add(outboxMessage);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeVnpayGateway : IVnpayGateway
    {
        public bool ShouldSignatureBeValid { get; set; } = true;

        public string GeneratePaymentUrl(
            string paymentCode,
            decimal amount,
            string orderInfo,
            string ipAddress,
            DateTime createdAtUtc,
            DateTime expiresAtUtc)
        {
            return "https://sandbox.vnpayment.vn/test";
        }

        public bool ValidateCallbackSignature(IReadOnlyDictionary<string, string> parameters)
        {
            return ShouldSignatureBeValid;
        }
    }

    #endregion

    #region Helper tạo dữ liệu mẫu

    private static void SetId(BaseEntity entity, int id)
        => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);

    private static Payment CreatePendingPayment(string code = "PAY_20261028120000_A1B2C3", decimal amount = 70000m)
    {
        var payment = new Payment
        {
            Code = code,
            BookingId = 2,
            BookingCode = "BK-0002",
            ParkingLotId = 1,
            OwnerProfileId = 2,
            UserId = 5,
            Purpose = PaymentPurpose.Booking,
            Method = PaymentMethod.Vnpay,
            Status = PaymentStatus.Pending,
            Amount = amount,
            Currency = "VND",
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        SetId(payment, 100);
        return payment;
    }

    #endregion


    #region Nhóm kiểm thử Return URL (ProcessVnpayReturnUseCase)

    [Fact]
    public async Task ExecuteAsync_InvalidSignature_ReturnsFailedResultAndLogsInvalidSig()
    {
        // Arrange (Chuẩn bị)
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = false }; // Giả lập chữ ký sai
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "PAY_20261028120000_A1B2C3",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "invalid_hash"
        };

        // Act (Thực thi)
        var result = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert (Kiểm chứng)
        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Contains("Chữ ký bảo mật không hợp lệ", result.Message);
        Assert.Single(repo.CallbackLogs);
        Assert.False(repo.CallbackLogs[0].SignatureValid);
        Assert.Empty(repo.OutboxMessages);
    }

    [Fact]
    public async Task ExecuteAsync_OrderNotFound_ReturnsFailedResult()
    {
        // Arrange
        var repo = new FakePaymentRepository(); // DB trống, không có đơn hàng
        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "PAY_NOT_FOUND",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var result = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Không tìm thấy thông tin giao dịch", result.Message);
        Assert.Empty(repo.OutboxMessages);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadySucceeded_ReturnsSuccessWithoutReProcessing()
    {
        // Arrange (Đơn hàng đã được IPN xử lý thành công trước đó)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment();
        payment.Status = PaymentStatus.Succeeded;
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var result = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Empty(repo.CallbackLogs); // Không cần ghi thêm log vì đã xử lý xong trước đó
        Assert.Empty(repo.OutboxMessages); // Không phát lại sự kiện lần 2
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPayment_UpdatesStatusToSucceededAndEmitsOutboxMessage()
    {
        // Arrange
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment();
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "15714079",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var result = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("15714079", payment.ProviderTransactionId);
        Assert.NotNull(payment.PaidAtUtc);

        Assert.Single(repo.CallbackLogs);
        Assert.True(repo.CallbackLogs[0].SignatureValid);

        Assert.Single(repo.OutboxMessages);
        Assert.Equal(nameof(PaymentSucceeded), repo.OutboxMessages[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_FailedPaymentResponseCode_UpdatesStatusToFailedAndEmitsOutboxFailed()
    {
        // Arrange (Khách hàng bấm Hủy thanh toán: ResponseCode = 24)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment();
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_ResponseCode"] = "24",
            ["vnp_TransactionNo"] = "0",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var result = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal(PaymentStatus.Failed, payment.Status);

        Assert.Single(repo.CallbackLogs);
        Assert.Single(repo.OutboxMessages);
        Assert.Equal(nameof(PaymentFailed), repo.OutboxMessages[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ValidReturn_LogsCallbackWithSourceIp()
    {
        // Arrange
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment();
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayReturnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        await useCase.ExecuteAsync(queryParams, "113.161.45.12");

        // Assert
        Assert.Single(repo.CallbackLogs);
        Assert.Equal("113.161.45.12", repo.CallbackLogs[0].SourceIp);
        Assert.Equal(payment.Id, repo.CallbackLogs[0].PaymentId);
    }

    #endregion

    #region Nhóm kiểm thử Webhook IPN (ProcessVnpayIpnUseCase)

    [Fact]
    public async Task ExecuteAsync_InvalidSignature_Returns97InvalidChecksumAndLogsFailure()
    {
        // Arrange
        var repo = new FakePaymentRepository();
        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = false }; // Chữ ký giả
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "PAY_TEST",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "fake_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("97", response.RspCode);
        Assert.Equal("Invalid Checksum", response.Message);
        Assert.Single(repo.CallbackLogs);
        Assert.False(repo.CallbackLogs[0].SignatureValid);
        Assert.Empty(repo.OutboxMessages);
    }

    [Fact]
    public async Task ExecuteAsync_OrderNotFound_Returns01OrderNotFoundAndLogsFailure()
    {
        // Arrange
        var repo = new FakePaymentRepository(); // Không có đơn hàng
        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "PAY_DOES_NOT_EXIST",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("01", response.RspCode);
        Assert.Equal("Order not found", response.Message);
        Assert.Single(repo.CallbackLogs);
        Assert.Contains("Không tìm thấy đơn hàng", repo.CallbackLogs[0].ProcessingError);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAmountMismatch_Returns04InvalidAmountAndLogsFailure()
    {
        // Arrange (Đơn hàng 70.000 VNĐ nhưng VNPAY truyền 50.000 VNĐ -> 5.000.000 xu)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "5000000", // Sai lệch số tiền
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("04", response.RspCode);
        Assert.Equal("Invalid amount", response.Message);
        Assert.Single(repo.CallbackLogs);
        Assert.Contains("Số tiền không khớp", repo.CallbackLogs[0].ProcessingError);
        Assert.Empty(repo.OutboxMessages);
        Assert.Equal(PaymentStatus.Pending, payment.Status); // Tuyệt đối không cập nhật trạng thái
    }

    [Fact]
    public async Task ExecuteAsync_NonNumericAmount_Returns04InvalidAmountAndLogsFailure()
    {
        // Arrange (vnp_Amount không phải là số)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "NOT_A_NUMBER",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("04", response.RspCode);
        Assert.Equal("Invalid amount", response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyConfirmedPayment_Returns02OrderAlreadyConfirmedAndLogsDuplicate()
    {
        // Arrange (Giao dịch đã Succeeded, VNPAY retry gửi lại IPN lần thứ 2)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        payment.Status = PaymentStatus.Succeeded;
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "7000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("02", response.RspCode);
        Assert.Equal("Order already confirmed", response.Message);
        Assert.Single(repo.CallbackLogs);
        Assert.True(repo.CallbackLogs[0].IsDuplicate); // Đánh dấu chính xác là Duplicate
        Assert.Empty(repo.OutboxMessages); // Tuyệt đối không phát sự kiện lặp lại
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPayment_Returns00ConfirmSuccessUpdatesStatusAndEmitsOutbox()
    {
        // Arrange (Giao dịch hợp lệ, số tiền 70.000 VNĐ -> 7.000.000 xu, mã 00)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "7000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "15714079",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("00", response.RspCode);
        Assert.Equal("Confirm Success", response.Message);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("15714079", payment.ProviderTransactionId);
        Assert.Equal("00", payment.ProviderResponseCode);
        Assert.NotNull(payment.PaidAtUtc);

        Assert.Single(repo.CallbackLogs);
        Assert.True(repo.CallbackLogs[0].SignatureValid);
        Assert.False(repo.CallbackLogs[0].IsDuplicate);

        Assert.Single(repo.OutboxMessages);
        Assert.Equal(nameof(PaymentSucceeded), repo.OutboxMessages[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_FailedPaymentResponseCode_Returns00ConfirmSuccessAndMarksPaymentFailed()
    {
        // Arrange (Khách hàng nhập sai OTP quá 3 lần -> ResponseCode = 09)
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "7000000",
            ["vnp_ResponseCode"] = "09",
            ["vnp_TransactionNo"] = "0",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        var response = await useCase.ExecuteAsync(queryParams, "127.0.0.1");

        // Assert
        Assert.Equal("00", response.RspCode); // VNPAY IPN vẫn nhận 00 để dừng retry
        Assert.Equal(PaymentStatus.Failed, payment.Status); // Nhưng hệ thống ghi nhận là Failed
        Assert.Equal("09", payment.ProviderResponseCode);

        Assert.Single(repo.CallbackLogs);
        Assert.Single(repo.OutboxMessages);
        Assert.Equal(nameof(PaymentFailed), repo.OutboxMessages[0].EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ValidIpn_LogsCallbackRecordWithSourceIpAndTransactionNo()
    {
        // Arrange
        var repo = new FakePaymentRepository();
        var payment = CreatePendingPayment(amount: 70000m);
        await repo.AddAsync(payment);

        var gateway = new FakeVnpayGateway { ShouldSignatureBeValid = true };
        var useCase = new ProcessVnpayIpnUseCase(repo, gateway);

        var queryParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = payment.Code,
            ["vnp_Amount"] = "7000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "99887766",
            ["vnp_SecureHash"] = "valid_hash"
        };

        // Act
        await useCase.ExecuteAsync(queryParams, "203.205.32.100");

        // Assert
        Assert.Single(repo.CallbackLogs);
        var log = repo.CallbackLogs[0];
        Assert.Equal("203.205.32.100", log.SourceIp);
        Assert.Equal("99887766", log.ProviderTransactionId);
        Assert.Equal("00", log.ResultCode);
        Assert.True(log.SignatureValid);
    }

    #endregion
}
