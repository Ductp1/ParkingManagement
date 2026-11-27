using ParkingManagement.SharedKernel.Contracts;
using SupportService.Application.DTOs;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Application.Interfaces;

// ===== US-091: các port SupportService cần – Infrastructure cài đặt (EF Core / HTTP chỉ đọc) =====

public interface ILotFullIncidentRepository
{
    Task<bool> ExistsForBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    /// <summary>Có tracking để cập nhật; null nếu không có.</summary>
    Task<LotFullIncident?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    /// <summary>Thêm sự cố + sự kiện vào Outbox trong cùng transaction.</summary>
    Task AddAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default);
    /// <summary>Lưu thay đổi + sự kiện; bên khác vừa cập nhật (RowVersion) → ConflictException.</summary>
    Task UpdateAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotFullIncident>> ListAsync(int? parkingLotId, LotFullIncidentStatus? status, bool slaViolationOnly,
        int take, CancellationToken cancellationToken = default);
}

/// <summary>Vị trí + chỗ trống của 1 bãi (ParkingService).</summary>
public sealed record LotLocation(int Id, string Name, double Latitude, double Longitude, int AvailableSlots);

/// <summary>ParkingService (chỉ đọc, API có sẵn của US-014/US-018): chi tiết bãi và tìm bãi quanh 1 tọa độ.</summary>
public interface IParkingLotSearch
{
    Task<LotLocation?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default);
    /// <summary>Bãi còn chỗ, đang mở trong bán kính, gần nhất trước.</summary>
    Task<IReadOnlyList<AlternativeLotDto>> SearchAvailableAsync(double latitude, double longitude, double radiusKm,
        CancellationToken cancellationToken = default);
}

public interface ILotFullIncidentService
{
    Task<LotFullIncidentDto> ReportAsync(int userId, ReportLotFullRequestDto request, CancellationToken cancellationToken = default);
    Task<LotFullIncidentDto> GetAsync(int userId, string code, CancellationToken cancellationToken = default);
    Task<LotFullIncidentDto> AcceptTransferAsync(int userId, string code, AcceptTransferRequestDto request, CancellationToken cancellationToken = default);
    Task<LotFullIncidentDto> DeclineAsync(int userId, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotFullIncidentDto>> ListForAdminAsync(int? parkingLotId, string? status, bool slaViolationOnly,
        CancellationToken cancellationToken = default);
}
