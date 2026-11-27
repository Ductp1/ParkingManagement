using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Test;

/// <summary>US-091: khách đến nơi nhưng bãi hết chỗ.</summary>
public class LotFullIncidentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 2, 0, 0, DateTimeKind.Utc);
    private const int Driver = 5;

    private static BookingSnapshot Booking(BookingStatus status = BookingStatus.Confirmed, int userId = Driver, int startInMinutes = 10)
        => new(7, "BK-0007", status, userId, 1, 1, Now.AddMinutes(startInMinutes), Now.AddMinutes(startInMinutes + 120), Now.AddDays(-1), 63000);

    private static readonly AlternativeLotDto BenThanh = new(4, "Bãi xe Bến Thành", "Lê Lợi, Q.1", 0.6, 12, true);

    private static (LotFullIncidentService Service, FakeIncidents Incidents, FakeBookings Bookings, FakeLots Lots) Create(
        BookingSnapshot? booking = null, params AlternativeLotDto[] nearby)
    {
        var incidents = new FakeIncidents();
        var bookings = new FakeBookings(booking ?? Booking());
        var lots = new FakeLots(nearby.Length > 0 ? nearby : [BenThanh]);
        return (new LotFullIncidentService(incidents, bookings, lots, new FixedClock(Now)), incidents, bookings, lots);
    }

    private static ReportLotFullRequestDto Report(string code = "BK-0007", string? note = "Bảo vệ báo bãi đã hết chỗ.") => new(code, note);

    // ===== AC1: ghi nhận sự cố ưu tiên cao =====

    [Fact]
    public async Task Report_records_high_priority_incident_with_evidence_and_notifies()
    {
        var (service, incidents, _, _) = Create();

        var result = await service.ReportAsync(Driver, Report(" bk-0007 "));

        Assert.StartsWith("LF-", result.Code);
        Assert.Equal(("Reported", "High"), (result.Status, result.Priority));
        Assert.Equal(63000, result.PaidAmount);
        Assert.Equal(3, result.LotAvailableSlotsAtReport);   // bằng chứng: lúc báo, bãi vẫn khai còn 3 chỗ
        var evt = Assert.IsType<LotFullIncidentReported>(Assert.Single(incidents.Events));
        Assert.Equal((result.Code, "BK-0007", Driver, 1, (int?)1, 1),
            (evt.IncidentCode, evt.BookingCode, evt.UserId, evt.ParkingLotId, evt.OwnerProfileId, evt.AlternativeCount));
    }

    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.CheckedIn)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task Only_confirmed_booking_can_report(BookingStatus status)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking(status)).Service.ReportAsync(Driver, Report()));

    [Theory]
    [InlineData(31)]     // còn hơn 30 phút mới tới giờ → chưa "đến nơi"
    [InlineData(-121)]   // khung giờ đã kết thúc
    public async Task Report_outside_arrival_window_is_rejected(int startInMinutes)
        => await Assert.ThrowsAsync<ValidationException>(() =>
            Create(Booking(startInMinutes: startInMinutes)).Service.ReportAsync(Driver, Report()));

    [Theory]
    [InlineData(30)]     // đúng 30 phút trước giờ bắt đầu
    [InlineData(-120)]   // đúng lúc kết thúc khung giờ
    public async Task Report_on_window_boundaries_is_allowed(int startInMinutes)
        => Assert.Equal("Reported", (await Create(Booking(startInMinutes: startInMinutes)).Service.ReportAsync(Driver, Report())).Status);

    [Fact]
    public async Task Booking_of_another_driver_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(Booking(userId: 99)).Service.ReportAsync(Driver, Report()));

    [Fact]
    public async Task Second_report_for_same_booking_is_rejected()
    {
        var (service, _, _, _) = Create();
        await service.ReportAsync(Driver, Report());
        await Assert.ThrowsAsync<ConflictException>(() => service.ReportAsync(Driver, Report()));
    }

    [Fact]
    public async Task Too_long_note_or_missing_booking_code_is_rejected()
    {
        var (service, _, _, _) = Create();
        await Assert.ThrowsAsync<ValidationException>(() => service.ReportAsync(Driver, Report(note: new string('a', 501))));
        await Assert.ThrowsAsync<ValidationException>(() => service.ReportAsync(Driver, Report(code: " ")));
    }

    // ===== AC2: gợi ý bãi thay thế trong 1 km, chuyển miễn phí nếu đồng ý =====

    [Fact]
    public async Task Alternatives_are_within_1km_open_available_other_lots_nearest_first_max_5()
    {
        AlternativeLotDto Lot(int id, double km, int slots = 5, bool open = true) => new(id, $"Bãi {id}", "Q.1", km, slots, open);
        var (service, _, _, lots) = Create(null,
            Lot(1, 0, 10),                              // chính bãi đang hết chỗ
            Lot(10, 0.9), Lot(11, 0.2), Lot(12, 1.4),   // 1,4 km: ngoài bán kính
            Lot(13, 0.3, slots: 0),                     // hết chỗ
            Lot(14, 0.4, open: false),                  // đang đóng cửa
            Lot(16, 0.6), Lot(17, 0.7), Lot(18, 0.8), Lot(19, 0.95));

        var result = await service.ReportAsync(Driver, Report());

        Assert.Equal([11, 16, 17, 18, 10], result.Alternatives.Select(a => a.ParkingLotId));
        Assert.Equal(LotFullRules.AlternativeRadiusKm, lots.LastRadius);
    }

    [Fact]
    public async Task No_alternative_nearby_still_records_incident()
    {
        var (service, _, _, _) = Create(null, new AlternativeLotDto(12, "Xa", "Q.3", 2.5, 5, true));
        var result = await service.ReportAsync(Driver, Report());
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public async Task Driver_accepts_transfer_and_booking_service_is_asked_to_move_booking()
    {
        var (service, incidents, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());

        var result = await service.AcceptTransferAsync(Driver, incident.Code, new AcceptTransferRequestDto(4));

        Assert.Equal("TransferAccepted", result.Status);
        Assert.Equal((4, "Bãi xe Bến Thành"), (result.NewParkingLotId!.Value, result.NewParkingLotName!));
        Assert.False(result.IsSlaViolation);
        var evt = Assert.IsType<LotFullTransferAccepted>(incidents.Events.Last());
        Assert.Equal((7, "BK-0007", Driver, 1, 4), (evt.BookingId, evt.BookingCode, evt.UserId, evt.OldParkingLotId, evt.NewParkingLotId));
        Assert.DoesNotContain(incidents.Events, e => e is LotFullRefundRequested or LotSlaViolated);
    }

    [Fact]
    public async Task Lot_not_in_suggestions_cannot_be_chosen()
    {
        var (service, incidents, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());
        await Assert.ThrowsAsync<ValidationException>(() => service.AcceptTransferAsync(Driver, incident.Code, new AcceptTransferRequestDto(99)));
        Assert.Single(incidents.Events);
    }

    // ===== AC3: không đồng ý → hoàn 100% + voucher, bãi vi phạm SLA =====

    [Fact]
    public async Task Driver_declines_requests_full_refund_with_voucher_and_records_sla_violation()
    {
        var (service, incidents, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());

        var result = await service.DeclineAsync(Driver, incident.Code);

        Assert.Equal("RefundRequested", result.Status);
        Assert.Equal((100, 20_000m, 90), (result.RefundPercent!.Value, result.VoucherAmount!.Value, result.VoucherValidDays!.Value));
        Assert.True(result.IsSlaViolation);
        var refund = Assert.Single(incidents.Events.OfType<LotFullRefundRequested>());
        Assert.Equal((7, "BK-0007", Driver, (int?)1, 100, 20_000m),
            (refund.BookingId, refund.BookingCode, refund.UserId, refund.OwnerProfileId, refund.RefundPercent, refund.VoucherAmount));
        var sla = Assert.Single(incidents.Events.OfType<LotSlaViolated>());
        Assert.Equal((1, (int?)1), (sla.ParkingLotId, sla.OwnerProfileId));
    }

    [Fact]
    public async Task Booking_no_longer_confirmed_cannot_be_transferred_or_refunded()
    {
        var (service, _, bookings, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());
        bookings.Current = Booking(BookingStatus.Cancelled);   // tài xế đã tự hủy trong lúc chờ

        await Assert.ThrowsAsync<ValidationException>(() => service.AcceptTransferAsync(Driver, incident.Code, new AcceptTransferRequestDto(4)));
        await Assert.ThrowsAsync<ValidationException>(() => service.DeclineAsync(Driver, incident.Code));
    }

    [Fact]
    public async Task Incident_can_be_decided_only_once()
    {
        var (service, _, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());
        await service.AcceptTransferAsync(Driver, incident.Code, new AcceptTransferRequestDto(4));

        await Assert.ThrowsAsync<ConflictException>(() => service.DeclineAsync(Driver, incident.Code));
        await Assert.ThrowsAsync<ConflictException>(() => service.AcceptTransferAsync(Driver, incident.Code, new AcceptTransferRequestDto(4)));
    }

    [Fact]
    public async Task Other_driver_cannot_see_or_decide_incident()
    {
        var (service, _, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(6, incident.Code));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeclineAsync(6, incident.Code));
    }

    // ===== Admin =====

    [Fact]
    public async Task Admin_lists_sla_violations_and_validates_status_filter()
    {
        var (service, _, _, _) = Create();
        var incident = await service.ReportAsync(Driver, Report());
        await service.DeclineAsync(Driver, incident.Code);

        Assert.Single(await service.ListForAdminAsync(1, "refundrequested", slaViolationOnly: true));
        Assert.Empty(await service.ListForAdminAsync(null, "TransferAccepted", false));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListForAdminAsync(null, "Closed", false));
    }

    // ===== Domain =====

    [Fact]
    public void Domain_blocks_second_decision()
    {
        var incident = new LotFullIncident { Code = "LF-1" };
        incident.AcceptTransfer(4, "Bến Thành", Now);
        Assert.Throws<InvalidOperationException>(() => incident.RequestRefund(100, 20_000, 90, Now));
    }

    // ===== Fakes =====

    private sealed class FakeIncidents : ILotFullIncidentRepository
    {
        private readonly List<LotFullIncident> _items = [];
        public List<IntegrationEvent> Events { get; } = [];

        public Task<bool> ExistsForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(i => i.BookingId == bookingId));

        public Task<LotFullIncident?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(i => i.Code == code));

        public Task AddAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            _items.Add(incident);
            Events.AddRange(events);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            Events.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LotFullIncident>> ListAsync(int? parkingLotId, LotFullIncidentStatus? status, bool slaViolationOnly,
            int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LotFullIncident>>(_items
                .Where(i => (parkingLotId is null || i.ParkingLotId == parkingLotId) && (status is null || i.Status == status)
                    && (!slaViolationOnly || i.IsSlaViolation))
                .Take(take).ToList());
    }

    private sealed class FakeBookings(BookingSnapshot booking) : IBookingLookup
    {
        public BookingSnapshot Current { get; set; } = booking;
        public Task<BookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Current.Code == bookingCode ? Current : null);
    }

    private sealed class FakeLots(AlternativeLotDto[] nearby) : IParkingLotSearch
    {
        public double LastRadius { get; private set; }
        public Task<LotLocation?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<LotLocation?>(new LotLocation(parkingLotId, "Bãi xe Vincom Đồng Khởi", 10.7781, 106.7019, 3));

        public Task<IReadOnlyList<AlternativeLotDto>> SearchAvailableAsync(double latitude, double longitude, double radiusKm,
            CancellationToken cancellationToken = default)
        {
            LastRadius = radiusKm;
            return Task.FromResult<IReadOnlyList<AlternativeLotDto>>(nearby);
        }
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
