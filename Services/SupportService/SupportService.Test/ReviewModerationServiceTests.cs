using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;

namespace SupportService.Test;

public class ReviewModerationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
    private const int Owner = 2;          // chủ bãi TSN
    private const int OwnerLot = 2;
    private const int Admin = 1;

    private static Review NewReview(int id = 10, int lot = OwnerLot, int? owner = Owner, string? reply = null, bool flagged = false, bool hidden = false)
    {
        var review = new Review
        {
            BookingId = id, UserId = 5, ReviewerName = "Nguyễn Văn An", ParkingLotId = lot, OwnerProfileId = owner,
            Rating = 2, Comment = "Bãi chật, nhân viên chậm.", OwnerReply = reply, IsFlagged = flagged, IsHidden = hidden,
        };
        typeof(ParkingManagement.SharedKernel.Domain.BaseEntity).GetProperty(nameof(Review.Id))!.SetValue(review, id);
        return review;
    }

    private static (ReviewModerationService Service, FakeReviews Repo, FakeLots Lots) Create(params Review[] reviews)
    {
        var repo = new FakeReviews();
        repo.Items.AddRange(reviews);
        var lots = new FakeLots();
        lots.ByOwner[Owner] = [OwnerLot];
        return (new ReviewModerationService(repo, lots, new FixedClock(Now)), repo, lots);
    }

    private static OwnerReplyRequestDto Reply(string text = "Cảm ơn góp ý, bãi đã bổ sung nhân viên giờ cao điểm.") => new(text);
    private static ReviewReasonRequestDto Reason(string text = "Đánh giá sai sự thật, xe không đỗ tại bãi hôm đó.") => new(text);

    // ===== AC1: 1 phản hồi công khai =====

    [Fact]
    public async Task Owner_writes_one_public_reply_and_reviewer_is_notified()
    {
        var review = NewReview();
        var (service, repo, lots) = Create(review);

        var result = await service.ReplyAsync(Owner, 10, Reply());

        Assert.Equal("Cảm ơn góp ý, bãi đã bổ sung nhân viên giờ cao điểm.", result.OwnerReply);
        Assert.Equal(Now, review.OwnerRepliedAtUtc);
        var evt = Assert.IsType<ReviewOwnerReplied>(Assert.Single(repo.Events));
        Assert.Equal(5, evt.UserId);
        Assert.Equal(0, lots.Calls);     // OwnerProfileId khớp → không cần gọi ParkingService
    }

    [Fact]
    public async Task Second_reply_is_rejected()
        => await Assert.ThrowsAsync<ConflictException>(() =>
            Create(NewReview(reply: "Đã phản hồi trước đó.")).Service.ReplyAsync(Owner, 10, Reply()));

    [Theory]
    [InlineData("Ok")]
    [InlineData("   ")]
    public async Task Too_short_reply_is_rejected(string text)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(NewReview()).Service.ReplyAsync(Owner, 10, Reply(text)));

    [Fact]
    public async Task Too_long_reply_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(NewReview()).Service.ReplyAsync(Owner, 10, Reply(new string('a', 2001))));

    // ===== Quyền chủ bãi =====

    [Fact]
    public async Task Review_without_stored_owner_is_matched_by_lot_and_owner_is_backfilled()
    {
        var review = NewReview(owner: null);
        var (service, _, lots) = Create(review);

        await service.ReplyAsync(Owner, 10, Reply());

        Assert.Equal(1, lots.Calls);
        Assert.Equal(Owner, review.OwnerProfileId);
    }

    [Fact]
    public async Task Review_of_another_owners_lot_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(NewReview(lot: 1, owner: null)).Service.ReplyAsync(Owner, 10, Reply()));

    [Fact]
    public async Task Review_stored_for_another_owner_on_a_foreign_lot_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(NewReview(lot: 1, owner: 1)).Service.ReportAsync(Owner, 10, Reason()));

    [Fact]
    public async Task Hidden_review_cannot_be_replied_or_reported()
    {
        var (service, _, _) = Create(NewReview(hidden: true));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReplyAsync(Owner, 10, Reply()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReportAsync(Owner, 10, Reason()));
    }

    [Fact]
    public async Task Missing_review_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create().Service.ReplyAsync(Owner, 99, Reply()));

    // ===== AC2: khiếu nại → Admin; chỉ Admin gỡ =====

    [Fact]
    public async Task Owner_report_puts_review_in_admin_queue_without_hiding_it()
    {
        var review = NewReview();
        var (service, repo, _) = Create(review);

        var result = await service.ReportAsync(Owner, 10, Reason());

        Assert.True(result.IsReported);
        Assert.False(result.IsHidden);                // chủ bãi không tự gỡ được
        Assert.Equal(Now, review.FlaggedAtUtc);
        Assert.Equal(Owner, review.FlaggedByOwnerProfileId);
        Assert.Single(await service.ListReportedAsync());
        Assert.Empty(repo.Events);
    }

    [Fact]
    public async Task Reporting_twice_while_pending_is_rejected()
        => await Assert.ThrowsAsync<ConflictException>(() => Create(NewReview(flagged: true)).Service.ReportAsync(Owner, 10, Reason()));

    [Fact]
    public async Task Too_short_report_reason_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(NewReview()).Service.ReportAsync(Owner, 10, Reason("Sai")));

    [Fact]
    public async Task Admin_hide_removes_review_closes_report_and_records_audit()
    {
        var review = NewReview(flagged: true);
        var (service, repo, _) = Create(review);

        var result = await service.HideAsync(Admin, 10, Reason("Nội dung xúc phạm, vi phạm quy định."));

        Assert.True(result.IsHidden);
        Assert.False(result.IsReported);
        Assert.Equal(Admin, review.HiddenByUserId);
        Assert.Equal(Now, review.HiddenAtUtc);
        Assert.Contains(repo.Events, e => e is ReviewHidden { ReviewId: 10, Rating: 2 });
        var audit = Assert.Single(repo.Events.OfType<AuditRecorded>());
        Assert.Equal("ReviewHidden", audit.Action);
        Assert.Equal(Admin, audit.UserId);
        Assert.Equal("Nội dung xúc phạm, vi phạm quy định.", audit.Reason);
    }

    [Fact]
    public async Task Admin_can_hide_even_without_a_report_but_not_twice()
    {
        var (service, _, _) = Create(NewReview());
        await service.HideAsync(Admin, 10, Reason());
        await Assert.ThrowsAsync<ConflictException>(() => service.HideAsync(Admin, 10, Reason()));
    }

    [Fact]
    public async Task Admin_dismiss_keeps_review_visible_and_records_audit()
    {
        var review = NewReview(flagged: true);
        var (service, repo, _) = Create(review);

        var result = await service.DismissReportAsync(Admin, 10, Reason("Đánh giá đúng trải nghiệm thực tế."));

        Assert.False(result.IsReported);
        Assert.False(result.IsHidden);
        Assert.Equal("ReviewReportDismissed", Assert.IsType<AuditRecorded>(Assert.Single(repo.Events)).Action);
        Assert.Empty(await service.ListReportedAsync());
    }

    [Fact]
    public async Task Dismiss_without_pending_report_is_rejected()
        => await Assert.ThrowsAsync<ConflictException>(() => Create(NewReview()).Service.DismissReportAsync(Admin, 10, Reason()));

    // ===== Đồng thời =====

    [Fact]
    public async Task Concurrent_update_conflict_is_reported_and_nothing_is_emitted()
    {
        var (service, repo, _) = Create(NewReview());
        repo.ConflictOnNextUpdate = true;

        await Assert.ThrowsAsync<ConflictException>(() => service.ReplyAsync(Owner, 10, Reply()));
        Assert.Empty(repo.Events);
    }

    // ===== Domain =====

    [Fact]
    public void Domain_rules_guard_invalid_transitions()
    {
        Assert.Throws<InvalidOperationException>(() => NewReview(reply: "x").ReplyAsOwner("y", Now));
        Assert.Throws<InvalidOperationException>(() => NewReview(flagged: true).ReportByOwner(Owner, "r", Now));
        Assert.Throws<InvalidOperationException>(() => NewReview(hidden: true).HideByAdmin(Admin, "r", Now));
        Assert.Throws<InvalidOperationException>(() => NewReview().DismissReport());
    }

    // ===== Fakes =====

    private sealed class FakeReviews : IReviewRepository
    {
        public List<Review> Items { get; } = [];
        public List<IntegrationEvent> Events { get; } = [];
        public bool ConflictOnNextUpdate { get; set; }

        public Task<int?> FindIdByBookingAsync(int bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.BookingId == bookingId)?.Id);

        public Task AddAsync(Review review, CancellationToken cancellationToken = default)
        {
            Items.Add(review);
            return Task.CompletedTask;
        }

        public Task<Review?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task UpdateAsync(Review review, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            if (ConflictOnNextUpdate)
            {
                ConflictOnNextUpdate = false;
                throw new ConflictException("Đánh giá vừa được cập nhật bởi thao tác khác.");
            }
            Events.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModeratedReviewDto>>(Items.Where(r => r.IsFlagged && !r.IsHidden)
                .Select(ReviewModerationService.ToDto).Take(take).ToList());
    }

    private sealed class FakeLots : IOwnerLotLookup
    {
        public Dictionary<int, int[]> ByOwner { get; } = [];
        public int Calls { get; private set; }

        public Task<IReadOnlyCollection<int>> GetLotIdsAsync(int ownerProfileId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyCollection<int>>(ByOwner.GetValueOrDefault(ownerProfileId) ?? []);
        }
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
