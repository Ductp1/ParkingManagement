using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Test;

public class ComplaintAppServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
    private const int Driver = 5;

    private static BookingSnapshot Booking(BookingStatus status = BookingStatus.Completed, DateTime? endAt = null,
        DateTime? lastChangedAt = null, int userId = Driver, int? ownerProfileId = 1, decimal paid = 50000)
        => new(1, "BK-0001", status, userId, 1, ownerProfileId, endAt ?? Now.AddDays(-1), lastChangedAt, paid);

    private static CreateComplaintRequestDto Request(ComplaintCategory category = ComplaintCategory.Overcharge,
        string description = "Bị tính thêm 1 block dù ra sớm 10 phút.", decimal? amount = 10000, IReadOnlyList<string>? evidence = null)
        => new(Driver, "BK-0001", category, description, amount, evidence);

    private static (ComplaintAppService Service, FakeRepository Repo) Create(BookingSnapshot? booking)
    {
        var repo = new FakeRepository();
        return (new ComplaintAppService(repo, new FakeBookings(booking), new FixedClock(Now)), repo);
    }

    // ===== AC1: tạo ticket có mã và trạng thái =====

    [Fact]
    public async Task Creates_ticket_with_code_status_and_first_message()
    {
        var (service, repo) = Create(Booking());

        var result = await service.CreateAsync(Request(evidence: ["https://cdn.example.com/receipt.jpg"]));

        Assert.Matches(@"^CP-20261009-\d{4}$", result.Code);
        Assert.Equal("AwaitingOwner", result.Status);
        Assert.Equal(Now.AddHours(48), result.OwnerResponseDueAtUtc);
        Assert.Equal(["https://cdn.example.com/receipt.jpg"], result.EvidenceUrls);
        var saved = Assert.Single(repo.Saved);
        Assert.Equal(1, saved.BookingId);
        Assert.Equal("Driver", Assert.Single(result.Messages).SenderParty);
    }

    [Fact]
    public async Task App_error_goes_to_platform_support_without_waiting_for_owner()
    {
        var (service, _) = Create(Booking());

        var result = await service.CreateAsync(Request(ComplaintCategory.AppError, amount: null));

        Assert.Equal("Open", result.Status);
        Assert.Null(result.OwnerResponseDueAtUtc);
    }

    [Fact]
    public async Task Unknown_owner_keeps_ticket_open_for_platform_support()
    {
        var (service, _) = Create(Booking(ownerProfileId: null));

        var result = await service.CreateAsync(Request());

        Assert.Equal("Open", result.Status);
    }

    // ===== AC2: chỉ trong 7 ngày làm việc (Now = thứ Sáu 09/10 15:00 giờ VN) =====

    [Fact]
    public async Task Seventh_working_day_is_still_allowed_even_after_9_calendar_days()
    {
        // Kết thúc thứ Tư 30/09 → ngày làm việc thứ 7 là thứ Sáu 09/10 (hôm nay).
        var (service, _) = Create(Booking(endAt: new DateTime(2026, 9, 30, 5, 0, 0, DateTimeKind.Utc)));
        Assert.NotNull(await service.CreateAsync(Request()));
    }

    [Fact]
    public async Task After_7_working_days_is_rejected()
    {
        // Kết thúc thứ Ba 29/09 → hạn hết thứ Năm 08/10.
        var (service, repo) = Create(Booking(endAt: new DateTime(2026, 9, 29, 5, 0, 0, DateTimeKind.Utc)));

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request()));
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task Window_counts_from_latest_decision_when_it_is_after_booking_end()
    {
        // Booking kết thúc 10 ngày trước nhưng quyết định hoàn tiền mới có 2 ngày trước → vẫn được khiếu nại.
        var (service, _) = Create(Booking(endAt: Now.AddDays(-10), lastChangedAt: Now.AddDays(-2)));
        Assert.NotNull(await service.CreateAsync(Request()));
    }

    // ===== AC3: phân mức ưu tiên =====

    [Theory]
    [InlineData(ComplaintCategory.LotFull, "High", "Supervisor")]
    [InlineData(ComplaintCategory.Damage, "High", "Supervisor")]
    [InlineData(ComplaintCategory.Overcharge, "Medium", "Support")]
    [InlineData(ComplaintCategory.RefundRequest, "Medium", "Support")]
    [InlineData(ComplaintCategory.StaffBehavior, "Low", "Support")]
    [InlineData(ComplaintCategory.Other, "Low", "Support")]
    public async Task Ticket_is_classified_by_category(ComplaintCategory category, string priority, string team)
    {
        var (service, _) = Create(Booking());

        var result = await service.CreateAsync(Request(category, amount: null));

        Assert.Equal(priority, result.Priority);
        Assert.Equal(team, result.AssignedTeam);
    }

    // ===== Kiểm tra booking =====

    [Fact]
    public async Task Booking_of_another_user_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(Booking(userId: 99)).Service.CreateAsync(Request()));

    [Fact]
    public async Task Missing_booking_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(null).Service.CreateAsync(Request()));

    [Fact]
    public async Task Unpaid_booking_cannot_be_complained_about()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking(BookingStatus.PendingPayment)).Service.CreateAsync(Request()));

    [Fact]
    public async Task Second_open_ticket_for_same_booking_is_rejected()
    {
        var (service, repo) = Create(Booking());
        repo.HasOpenTicket = true;

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request()));
    }

    // ===== Kiểm tra dữ liệu gửi lên =====

    [Fact]
    public async Task Too_short_description_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Request(description: "Sai tiền")));

    [Fact]
    public async Task Requested_amount_above_paid_amount_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking(paid: 50000)).Service.CreateAsync(Request(amount: 60000)));

    [Fact]
    public async Task Negative_requested_amount_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Request(amount: -1)));

    [Fact]
    public async Task More_than_5_evidence_files_is_rejected()
    {
        var urls = Enumerable.Range(1, 6).Select(i => $"https://cdn.example.com/{i}.jpg").ToList();
        await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Request(evidence: urls)));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://cdn.example.com/a.jpg")]
    public async Task Invalid_evidence_url_is_rejected(string url)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Request(evidence: [url])));

    [Fact]
    public async Task Undefined_category_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Request((ComplaintCategory)42)));

    // ===== AC4: theo dõi ticket =====

    [Fact]
    public async Task Ticket_of_another_user_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(Booking()).Service.GetForUserAsync("CP-20261009-1234", Driver));

    [Fact]
    public async Task Listing_requires_positive_user_id()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.ListForUserAsync(0));

    // ===== Fakes =====

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeBookings(BookingSnapshot? booking) : IBookingLookup
    {
        public Task<BookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
            => Task.FromResult(booking?.Code == bookingCode ? booking : null);
    }

    private sealed class FakeRepository : IComplaintRepository
    {
        public bool HasOpenTicket { get; set; }
        public List<Complaint> Saved { get; } = [];

        public Task<bool> HasOpenComplaintForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(HasOpenTicket);

        public Task AddAsync(Complaint complaint, CancellationToken cancellationToken = default)
        {
            Saved.Add(complaint);
            return Task.CompletedTask;
        }

        public Task<ComplaintDetailDto?> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ComplaintDetailDto?>(null);

        public Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ComplaintSummaryDto>>([]);
    }
}
