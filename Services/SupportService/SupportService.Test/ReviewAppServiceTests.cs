using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;

namespace SupportService.Test;

public class ReviewAppServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
    private const int Driver = 5;

    private static BookingSnapshot Booking(BookingStatus status = BookingStatus.Completed, int userId = Driver, int? ownerProfileId = 1)
        => new(7, "BK-0007", status, userId, 3, ownerProfileId, Now.AddHours(-3), Now.AddHours(-1), Now.AddHours(-1), 50000);

    private static CreateReviewRequestDto Request(int rating = 4, string? comment = "Bãi rộng, dễ tìm chỗ.", string? reviewerName = "Nguyễn Văn An")
        => new("BK-0007", rating, comment, reviewerName);

    private static (ReviewAppService Service, FakeRepository Repo) Create(BookingSnapshot? booking, int? existingReviewId = null)
    {
        var repo = new FakeRepository { ExistingReviewId = existingReviewId };
        return (new ReviewAppService(repo, new FakeBookings(booking)), repo);
    }

    // ===== AC1: booking Completed → đánh giá 1–5 sao + nhận xét =====

    [Fact]
    public async Task Completed_booking_can_be_reviewed()
    {
        var (service, repo) = Create(Booking());

        var result = await service.CreateAsync(Driver, Request());

        var saved = Assert.Single(repo.Saved);
        Assert.Equal(7, saved.BookingId);
        Assert.Equal(3, saved.ParkingLotId);          // lấy từ booking, không tin client
        Assert.Equal(1, saved.OwnerProfileId);
        Assert.Equal((byte)4, saved.Rating);
        Assert.Equal("BK-0007", result.BookingCode);
        Assert.Equal("Nguyễn Văn An", result.ReviewerName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Boundary_ratings_are_accepted(int rating)
        => Assert.Equal(rating, (await Create(Booking()).Service.CreateAsync(Driver, Request(rating))).Rating);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Rating_outside_1_to_5_is_rejected(int rating)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Driver, Request(rating)));

    [Fact]
    public async Task Comment_is_optional_and_trimmed()
    {
        var (service, repo) = Create(Booking());

        await service.CreateAsync(Driver, Request(comment: "   "));
        Assert.Null(repo.Saved[0].Comment);
    }

    [Fact]
    public async Task Too_long_comment_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(Driver, Request(comment: new string('a', 2001))));

    [Fact]
    public async Task Missing_reviewer_name_falls_back_to_anonymous()
        => Assert.Equal("Tài xế ẩn danh", (await Create(Booking()).Service.CreateAsync(Driver, Request(reviewerName: null))).ReviewerName);

    [Fact]
    public async Task Unknown_owner_is_stored_as_null()
    {
        var (service, repo) = Create(Booking(ownerProfileId: null));
        await service.CreateAsync(Driver, Request());
        Assert.Null(repo.Saved[0].OwnerProfileId);
    }

    // ===== AC1: mỗi booking 1 lần =====

    [Fact]
    public async Task Second_review_for_same_booking_is_rejected()
    {
        var (service, repo) = Create(Booking(), existingReviewId: 11);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Driver, Request()));
        Assert.Empty(repo.Saved);
    }

    // ===== AC2: chỉ booking Completed =====

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Parking)]
    [InlineData(BookingStatus.CheckedOut)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.NoShow)]
    public async Task Booking_that_is_not_completed_cannot_be_reviewed(BookingStatus status)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking(status)).Service.CreateAsync(Driver, Request()));

    [Fact]
    public async Task Booking_of_another_user_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(Booking(userId: 99)).Service.CreateAsync(Driver, Request()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Missing_user_from_token_is_rejected(int userId)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CreateAsync(userId, Request()));

    [Fact]
    public async Task Missing_booking_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(null).Service.CreateAsync(Driver, Request()));

    // ===== AC2: kiểm tra có hiện nút đánh giá =====

    [Fact]
    public async Task Eligible_when_completed_and_not_reviewed()
    {
        var result = await Create(Booking()).Service.CheckEligibilityAsync(Driver, "BK-0007");
        Assert.True(result.CanReview);
        Assert.Null(result.ReviewId);
    }

    [Fact]
    public async Task Not_eligible_when_already_reviewed()
    {
        var result = await Create(Booking(), existingReviewId: 11).Service.CheckEligibilityAsync(Driver, "BK-0007");
        Assert.False(result.CanReview);
        Assert.Equal(11, result.ReviewId);
    }

    [Fact]
    public async Task Not_eligible_when_booking_not_completed()
    {
        var result = await Create(Booking(BookingStatus.Confirmed)).Service.CheckEligibilityAsync(Driver, "BK-0007");
        Assert.False(result.CanReview);
        Assert.Equal("Booking chưa hoàn tất.", result.Reason);
    }

    [Fact]
    public async Task Eligibility_of_another_users_booking_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Create(Booking(userId: 99)).Service.CheckEligibilityAsync(Driver, "BK-0007"));

    [Theory]
    [InlineData(0, "BK-0007")]
    [InlineData(5, " ")]
    public async Task Eligibility_requires_valid_input(int userId, string bookingCode)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Booking()).Service.CheckEligibilityAsync(userId, bookingCode));

    // ===== Fakes =====

    private sealed class FakeBookings(BookingSnapshot? booking) : IBookingLookup
    {
        public Task<BookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
            => Task.FromResult(booking?.Code == bookingCode ? booking : null);
    }

    private sealed class FakeRepository : IReviewRepository
    {
        public int? ExistingReviewId { get; init; }
        public List<Review> Saved { get; } = [];

        public Task<int?> FindIdByBookingAsync(int bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingReviewId);

        public Task AddAsync(Review review, CancellationToken cancellationToken = default)
        {
            Saved.Add(review);
            return Task.CompletedTask;
        }

        // US-094 – không dùng trong test tạo đánh giá.
        public Task<Review?> FindByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<Review?>(null);

        public Task UpdateAsync(Review review, IReadOnlyCollection<ParkingManagement.SharedKernel.Contracts.IntegrationEvent> events,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModeratedReviewDto>>([]);
    }
}
