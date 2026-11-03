using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Abstractions;

/// <summary>Bãi đỗ cho màn xử lý khai báo sai – đọc cả bãi Suspended (API public trả 404 với bãi không công khai).</summary>
public sealed record MisdeclarationLotRow(int Id, string Name, string Address, int OwnerProfileId, ParkingLotStatus Status);

/// <summary>Một case khai báo sai (dữ liệu thô, Application tự tính quá hạn).</summary>
public sealed record MisdeclarationCaseRow(int Id, MisdeclarationSeverity Severity, MisdeclarationCategory Category,
    MisdeclarationCaseStatus Status, string Finding, DateTime DueAtUtc, int OpenedByUserId, DateTime OpenedAtUtc,
    string? EscalationNote, DateTime? EscalatedAtUtc, int? ClosedByUserId, DateTime? ClosedAtUtc, string? ResolutionNote);

/// <summary>PORT đọc (CQRS-lite) cho xử lý khai báo sai (US-056): AsNoTracking + Select, tách khỏi cổng ghi.</summary>
public interface ILotMisdeclarationQueries
{
    Task<MisdeclarationLotRow?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử case của bãi, mới mở trước.</summary>
    Task<IReadOnlyList<MisdeclarationCaseRow>> ListCasesAsync(int parkingLotId, CancellationToken cancellationToken = default);
}
