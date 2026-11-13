using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Invoices;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho chức năng Phát hành và Tra cứu Hóa đơn điện tử (US-036).
/// Tuân thủ quy chuẩn: TênHàm_TrạngThái_KếtQuả và cô lập 100% trên bộ nhớ RAM.
/// </summary>
public class InvoiceUseCaseTests
{
    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public readonly Dictionary<int, Invoice> Invoices = [];
        public readonly Dictionary<int, Payment> Payments = [];
        public readonly Dictionary<int, Settlement> Settlements = [];
        private int _invoiceIdSeq = 1;

        public Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            Invoices.TryGetValue(id, out var found);
            return Task.FromResult(found);
        }

        public Task<Invoice?> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            var found = Invoices.Values.FirstOrDefault(i => i.PaymentId == paymentId);
            return Task.FromResult(found);
        }

        public Task<Invoice?> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default)
        {
            var found = Invoices.Values.FirstOrDefault(i => i.Payment != null && i.Payment.BookingId == bookingId);
            return Task.FromResult(found);
        }

        public Task<Invoice?> GetBySettlementIdAsync(int settlementId, CancellationToken cancellationToken = default)
        {
            var found = Invoices.Values.FirstOrDefault(i => i.SettlementId == settlementId);
            return Task.FromResult(found);
        }

        public Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            var normalized = invoiceNumber.Trim().ToUpperInvariant();
            var found = Invoices.Values.FirstOrDefault(i => i.InvoiceNumber == normalized);
            return Task.FromResult(found);
        }

        public Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            Payments.TryGetValue(paymentId, out var found);
            return Task.FromResult(found);
        }

        public Task<Settlement?> GetSettlementByIdAsync(int settlementId, CancellationToken cancellationToken = default)
        {
            Settlements.TryGetValue(settlementId, out var found);
            return Task.FromResult(found);
        }

        public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            if (invoice.Id == 0)
            {
                typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(invoice, _invoiceIdSeq++);
            }
            Invoices[invoice.Id] = invoice;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            var exists = Invoices.Values.Any(i => i.PaymentId == paymentId);
            return Task.FromResult(exists);
        }

        public Task<bool> ExistsForSettlementAsync(int settlementId, CancellationToken cancellationToken = default)
        {
            var exists = Invoices.Values.Any(i => i.SettlementId == settlementId);
            return Task.FromResult(exists);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static Payment CreateSamplePayment(
        int id = 1,
        int bookingId = 101,
        decimal amount = 110_000m,
        PaymentStatus status = PaymentStatus.Succeeded)
    {
        var p = new Payment
        {
            Code = $"PAY_{id:D6}",
            BookingId = bookingId,
            BookingCode = $"BK_{bookingId}",
            ParkingLotId = 1,
            OwnerProfileId = 2,
            Amount = amount,
            Status = status,
            PaidAtUtc = FixedNow.UtcDateTime
        };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(p, id);
        return p;
    }

    private static Settlement CreateSampleSettlement(
        int id = 1,
        decimal commissionAmount = 500_000m,
        SettlementStatus status = SettlementStatus.Approved)
    {
        var s = new Settlement
        {
            OwnerProfileId = 2,
            CommissionAmount = commissionAmount,
            GrossAmount = 5_000_000m,
            CommissionRate = 0.10m,
            NetPayout = 4_500_000m,
            Status = status
        };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(s, id);
        return s;
    }

    // =========================================================================
    // NHÓM 1: KIỂM THỬ PHÁT HÀNH HÓA ĐƠN PHÍ ĐỖ XE CHO TÀI XẾ (PARKING INVOICE)
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task IssueParkingInvoice_ZeroOrNegativePaymentId_ThrowsValidationException(int invalidPaymentId)
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(invalidPaymentId, "Nguyễn Văn A");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("PaymentId phải là số nguyên dương", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task IssueParkingInvoice_EmptyBuyerName_ThrowsValidationException(string? invalidName)
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(1, invalidName!);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên người mua / đơn vị nhận hóa đơn không được để trống", ex.Message);
    }

    [Fact]
    public async Task IssueParkingInvoice_NegativeVatRate_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(1, "Nguyễn Văn A", VatRate: -0.05m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Thuế suất VAT không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task IssueParkingInvoice_ExcessiveVatRate_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(1, "Nguyễn Văn A", VatRate: 0.50m); // 50% > 30%

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Thuế suất VAT không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task IssueParkingInvoice_NonExistentPayment_ThrowsNotFoundException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(999, "Nguyễn Văn A");

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public async Task IssueParkingInvoice_PaymentNotSucceeded_ThrowsValidationException(PaymentStatus invalidStatus)
    {
        var repo = new FakeInvoiceRepository();
        var payment = CreateSamplePayment(id: 10, status: invalidStatus);
        repo.Payments[payment.Id] = payment;

        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(10, "Nguyễn Văn A");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ có thể xuất hóa đơn cho giao dịch đã thanh toán thành công", ex.Message);
    }

    [Fact]
    public async Task IssueParkingInvoice_AlreadyIssued_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var payment = CreateSamplePayment(id: 5);
        repo.Payments[payment.Id] = payment;

        // Giả lập hóa đơn đã được phát hành trước đó
        repo.Invoices[1] = new Invoice
        {
            PaymentId = 5,
            InvoiceNumber = "INV-PK-20261109-000005"
        };

        var useCase = new IssueParkingInvoiceUseCase(repo);
        var command = new IssueParkingInvoiceCommand(5, "Nguyễn Văn A");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("đã được xuất hóa đơn điện tử trước đó", ex.Message);
    }

    [Fact]
    public async Task IssueParkingInvoice_ValidCommand_IssuesInvoiceWithCorrectVatSeparation()
    {
        // Sắp xếp: Giao dịch 110.000 VNĐ đã thanh toán thành công, thuế VAT 10%
        var repo = new FakeInvoiceRepository();
        var payment = CreateSamplePayment(id: 7, bookingId: 1007, amount: 110_000m);
        repo.Payments[payment.Id] = payment;

        var useCase = new IssueParkingInvoiceUseCase(repo, new FakeTimeProvider(FixedNow));
        var command = new IssueParkingInvoiceCommand(
            7, 
            "Công ty TNHH Vận Tải ABC", 
            BuyerTaxCode: "0312345678", 
            BuyerEmail: "ketoan@vantaiabc.vn", 
            VatRate: 0.10m);

        // Hành động
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng
        Assert.NotNull(result);
        Assert.Equal(InvoiceType.ParkingFee, result.Type);
        Assert.Equal("INV-PK-20261109-000007", result.InvoiceNumber);
        Assert.Equal(7, result.PaymentId);
        Assert.Equal(1007, result.BookingId);
        Assert.Equal("Công ty TNHH Vận Tải ABC", result.BuyerName);
        Assert.Equal("0312345678", result.BuyerTaxCode);
        Assert.Equal("ketoan@vantaiabc.vn", result.BuyerEmail);

        // Kiểm tra bóc tách tài chính: SubTotal (100.000) + VatAmount (10.000) = Total (110.000)
        Assert.Equal(100_000m, result.SubTotal);
        Assert.Equal(10_000m, result.VatAmount);
        Assert.Equal(110_000m, result.Total);
        Assert.Equal("/invoices/INV-PK-20261109-000007.pdf", result.PdfUrl);
        Assert.Single(repo.Invoices);
    }

    [Fact]
    public async Task IssueParkingInvoice_ZeroVatRate_CalculatesZeroVatAmount()
    {
        var repo = new FakeInvoiceRepository();
        var payment = CreateSamplePayment(id: 8, amount: 50_000m);
        repo.Payments[payment.Id] = payment;

        var useCase = new IssueParkingInvoiceUseCase(repo, new FakeTimeProvider(FixedNow));
        var command = new IssueParkingInvoiceCommand(8, "Trần Văn B", VatRate: 0.0m);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(50_000m, result.SubTotal);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(50_000m, result.Total);
    }

    // =========================================================================
    // NHÓM 2: KIỂM THỬ PHÁT HÀNH HÓA ĐƠN HOA HỒNG SÀN (COMMISSION INVOICE)
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IssueCommissionInvoice_ZeroOrNegativeSettlementId_ThrowsValidationException(int invalidId)
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(invalidId, "Chủ bãi Vincom");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("SettlementId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task IssueCommissionInvoice_EmptyBuyerName_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(1, "   ");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên đơn vị nhận hóa đơn (Chủ bãi xe) không được để trống", ex.Message);
    }

    [Fact]
    public async Task IssueCommissionInvoice_NonExistentSettlement_ThrowsNotFoundException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(999, "Chủ bãi Vincom");

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task IssueCommissionInvoice_DraftSettlement_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var settlement = CreateSampleSettlement(id: 2, status: SettlementStatus.Draft);
        repo.Settlements[settlement.Id] = settlement;

        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(2, "Chủ bãi Vincom");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ có thể xuất hóa đơn cho kỳ quyết toán đã được duyệt hoặc đã chi trả", ex.Message);
    }

    [Fact]
    public async Task IssueCommissionInvoice_ZeroCommissionAmount_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var settlement = CreateSampleSettlement(id: 3, commissionAmount: 0m, status: SettlementStatus.Approved);
        repo.Settlements[settlement.Id] = settlement;

        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(3, "Chủ bãi Vincom");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("không phát sinh phí hoa hồng sàn", ex.Message);
    }

    [Fact]
    public async Task IssueCommissionInvoice_AlreadyIssued_ThrowsValidationException()
    {
        var repo = new FakeInvoiceRepository();
        var settlement = CreateSampleSettlement(id: 4);
        repo.Settlements[settlement.Id] = settlement;

        repo.Invoices[1] = new Invoice
        {
            SettlementId = 4,
            InvoiceNumber = "INV-COM-20261109-000004"
        };

        var useCase = new IssueCommissionInvoiceUseCase(repo);
        var command = new IssueCommissionInvoiceCommand(4, "Chủ bãi Vincom");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("đã được xuất hóa đơn hoa hồng trước đó", ex.Message);
    }

    [Fact]
    public async Task IssueCommissionInvoice_ValidCommand_IssuesInvoiceWithCorrectVat()
    {
        var repo = new FakeInvoiceRepository();
        var settlement = CreateSampleSettlement(id: 9, commissionAmount: 1_000_000m, status: SettlementStatus.Paid);
        repo.Settlements[settlement.Id] = settlement;

        var useCase = new IssueCommissionInvoiceUseCase(repo, new FakeTimeProvider(FixedNow));
        var command = new IssueCommissionInvoiceCommand(
            9, 
            "Công ty CP Quản lý Bãi đỗ xe Landmark", 
            BuyerTaxCode: "0109998887", 
            BuyerEmail: "finance@landmarkparking.vn",
            VatRate: 0.10m);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(InvoiceType.PlatformCommission, result.Type);
        Assert.Equal("INV-COM-20261109-000009", result.InvoiceNumber);
        Assert.Equal(9, result.SettlementId);
        Assert.Null(result.PaymentId);
        Assert.Equal(1_000_000m, result.SubTotal);
        Assert.Equal(100_000m, result.VatAmount);
        Assert.Equal(1_100_000m, result.Total);
        Assert.Equal("/invoices/INV-COM-20261109-000009.pdf", result.PdfUrl);
    }

    // =========================================================================
    // NHÓM 3: KIỂM THỬ TRA CỨU HÓA ĐƠN (GET INVOICE BY ID, PAYMENT, BOOKING, NUMBER)
    // =========================================================================

    [Fact]
    public async Task GetById_NonExistentId_ThrowsNotFoundException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new GetInvoiceUseCase(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetById_ValidId_ReturnsInvoiceDto()
    {
        var repo = new FakeInvoiceRepository();
        var invoice = new Invoice
        {
            Type = InvoiceType.ParkingFee,
            InvoiceNumber = "INV-PK-20261109-000001",
            BuyerName = "Nguyễn Văn C",
            Total = 50_000m,
            SubTotal = 45_454.55m,
            VatAmount = 4_545.45m
        };
        await repo.AddAsync(invoice);

        var useCase = new GetInvoiceUseCase(repo);
        var result = await useCase.GetByIdAsync(invoice.Id);

        Assert.Equal(invoice.Id, result.Id);
        Assert.Equal("INV-PK-20261109-000001", result.InvoiceNumber);
    }

    [Fact]
    public async Task GetByPaymentId_NonExistent_ThrowsNotFoundException()
    {
        var repo = new FakeInvoiceRepository();
        var useCase = new GetInvoiceUseCase(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetByPaymentIdAsync(888));
    }

    [Fact]
    public async Task GetByPaymentId_ValidPaymentId_ReturnsInvoiceDto()
    {
        var repo = new FakeInvoiceRepository();
        var invoice = new Invoice
        {
            PaymentId = 15,
            InvoiceNumber = "INV-PK-20261109-000015",
            BuyerName = "Lê Thị D"
        };
        await repo.AddAsync(invoice);

        var useCase = new GetInvoiceUseCase(repo);
        var result = await useCase.GetByPaymentIdAsync(15);

        Assert.Equal(15, result.PaymentId);
        Assert.Equal("Lê Thị D", result.BuyerName);
    }

    [Fact]
    public async Task GetByBookingId_ValidBookingId_ReturnsInvoiceDto()
    {
        var repo = new FakeInvoiceRepository();
        var payment = CreateSamplePayment(id: 20, bookingId: 555);
        var invoice = new Invoice
        {
            PaymentId = 20,
            Payment = payment,
            InvoiceNumber = "INV-PK-20261109-000020",
            BuyerName = "Phạm Văn E"
        };
        await repo.AddAsync(invoice);

        var useCase = new GetInvoiceUseCase(repo);
        var result = await useCase.GetByBookingIdAsync(555);

        Assert.Equal(20, result.PaymentId);
        Assert.Equal(555, result.BookingId);
        Assert.Equal("Phạm Văn E", result.BuyerName);
    }

    [Fact]
    public async Task GetByInvoiceNumber_ValidNumber_ReturnsInvoiceDto()
    {
        var repo = new FakeInvoiceRepository();
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-PK-20261109-000099",
            BuyerName = "Vũ Văn F"
        };
        await repo.AddAsync(invoice);

        var useCase = new GetInvoiceUseCase(repo);
        var result = await useCase.GetByInvoiceNumberAsync("inv-pk-20261109-000099"); // Test case-insensitive

        Assert.Equal("INV-PK-20261109-000099", result.InvoiceNumber);
        Assert.Equal("Vũ Văn F", result.BuyerName);
    }

    [Fact]
    public async Task GetBySettlementId_ValidSettlementId_ReturnsInvoiceDto()
    {
        var repo = new FakeInvoiceRepository();
        var invoice = new Invoice
        {
            Type = InvoiceType.PlatformCommission,
            SettlementId = 33,
            InvoiceNumber = "INV-COM-20261109-000033",
            BuyerName = "Chủ bãi Vincom"
        };
        await repo.AddAsync(invoice);

        var useCase = new GetInvoiceUseCase(repo);
        var result = await useCase.GetBySettlementIdAsync(33);

        Assert.Equal(33, result.SettlementId);
        Assert.Equal(InvoiceType.PlatformCommission, result.Type);
    }
}

