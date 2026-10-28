using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Payments;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho ca sử dụng tạo mã thanh toán VietQR động chuẩn NAPAS247 (Task T-402).
/// Quy tắc đặt tên phương thức kiểm thử: TenHam_TrangThai_KetQua.
/// </summary>
public class CreateVietQrPaymentUseCaseTests
{
    /// <summary>
    /// Giả lập kho lưu trữ thanh toán trong bộ nhớ RAM phục vụ Unit Test cô lập.
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

        public Task SaveCallbackResultAsync(
            Payment? payment, 
            PaymentCallbackLog callbackLog, 
            ParkingManagement.SharedKernel.Domain.OutboxMessage? outboxMessage, 
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_DuLieuHopLeChoBooking_TaoThanhToanVietQrThanhCong()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("PAY_", result.PaymentCode);
        Assert.Equal("970422", result.BankBin);
        Assert.Equal("0123456789", result.BankAccountNumber);
        Assert.Equal("NGUYEN VAN CHU BAI", result.BankAccountName);
        Assert.Equal(50000, result.Amount);
        Assert.Equal("BK-0010", result.TransferContent);
        Assert.Contains("img.vietqr.io/image/970422-0123456789-compact2.png", result.QrImageUrl);
        Assert.Contains("amount=50000", result.QrImageUrl);
        Assert.Contains("addInfo=BK-0010", result.QrImageUrl);

        // Kiểm tra dữ liệu được lưu xuống cơ sở dữ liệu
        Assert.Single(fakeRepo.SavedPayments);
        var saved = fakeRepo.SavedPayments[0];
        Assert.Equal(result.PaymentCode, saved.Code);
        Assert.Equal(PaymentMethod.VietQr, saved.Method);
        Assert.Equal(PaymentStatus.Pending, saved.Status);
        Assert.Equal(PaymentPurpose.Booking, saved.Purpose);
        Assert.Equal("BK-0010", saved.TransferContent);
        Assert.Equal(50000, saved.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_DuLieuHopLeChoKhachVangLaiCuaParkingSession_TaoThanhToanWalkInThanhCong()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: null,
            BookingCode: null,
            ParkingSessionId: 88,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: null,
            Amount: 30000,
            BankBin: "970436",
            BankAccountNumber: "9876543210",
            BankAccountName: "LE THI CHU BAI");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SES_88", result.TransferContent);
        Assert.Contains("addInfo=SES_88", result.QrImageUrl);

        // Kiểm tra lưu đúng loại mục đích vãng lai
        Assert.Single(fakeRepo.SavedPayments);
        var saved = fakeRepo.SavedPayments[0];
        Assert.Equal(PaymentPurpose.WalkIn, saved.Purpose);
        Assert.Equal(88, saved.ParkingSessionId);
        Assert.Null(saved.BookingId);
        Assert.Null(saved.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_SoTienBangKhong_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 0,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Số tiền thanh toán phải lớn hơn 0 đồng.", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_SoTienAm_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: -20000,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Số tiền thanh toán phải lớn hơn 0 đồng.", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ThieuBankBin_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000,
            BankBin: "   ",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("BankBin", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ThieuSoTaiKhoan_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000,
            BankBin: "970422",
            BankAccountNumber: "",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Số tài khoản", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ThieuTenChuTaiKhoan_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "\t");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên chủ tài khoản", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ThieuCaBookingIdVaParkingSessionId_QuangValidationException()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: null,
            BookingCode: null,
            ParkingSessionId: 0,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 50000,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("BookingId hoặc ParkingSessionId", ex.Message);
        Assert.Empty(fakeRepo.SavedPayments);
    }

    [Fact]
    public async Task ExecuteAsync_ThoiHanHetHanSau15Phut_DatExpiresAtUtcChuanXac()
    {
        // Arrange
        var fakeRepo = new FakePaymentRepository();
        var useCase = new CreateVietQrPaymentUseCase(fakeRepo);
        var command = new CreateVietQrPaymentCommand(
            BookingId: 10,
            BookingCode: "BK-0010",
            ParkingSessionId: null,
            ParkingLotId: 1,
            OwnerProfileId: 2,
            UserId: 5,
            Amount: 70000,
            BankBin: "970422",
            BankAccountNumber: "0123456789",
            BankAccountName: "NGUYEN VAN CHU BAI");

        var beforeCall = DateTime.UtcNow.AddMinutes(15);

        // Act
        var result = await useCase.ExecuteAsync(command);
        var afterCall = DateTime.UtcNow.AddMinutes(15);

        // Assert
        Assert.True(result.ExpiresAtUtc >= beforeCall.AddSeconds(-2));
        Assert.True(result.ExpiresAtUtc <= afterCall.AddSeconds(2));
    }

    [Fact]
    public void GenerateQuickLink_ChuanHoaSoTaiKhoanVaTen_TaoUrlChuanKhongLoiKyTu()
    {
        // Arrange
        var bin = " 970422 ";
        var accountNo = " 0123456789 ";
        var amount = 100000.4m;
        var content = "BK 0002 / DO XE";
        var name = " nguyen van a ";

        // Act
        var url = VietQrGenerator.GenerateQuickLink(bin, accountNo, amount, content, name);

        // Assert
        Assert.Equal("https://img.vietqr.io/image/970422-0123456789-compact2.png?amount=100000&addInfo=BK%200002%20%2F%20DO%20XE&accountName=NGUYEN%20VAN%20A", url);
    }
}
