using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
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

    public Task<Review?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Reviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task UpdateAsync(Review review, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        // Transactional Outbox: thay đổi của đánh giá và các sự kiện (thông báo, xếp hạng, audit) cùng thành công hoặc cùng thất bại.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var evt in events)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = evt.GetType().Name,
                PayloadJson = JsonSerializer.Serialize(evt, evt.GetType()),
                OccurredAtUtc = evt.OccurredAtUtc,
            });
        }
        try
        {
            // UPDATE "Reviews" ... WHERE "Id" = @id AND "RowVersion" = @original → 0 dòng nếu bên khác đã cập nhật trước.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException($"Đánh giá {review.Id} vừa được cập nhật bởi thao tác khác. Vui lòng tải lại.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModeratedReviewDto>> ListReportedAsync(int take, CancellationToken cancellationToken = default)
        // Dùng index lọc IX_Reviews_Flagged_Queue (chỉ chứa đánh giá đang bị khiếu nại).
        => await db.Reviews.AsNoTracking()
            .Where(r => r.IsFlagged && !r.IsHidden)
            .OrderBy(r => r.FlaggedAtUtc)
            .Take(take)
            .Select(r => new ModeratedReviewDto(r.Id, r.ParkingLotId, r.ReviewerName, r.Rating, r.Comment, r.OwnerReply,
                r.OwnerRepliedAtUtc, r.IsFlagged, r.FlagReason, r.FlaggedAtUtc, r.IsHidden, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
