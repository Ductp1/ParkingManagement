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
        DateTime? lastChangedAt = null, int userId = Driver, int? ownerProfileId = 1, decimal paid = 50000, DateTime? startAt = null)
    {
        var end = endAt ?? Now.AddDays(-1);
        return new(1, "BK-0001", status, userId, 1, ownerProfileId, startAt ?? end.AddHours(-2), end, lastChangedAt, paid);
    }

    private static CreateComplaintRequestDto Request(ComplaintCategory category = ComplaintCategory.Overcharge,
        string description = "Bị tính thêm 1 block dù ra sớm 10 phút.", decimal? amount = 10000, IReadOnlyList<string>? evidence = null)
        => new(Driver, "BK-0001", category, description, amount, evidence);

    private static (ComplaintAppService Service, FakeRepository Repo) Create(BookingSnapshot? booking)
    {
        var repo = new FakeRepository();
        return (new ComplaintAppService(repo, new FakeBookings(booking), new FixedClock(Now)), repo);
    }

    // ===== AC1: tạo ticket có mã và trạng thái =====

    // ===== US-089 AC3: phong tỏa tiền quyết toán khi tạo khiếu nại =====

    [Fact]
    public async Task Lot_dispute_with_requested_amount_holds_payout()
    {
        var (service, repo) = Create(Booking());
        var result = await service.CreateAsync(Request(ComplaintCategory.Overcharge, amount: 10000));
        Assert.True(result.PayoutHeld);
        Assert.True(repo.Saved[0].PayoutHeld);
    }

    [Theory]
    [InlineData(ComplaintCategory.Overcharge, null)]     // không đòi tiền
    [InlineData(ComplaintCategory.AppError, 10000)]      // lỗi app – không phải lỗi của bãi
    public async Task Payout_is_not_held_without_money_or_for_platform_issues(ComplaintCategory category, int? amount)
        => Assert.False((await Create(Booking()).Service.CreateAsync(Request(category, amount: amount))).PayoutHeld);

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

    // ===== Review: sự cố phải đã xảy ra =====

    [Fact]
    public async Task Booking_that_has_not_started_is_rejected()
    {
        var (service, repo) = Create(Booking(BookingStatus.Confirmed, startAt: Now.AddDays(1), endAt: Now.AddDays(1).AddHours(2)));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(ComplaintCategory.LotFull, amount: null)));
        Assert.Contains("chưa diễn ra", ex.Message);
        Assert.Empty(repo.Saved);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]   // đến cổng thì bãi báo hết chỗ
    [InlineData(BookingStatus.CheckedIn)]
    [InlineData(BookingStatus.Parking)]
    public async Task Ongoing_booking_that_has_started_can_be_complained_about(BookingStatus status)
    {
        var (service, _) = Create(Booking(status, startAt: Now.AddMinutes(-30), endAt: Now.AddHours(2)));
        Assert.NotNull(await service.CreateAsync(Request(ComplaintCategory.LotFull, amount: null)));
    }

    [Fact]
    public async Task Confirmed_booking_long_past_its_end_time_is_rejected()
    {
        // Không vào cổng, quá giờ kết thúc 15 ngày mà vẫn "Confirmed" → hạn tính từ giờ kết thúc nên đã quá hạn.
        var (service, _) = Create(Booking(BookingStatus.Confirmed, endAt: Now.AddDays(-15)));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(ComplaintCategory.LotFull, amount: null)));
    }

    [Fact]
    public async Task Cancelled_future_booking_counts_window_from_cancellation_time()
    {
        // Booking của tuần sau, đã hủy 2 ngày trước → còn hạn (trước đây mốc bị tính từ giờ kết thúc ở tương lai).
        var (service, _) = Create(Booking(BookingStatus.Cancelled, startAt: Now.AddDays(7), endAt: Now.AddDays(7).AddHours(2),
            lastChangedAt: Now.AddDays(-2)));
        Assert.NotNull(await service.CreateAsync(Request(ComplaintCategory.RefundRequest)));
    }

    [Fact]
    public async Task Cancelled_future_booking_after_window_is_rejected()
    {
        // Hủy từ 15 ngày trước: giờ kết thúc còn ở tương lai nhưng hạn tính từ lúc hủy nên đã quá hạn.
        var (service, _) = Create(Booking(BookingStatus.Cancelled, startAt: Now.AddDays(7), endAt: Now.AddDays(7).AddHours(2),
            lastChangedAt: Now.AddDays(-15)));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(ComplaintCategory.RefundRequest)));
    }

    [Fact]
    public async Task Early_checkout_uses_actual_completion_time()
    {
        // Ra sớm: giờ kết thúc dự kiến còn 10 phút nữa nhưng đã hoàn tất 5 phút trước → khiếu nại ngay được.
        var (service, _) = Create(Booking(BookingStatus.Completed, endAt: Now.AddMinutes(10), lastChangedAt: Now.AddMinutes(-5)));
        Assert.NotNull(await service.CreateAsync(Request()));
    }

    [Fact]
    public async Task Incident_time_in_the_future_is_rejected()
    {
        // Dữ liệu bất thường: lần đổi trạng thái cuối nằm ở tương lai.
        var (service, repo) = Create(Booking(BookingStatus.Completed, endAt: Now.AddHours(1), lastChangedAt: Now.AddHours(1)));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request()));
        Assert.Contains("tương lai", ex.Message);
        Assert.Empty(repo.Saved);
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

    private sealed class FakeRepository : InMemoryComplaintRepository;
}
