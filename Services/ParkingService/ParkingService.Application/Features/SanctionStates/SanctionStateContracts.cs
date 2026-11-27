using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.SanctionStates;

/// <summary>PUT /api/v1/parking-lots/sanction-states/{sanctionId} – AdminService đẩy hiệu lực chế tài (US-097).</summary>
public sealed record UpsertSanctionStateRequest(
    int OwnerProfileId,
    int? ParkingLotId,
    SanctionStateKind? Kind,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc);

public sealed record SanctionStateDto(int SanctionId, int OwnerProfileId, int? ParkingLotId, string Kind, DateTime StartsAtUtc,
    DateTime? EndsAtUtc, DateTime? LiftedAtUtc, bool IsInEffect);

/// <summary>Trạng thái công khai (ẩn danh) cho tài xế / BookingService: chỉ biết bãi có đang bị tạm ngưng nhận đặt chỗ hay không.</summary>
public sealed record LotSanctionStatusDto(bool IsListingSuspended, DateTime? ListingSuspendedUntilUtc);

/// <summary>Trạng thái đầy đủ cho Admin: tạm ngưng, hạ thứ hạng và các chế tài đang hiệu lực (theo bãi + theo chủ bãi).</summary>
public sealed record LotSanctionStateDto(int ParkingLotId, int OwnerProfileId, bool IsListingSuspended, DateTime? ListingSuspendedUntilUtc,
    bool IsRankingPenalized, DateTime? RankingPenaltyUntilUtc, IReadOnlyList<SanctionStateDto> ActiveStates);

/// <summary>Gộp chế tài đang hiệu lực thành tác động lên từng bãi (theo bãi + theo chủ bãi).</summary>
public sealed class LotSanctionEffects
{
    public static readonly LotSanctionEffects None = new([]);

    private readonly IReadOnlyList<SanctionState> states;

    public LotSanctionEffects(IReadOnlyList<SanctionState> inEffectStates) => states = inEffectStates;

    public IEnumerable<SanctionState> For(int parkingLotId, int ownerProfileId)
        => states.Where(s => s.ParkingLotId == parkingLotId || (s.ParkingLotId == null && s.OwnerProfileId == ownerProfileId));

    public bool IsListingSuspended(int parkingLotId, int ownerProfileId)
        => For(parkingLotId, ownerProfileId).Any(s => s.Kind == SanctionStateKind.ListingSuspension);

    public bool IsRankingPenalized(int parkingLotId, int ownerProfileId)
        => For(parkingLotId, ownerProfileId).Any(s => s.Kind == SanctionStateKind.RankingPenalty);

    /// <summary>Mốc hết hiệu lực muộn nhất của loại chế tài; có chế tài không thời hạn → null.</summary>
    public DateTime? Until(int parkingLotId, int ownerProfileId, SanctionStateKind kind)
    {
        var ofKind = For(parkingLotId, ownerProfileId).Where(s => s.Kind == kind).ToList();
        return ofKind.Count == 0 || ofKind.Any(s => s.EndsAtUtc is null) ? null : ofKind.Max(s => s.EndsAtUtc);
    }

    public static SanctionStateDto ToDto(SanctionState s, DateTime nowUtc)
        => new(s.SanctionId, s.OwnerProfileId, s.ParkingLotId, s.Kind.ToString(), s.StartsAtUtc, s.EndsAtUtc, s.LiftedAtUtc,
            s.IsInEffect(nowUtc));
}
