using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;

namespace ParkingService.Infrastructure.Persistence.Queries;

/// <summary>ADAPTER cổng đọc màn duyệt KYB (US-055): AsNoTracking + Select, không nạp entity đầy đủ.</summary>
public sealed class AdminKybQueries(ParkingDbContext db) : IAdminKybQueries
{
    public async Task<PagedResult<AdminKybListRow>> ListAsync(KybStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = db.KybApplications.AsNoTracking();
        if (status is { } s) query = query.Where(k => k.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            var byId = int.TryParse(search.Trim().TrimStart('#'), out var id) ? id : (int?)null;
            query = query.Where(k => EF.Functions.ILike(k.ParkingLot.Name, pattern)
                                     || EF.Functions.ILike(k.ParkingLot.Address, pattern)
                                     || (byId != null && k.Id == byId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(k => k.SubmittedAtUtc ?? k.CreatedAtUtc)
            .ThenByDescending(k => k.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => new AdminKybListRow(k.Id, k.ParkingLotId, k.ParkingLot.Name, k.ParkingLot.Address, k.ParkingLot.City,
                k.ParkingLot.District, k.ParkingLot.OwnerProfileId, k.ParkingLot.TotalSlots, k.Status, k.ParkingLot.Status,
                k.SubmittedAtUtc, k.FieldSurveyRequired, k.FieldSurveyAtUtc, k.FieldSurveyCompletedAtUtc, k.ReviewedByUserId,
                k.ReviewedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminKybListRow>(items, page, pageSize, total);
    }

    public Task<AdminKybDetailRow?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
        => db.KybApplications.AsNoTracking()
            .Where(k => k.Id == id)
            .Select(k => new AdminKybDetailRow(
                k.Id, k.Status, k.BusinessLicenseUrl, k.SitePhotoUrlsJson, k.PhotoLatitude, k.PhotoLongitude,
                k.FireSafetyCertificateUrl, k.FieldSurveyRequired, k.FieldSurveyAtUtc, k.FieldSurveyCompletedAtUtc,
                k.FieldSurveyNote, k.CreatedAtUtc, k.SubmittedAtUtc, k.ReviewedByUserId, k.ReviewedAtUtc, k.RejectReason, k.ReviewNote,
                k.ParkingLotId, k.ParkingLot.Name, k.ParkingLot.Address, k.ParkingLot.City, k.ParkingLot.District,
                k.ParkingLot.Latitude, k.ParkingLot.Longitude, k.ParkingLot.TotalSlots, k.ParkingLot.MaxHeightCm,
                k.ParkingLot.OpenTime, k.ParkingLot.CloseTime, k.ParkingLot.Status, k.ParkingLot.OwnerProfileId,
                db.Slots.Count(sl => sl.IsActive && sl.Floor.Zone.ParkingLotId == k.ParkingLotId)))
            .FirstOrDefaultAsync(cancellationToken);
}
