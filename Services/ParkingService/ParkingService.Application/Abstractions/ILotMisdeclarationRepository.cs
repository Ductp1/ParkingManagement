using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT ghi cho xử lý bãi khai báo sai (US-056). Case, bãi và sự kiện outbox (AuditRecorded) được lưu trong CÙNG MỘT lần SaveChanges.
/// </summary>
public interface ILotMisdeclarationRepository
{
    /// <summary>Bãi có tracking (để tạm khóa khi mở case). Bãi đã soft-delete → null.</summary>
    Task<ParkingLot?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>Case có tracking kèm bãi, chỉ khi case thuộc đúng bãi.</summary>
    Task<LotMisdeclarationCase?> GetCaseAsync(int parkingLotId, int caseId, CancellationToken cancellationToken = default);

    Task<bool> HasOpenCaseAsync(int parkingLotId, CancellationToken cancellationToken = default);

    void Add(LotMisdeclarationCase misdeclarationCase);

    /// <summary>
    /// Lưu thay đổi đang tracking + ghi từng sự kiện vào OutboxMessages trong một SaveChanges.
    /// Xung đột RowVersion hoặc trùng case đang mở (unique index) → ConflictException.
    /// </summary>
    Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default);
}
