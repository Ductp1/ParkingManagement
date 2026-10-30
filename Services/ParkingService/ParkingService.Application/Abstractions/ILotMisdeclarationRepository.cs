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

    /// <summary>Mọi bãi (chưa soft-delete) của chủ bãi, có tracking – để khóa vĩnh viễn khi cấm (US-056 AC2).</summary>
    Task<IReadOnlyList<ParkingLot>> GetOwnerLotsAsync(int ownerProfileId, CancellationToken cancellationToken = default);

    /// <summary>Case đang mở (Warned / Suspended) trên các bãi của chủ bãi, có tracking kèm bãi.</summary>
    Task<IReadOnlyList<LotMisdeclarationCase>> GetOpenCasesByOwnerAsync(int ownerProfileId, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ KYB còn mở (Draft / Submitted / UnderReview / NeedsMoreInfo) của các bãi, có tracking.</summary>
    Task<IReadOnlyList<KybApplication>> GetOpenKybApplicationsAsync(IReadOnlyCollection<int> parkingLotIds,
        CancellationToken cancellationToken = default);

    void Add(LotMisdeclarationCase misdeclarationCase);

    /// <summary>
    /// Lưu thay đổi đang tracking + ghi từng sự kiện vào OutboxMessages trong một SaveChanges.
    /// Xung đột RowVersion hoặc trùng case đang mở (unique index) → ConflictException.
    /// </summary>
    Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default);
}
