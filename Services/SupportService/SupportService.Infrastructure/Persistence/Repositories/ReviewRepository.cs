using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(SupportDbContext db) : IReviewRepository
{
    public async Task<int?> FindIdByBookingAsync(int bookingId, CancellationToken cancellationToken = default)
        // IgnoreQueryFilters: unique index trên BookingId tính cả đánh giá đã xóa mềm, nên kiểm tra cũng phải tính.
        => await db.Reviews.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.BookingId == bookingId)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default)
    {
        // Transactional Outbox: đánh giá và sự kiện ReviewCreated cùng thành công hoặc cùng thất bại.
        // ParkingService nghe sự kiện này để cập nhật điểm phục vụ xếp hạng tìm kiếm (US-093 AC3).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);

        var evt = new ReviewCreated(review.Id, review.ParkingLotId, review.Rating);
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(ReviewCreated),
            PayloadJson = JsonSerializer.Serialize(evt),
            OccurredAtUtc = evt.OccurredAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
