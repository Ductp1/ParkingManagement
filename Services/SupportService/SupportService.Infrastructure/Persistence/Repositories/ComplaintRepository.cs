using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Repositories;

public sealed class ComplaintRepository(SupportDbContext db) : IComplaintRepository
{
    public Task<bool> HasOpenComplaintForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
        => db.Complaints.AnyAsync(c => c.BookingId == bookingId
            && c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Rejected, cancellationToken);

    public async Task AddAsync(Complaint complaint, CancellationToken cancellationToken = default)
    {
        // Transactional Outbox: ticket và sự kiện ComplaintCreated cùng thành công hoặc cùng thất bại.
        // Cần 2 lần SaveChanges vì Id (IDENTITY) chỉ có sau khi INSERT ticket.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync(cancellationToken);

        var evt = new ComplaintCreated(complaint.Id, complaint.Code, complaint.ParkingLotId, complaint.OwnerProfileId);
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(ComplaintCreated),
            PayloadJson = JsonSerializer.Serialize(evt),
            OccurredAtUtc = evt.OccurredAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<ComplaintDetailDto?> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default)
        => GetDetailAsync(x => x.Code == code && x.UserId == userId, cancellationToken);

    public Task<ComplaintDetailDto?> GetForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default)
        => GetDetailAsync(x => x.Code == code && x.OwnerProfileId == ownerProfileId, cancellationToken);

    /// <summary>Chi tiết ticket dùng chung cho tài xế và chủ bãi – điều kiện quyền xem nằm trong <paramref name="filter"/>.</summary>
    private async Task<ComplaintDetailDto?> GetDetailAsync(Expression<Func<Complaint, bool>> filter, CancellationToken cancellationToken)
    {
        var c = await db.Complaints.AsNoTracking()
            .Where(filter)
            .Select(x => new
            {
                x.Id, x.Code, x.Status, x.Category, x.Priority, x.AssignedTeam, x.BookingCode, x.ParkingLotId, x.Description,
                x.EvidenceUrlsJson, x.RequestedAmount, x.OwnerResponseDueAtUtc, x.OwnerResponse, x.Resolution,
                x.ResolvedRefundAmount, x.CreatedAtUtc, x.UpdatedAtUtc, x.OwnerRespondedAtUtc, x.EscalatedAtUtc, x.PayoutHeld,
                // Ghi chú nội bộ của Admin không trả cho tài xế / chủ bãi.
                Messages = x.Messages.Where(m => !m.IsInternal).OrderBy(m => m.CreatedAtUtc)
                    .Select(m => new { m.Id, m.SenderParty, m.Message, m.AttachmentUrlsJson, m.CreatedAtUtc }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (c is null) return null;

        // Enum.ToString() và đọc JSON làm sau khi lấy dữ liệu (EF không dịch được sang SQL).
        return new ComplaintDetailDto(
            c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.AssignedTeam.ToString(),
            c.BookingCode, c.ParkingLotId, c.Description, ComplaintAppService.ReadUrls(c.EvidenceUrlsJson), c.RequestedAmount,
            c.OwnerResponseDueAtUtc, c.OwnerResponse, c.Resolution, c.ResolvedRefundAmount, c.CreatedAtUtc, c.UpdatedAtUtc,
            c.Messages.Select(m => new ComplaintMessageDto(m.Id, m.SenderParty.ToString(), m.Message,
                ComplaintAppService.ReadUrls(m.AttachmentUrlsJson), m.CreatedAtUtc)).ToList(),
            c.OwnerRespondedAtUtc, c.EscalatedAtUtc, c.PayoutHeld);
    }

    public Task<Complaint?> FindForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default)
        => db.Complaints.FirstOrDefaultAsync(c => c.Code == code && c.OwnerProfileId == ownerProfileId, cancellationToken);

    public async Task<IReadOnlyList<int>> ListOverdueAwaitingOwnerIdsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
        // Dùng index (Status, OwnerResponseDueAtUtc) đã có cho job escalate.
        => await db.Complaints.AsNoTracking()
            .Where(c => c.Status == ComplaintStatus.AwaitingOwner && c.OwnerResponseDueAtUtc != null && c.OwnerResponseDueAtUtc < nowUtc)
            .OrderBy(c => c.OwnerResponseDueAtUtc)
            .Take(take)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<Complaint?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Complaints.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task UpdateAsync(Complaint complaint, IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        // Transactional Outbox: thay đổi của ticket (kèm tin nhắn mới) và sự kiện cùng thành công hoặc cùng thất bại.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = integrationEvent.GetType().Name,
            PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
        });
        try
        {
            // UPDATE "Complaints" ... WHERE "Id" = @id AND "RowVersion" = @original → 0 dòng nếu bên khác đã cập nhật trước.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Transaction chưa commit sẽ rollback khi dispose: không ghi ticket, tin nhắn hay sự kiện.
            // Bỏ các thay đổi đang treo để lần lưu sau trong cùng DbContext không gửi lại chúng.
            db.ChangeTracker.Clear();
            throw new ConflictException($"Tranh chấp {complaint.Code} vừa được cập nhật bởi thao tác khác (ví dụ đã chuyển nền tảng xử lý). Vui lòng tải lại.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayoutHoldDto>> ListPayoutHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken = default)
    {
        var query = db.Complaints.AsNoTracking()
            .Where(c => c.PayoutHeld && c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Rejected);
        if (ownerProfileId is { } owner) query = query.Where(c => c.OwnerProfileId == owner);

        var rows = await query
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new { c.Code, c.Status, c.ParkingLotId, c.OwnerProfileId, c.BookingCode, c.RequestedAmount, c.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(c => new PayoutHoldDto(c.Code, c.Status.ToString(), c.ParkingLotId, c.OwnerProfileId, c.BookingCode,
            c.RequestedAmount ?? 0, c.CreatedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.Complaints.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new { c.Id, c.Code, c.Status, c.Category, c.Priority, c.BookingCode, c.CreatedAtUtc, c.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(c => new ComplaintSummaryDto(c.Id, c.Code, c.Status.ToString(), c.Category.ToString(),
            c.Priority.ToString(), c.BookingCode, c.CreatedAtUtc, c.UpdatedAtUtc)).ToList();
    }
}
