using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
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

        var evt = new ComplaintCreated(complaint.Id, complaint.Code, complaint.ParkingLotId, complaint.OwnerProfileId ?? 0);
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(ComplaintCreated),
            PayloadJson = JsonSerializer.Serialize(evt),
            OccurredAtUtc = evt.OccurredAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ComplaintDetailDto?> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default)
    {
        var c = await db.Complaints.AsNoTracking()
            .Where(x => x.Code == code && x.UserId == userId)
            .Select(x => new
            {
                x.Id, x.Code, x.Status, x.Category, x.Priority, x.AssignedTeam, x.BookingCode, x.ParkingLotId, x.Description,
                x.EvidenceUrlsJson, x.RequestedAmount, x.OwnerResponseDueAtUtc, x.OwnerResponse, x.Resolution,
                x.ResolvedRefundAmount, x.CreatedAtUtc, x.UpdatedAtUtc,
                // Ghi chú nội bộ của Admin không trả cho tài xế.
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
                ComplaintAppService.ReadUrls(m.AttachmentUrlsJson), m.CreatedAtUtc)).ToList());
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
