using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.Features;

namespace SupportService.Infrastructure.Persistence.Queries;

public sealed class SupportQueries(SupportDbContext db) : ISupportQueries
{
    public async Task<IReadOnlyList<ComplaintDto>> ListComplaintsByOwnerAsync(int ownerProfileId, ComplaintStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Complaints.AsNoTracking().Where(c => c.OwnerProfileId == ownerProfileId);
        if (status is { } s) query = query.Where(c => c.Status == s);

        return await query
            .OrderBy(c => c.Status == ComplaintStatus.AwaitingOwner ? 0 : 1)
            .ThenBy(c => c.OwnerResponseDueAtUtc)
            .Select(c => new ComplaintDto(c.Id, c.Code, c.Status.ToString(), c.Category.ToString(), c.UserId, c.BookingCode,
                c.ParkingLotId, c.Description, c.RequestedAmount, c.OwnerResponseDueAtUtc, c.OwnerResponse, c.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<LotReviewsDto> GetLotReviewsAsync(int parkingLotId, int take, CancellationToken cancellationToken)
    {
        var visible = db.Reviews.AsNoTracking().Where(r => r.ParkingLotId == parkingLotId && !r.IsHidden);

        // GroupBy → đếm số review theo từng mức sao (1..5) ngay trong SQL.
        var breakdown = await visible
            .GroupBy(r => r.Rating)
            .Select(g => new { Star = (int)g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Star, x => x.Count, cancellationToken);

        var count = breakdown.Values.Sum();
        var average = count == 0 ? 0 : Math.Round(breakdown.Sum(x => x.Key * x.Value) / (double)count, 2);

        var latest = await visible
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .Select(r => new ReviewDto(r.Id, r.ReviewerName, r.Rating, r.Comment, r.OwnerReply, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var full = Enumerable.Range(1, 5).ToDictionary(star => star, star => breakdown.GetValueOrDefault(star));
        return new LotReviewsDto(parkingLotId, average, count, full, latest);
    }
}
