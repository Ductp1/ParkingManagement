using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;

namespace SupportService.Test;

/// <summary>IComplaintRepository giả lập trong bộ nhớ cho unit test (không cần database).</summary>
public class InMemoryComplaintRepository : IComplaintRepository
{
    /// <summary>Ép kết quả "booking đã có ticket đang mở" (test US-088).</summary>
    public bool HasOpenTicket { get; set; }

    /// <summary>Ticket đã lưu (Add) hoặc được nạp sẵn để test.</summary>
    public List<Complaint> Saved { get; } = [];

    /// <summary>Sự kiện đã ghi vào "Outbox" qua UpdateAsync.</summary>
    public List<IntegrationEvent> Events { get; } = [];

    public int UpdateCount { get; private set; }

    public Task<bool> HasOpenComplaintForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasOpenTicket);

    public Task AddAsync(Complaint complaint, CancellationToken cancellationToken = default)
    {
        Saved.Add(complaint);
        return Task.CompletedTask;
    }

    public Task<ComplaintDetailDto?> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Saved.Where(c => c.Code == code && c.UserId == userId).Select(ToDetail).FirstOrDefault());

    public Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ComplaintSummaryDto>>(Saved.Where(c => c.UserId == userId)
            .Select(c => new ComplaintSummaryDto(c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(),
                c.BookingCode, c.CreatedAtUtc, c.UpdatedAtUtc)).ToList());

    public Task<Complaint?> FindForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult(Saved.FirstOrDefault(c => c.Code == code && c.OwnerProfileId == ownerProfileId));

    public Task<ComplaintDetailDto?> GetForOwnerAsync(string code, int ownerProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult(Saved.Where(c => c.Code == code && c.OwnerProfileId == ownerProfileId).Select(ToDetail).FirstOrDefault());

    /// <summary>Mã ticket mà lần UpdateAsync kế tiếp sẽ gặp xung đột RowVersion (giả lập bên khác vừa cập nhật).</summary>
    public HashSet<string> ConflictOnUpdate { get; } = [];

    /// <summary>Chạy ngay trước khi FindByIdAsync trả ticket – giả lập thao tác chen vào giữa lúc nạp danh sách và lúc xử lý.</summary>
    public Action<Complaint>? BeforeFind { get; set; }

    public Task<IReadOnlyList<int>> ListOverdueAwaitingOwnerIdsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        EnsureIds();
        return Task.FromResult<IReadOnlyList<int>>(Saved
            .Where(c => c.Status == ComplaintStatus.AwaitingOwner && c.OwnerResponseDueAtUtc < nowUtc)
            .OrderBy(c => c.OwnerResponseDueAtUtc).Take(take).Select(c => c.Id).ToList());
    }

    public Task<Complaint?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureIds();
        var complaint = Saved.FirstOrDefault(c => c.Id == id);
        if (complaint is not null) BeforeFind?.Invoke(complaint);
        return Task.FromResult(complaint);
    }

    public Task UpdateAsync(Complaint complaint, IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        // Giống repository thật: xung đột RowVersion → ConflictException, không ghi sự kiện.
        if (ConflictOnUpdate.Remove(complaint.Code))
            throw new ParkingManagement.SharedKernel.Exceptions.ConflictException($"Tranh chấp {complaint.Code} vừa được cập nhật bởi thao tác khác.");
        UpdateCount++;
        Events.Add(integrationEvent);
        return Task.CompletedTask;
    }

    /// <summary>BaseEntity.Id có setter protected (DB tự sinh) → gán Id giả cho ticket nạp sẵn trong test.</summary>
    private void EnsureIds()
    {
        var idProperty = typeof(ParkingManagement.SharedKernel.Domain.BaseEntity).GetProperty(nameof(Complaint.Id))!;
        var next = Saved.Count == 0 ? 1 : Saved.Max(c => c.Id) + 1;
        foreach (var complaint in Saved.Where(c => c.Id == 0)) idProperty.SetValue(complaint, next++);
    }

    public Task<IReadOnlyList<PayoutHoldDto>> ListPayoutHoldsAsync(int? ownerProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PayoutHoldDto>>(Saved
            .Where(c => c.PayoutHeld && c.Status is not (ComplaintStatus.Resolved or ComplaintStatus.Rejected))
            .Where(c => ownerProfileId is null || c.OwnerProfileId == ownerProfileId)
            .Select(c => new PayoutHoldDto(c.Code, c.Status.ToString(), c.ParkingLotId, c.OwnerProfileId, c.BookingCode,
                c.RequestedAmount ?? 0, c.CreatedAtUtc)).ToList());

    private static ComplaintDetailDto ToDetail(Complaint c) => new(
        c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.Priority.ToString(), c.AssignedTeam.ToString(),
        c.BookingCode, c.ParkingLotId, c.Description, [], c.RequestedAmount, c.OwnerResponseDueAtUtc, c.OwnerResponse,
        c.Resolution, c.ResolvedRefundAmount, c.CreatedAtUtc, c.UpdatedAtUtc,
        c.Messages.Where(m => !m.IsInternal).Select(m => new ComplaintMessageDto(m.Id, m.SenderParty.ToString(), m.Message, [], m.CreatedAtUtc)).ToList(),
        c.OwnerRespondedAtUtc, c.EscalatedAtUtc, c.PayoutHeld);
}
