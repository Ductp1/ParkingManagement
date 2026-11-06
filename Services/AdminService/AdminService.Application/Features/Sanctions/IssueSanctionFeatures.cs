using System.Text.Json;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

public interface IIssueSanctionUseCase
{
    Task<IssueSanctionResultDto> ExecuteAsync(IssueSanctionRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097 (UC-41): Admin áp chế tài SLA 4 cấp (Điều 7 hợp đồng mẫu).
///  - Cảnh cáo (bãi / toàn chủ bãi), hạ hạng (bãi / toàn chủ bãi), tạm ngừng niêm yết (bãi): mỗi bãi 1 chế tài, tất cả + audit log ghi
///    trong 1 transaction; bãi nào đang có chế tài cùng cấp còn hiệu lực → 409 cả request. ParkingService / thông báo ở trạng thái
///    chờ đồng bộ (thi hành ở bước đồng bộ, không gọi trong use case này).
///  - Tạm ngừng toàn chủ bãi (khóa tài khoản có hạn) và chấm dứt hợp tác (khóa vĩnh viễn): đi qua lõi khóa chủ bãi của US-096
///    (OwnerLockCore) – cùng unique index, audit "Owner.Locked" và đồng bộ UserService; khóa tạm đang hiệu lực được thay bằng khóa
///    vĩnh viễn khi nâng lên cấp 4.
/// Tiền phạt chỉ ghi nhận (cấn trừ ở quyết toán – US-080).
/// </summary>
public sealed class IssueSanctionUseCase(
    IUserServiceClient userService,
    IOwnerLockRepository lockRepository,
    ISanctionRepository repository,
    IParkingLotDirectory lotDirectory,
    ISanctionAdminQueries queries,
    SanctionSettings settings,
    TimeProvider timeProvider,
    ILogger<IssueSanctionUseCase> logger) : IIssueSanctionUseCase
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);
    private readonly OwnerLockCore core = new(userService, timeProvider, logger);

    public async Task<IssueSanctionResultDto> ExecuteAsync(IssueSanctionRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (request.OwnerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (request.OwnerUserId <= 0) throw new ValidationException("ownerUserId phải là số nguyên dương.");
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (request.Level is not { } level || !Enum.IsDefined(level)) throw new ValidationException("Cấp chế tài (level) không hợp lệ.");
        if (request.RiskFlagId is <= 0) throw new ValidationException("riskFlagId phải là số nguyên dương.");
        var reason = SanctionRules.RequireReason(request.Reason, "áp chế tài");
        var evidence = SanctionRules.NormalizeEvidence(request.EvidenceJson);
        var penalty = SanctionRules.NormalizePenalty(request.PenaltyAmount);
        var lotIds = SanctionRules.NormalizeLotIds(request.ParkingLotIds);
        var endsAtUtc = UtcDateTime.ToUtc(request.EndsAtUtc);
        var correctionDueAtUtc = UtcDateTime.ToUtc(request.CorrectionDueAtUtc);
        SanctionRules.ValidatePeriod(level, lotIds.Count > 0, now, endsAtUtc, correctionDueAtUtc);

        // Chủ bãi có thật và OwnerUserId đúng là người sở hữu (người nhận thông báo).
        await core.VerifyOwnerAsync(request.OwnerProfileId, request.OwnerUserId, cancellationToken);

        var riskFlag = await LoadRiskFlagAsync(request, lotIds, cancellationToken);
        var extras = new SanctionExtras(request.OwnerUserId, penalty, evidence,
            SanctionRules.AppealDeadlineUtc(now, settings.AppealBusinessDays));

        var isOwnerLock = lotIds.Count == 0 && level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan;
        var ids = isOwnerLock
            ? [await IssueOwnerLockAsync(request, level, reason, endsAtUtc, extras, riskFlag, performedByUserId, now, cancellationToken)]
            : await IssueAsync(request, level, lotIds, reason, endsAtUtc, correctionDueAtUtc, extras, riskFlag, performedByUserId, now,
                cancellationToken);

        return new IssueSanctionResultDto(await queries.ListByIdsAsync(ids, now, cancellationToken));
    }

    /// <summary>RiskFlag phải tồn tại, còn chờ xử lý và đúng đối tượng (chủ bãi / user chủ bãi / một trong các bãi).</summary>
    private async Task<RiskFlag?> LoadRiskFlagAsync(IssueSanctionRequest request, IReadOnlyList<int> lotIds, CancellationToken cancellationToken)
    {
        if (request.RiskFlagId is not { } riskFlagId) return null;
        var flag = await repository.FindRiskFlagTrackedAsync(riskFlagId, cancellationToken)
            ?? throw new NotFoundException("Cảnh báo rủi ro", riskFlagId);
        var matches = flag.SubjectType switch
        {
            RiskSubjectType.OwnerProfile => flag.SubjectId == request.OwnerProfileId,
            RiskSubjectType.User => flag.SubjectId == request.OwnerUserId,
            RiskSubjectType.ParkingLot => lotIds.Contains(flag.SubjectId),
            _ => false,
        };
        if (!matches)
            throw new ValidationException($"Cảnh báo rủi ro #{riskFlagId} ({flag.SubjectType} #{flag.SubjectId}) không thuộc chủ bãi / bãi được chọn.");
        if (!flag.IsPending) throw new ConflictException($"Cảnh báo rủi ro #{riskFlagId} đã được xử lý ({flag.Status}).");
        return flag;
    }

    /// <summary>Cấp 3 toàn chủ bãi / cấp 4: tái dùng lõi khóa chủ bãi US-096.</summary>
    private async Task<int> IssueOwnerLockAsync(IssueSanctionRequest request, SanctionLevel level, string reason, DateTime? endsAtUtc,
        SanctionExtras extras, RiskFlag? riskFlag, int performedByUserId, DateTime now, CancellationToken cancellationToken)
    {
        int? supersededId = null;
        var active = await lockRepository.FindActiveOwnerLockTrackedAsync(request.OwnerProfileId, cancellationToken);
        if (active is not null && active.IsInEffect(now))
        {
            if (level == SanctionLevel.PermanentBan && active.Level == SanctionLevel.TemporarySuspension)
            {
                active.Supersede($"Nâng lên cấp 4 – chấm dứt hợp tác: {reason}".Truncate(Sanction.ReasonMaxLength), performedByUserId, now);
                supersededId = active.Id;
            }
            else
            {
                throw new ConflictException($"Chủ bãi {request.OwnerProfileId} đang bị khóa bởi chế tài #{active.Id} " +
                    $"({SanctionRules.LevelLabel(active.Level)}). Gỡ hoặc đồng bộ lại chế tài đó trước.");
            }
        }
        else
        {
            active?.Expire();
        }

        var sanction = Sanction.CreateOwnerLock(request.OwnerProfileId, reason, performedByUserId, now,
            level == SanctionLevel.PermanentBan ? null : endsAtUtc, extras);
        await lockRepository.AddLockAsync(sanction, OwnerLockCore.BuildLockAuditLog(sanction, request.OwnerUserId, supersededId),
            cancellationToken);
        // Gắn cảnh báo rủi ro sau khi có Id; được ghi cùng lần lưu kết quả đồng bộ bên dưới (chung DbContext).
        riskFlag?.ResolveWithSanction(sanction.Id, now);

        await core.SyncLockAsync(sanction, cancellationToken);
        await lockRepository.SaveAsync(cancellationToken);
        return sanction.Id;
    }

    /// <summary>Cảnh cáo / hạ hạng / tạm ngừng niêm yết: mỗi bãi 1 chế tài (hoặc 1 chế tài toàn chủ bãi).</summary>
    private async Task<IReadOnlyList<int>> IssueAsync(IssueSanctionRequest request, SanctionLevel level, IReadOnlyList<int> lotIds,
        string reason, DateTime? endsAtUtc, DateTime? correctionDueAtUtc, SanctionExtras extras, RiskFlag? riskFlag,
        int performedByUserId, DateTime now, CancellationToken cancellationToken)
    {
        if (lotIds.Count > 0)
        {
            var ownerLots = (await lotDirectory.ListOwnerLotsAsync(request.OwnerProfileId, cancellationToken)).Select(l => l.Id).ToHashSet();
            var foreign = lotIds.Where(id => !ownerLots.Contains(id)).ToList();
            if (foreign.Count > 0)
                throw new ValidationException($"Bãi {SanctionRules.JoinIds(foreign)} không thuộc chủ bãi {request.OwnerProfileId}.");
        }

        await EnsureNoActiveDuplicateAsync(request.OwnerProfileId, level, lotIds, now, cancellationToken);

        IReadOnlyList<int?> scopes = lotIds.Count == 0 ? [null] : lotIds.Select(id => (int?)id).ToList();
        var sanctions = scopes
            .Select(lotId => Sanction.Issue(request.OwnerProfileId, lotId, level, reason, performedByUserId, now, endsAtUtc,
                correctionDueAtUtc, extras))
            .ToList();

        await repository.AddIssuedAsync(sanctions, s => BuildIssuedAuditLog(s, performedByUserId),
            riskFlag is null ? null : inserted => riskFlag.ResolveWithSanction(PickForRiskFlag(inserted, riskFlag).Id, now),
            cancellationToken);
        return sanctions.Select(s => s.Id).ToList();
    }

    /// <summary>
    /// Mỗi bãi tối đa 1 hạ hạng và 1 tạm ngừng niêm yết còn hiệu lực; mỗi chủ bãi tối đa 1 hạ hạng toàn chủ bãi. Chế tài Active
    /// đã quá hạn (job chưa đóng) được đóng luôn để nhường chỗ – ghi cùng transaction với chế tài mới.
    /// </summary>
    private async Task EnsureNoActiveDuplicateAsync(int ownerProfileId, SanctionLevel level, IReadOnlyList<int> lotIds, DateTime now,
        CancellationToken cancellationToken)
    {
        if (level == SanctionLevel.Warning) return;

        if (lotIds.Count == 0)
        {
            var ownerPenalty = await repository.FindActiveOwnerRankingPenaltyTrackedAsync(ownerProfileId, cancellationToken);
            if (ownerPenalty is null) return;
            if (ownerPenalty.IsInEffect(now))
                throw new ConflictException($"Chủ bãi {ownerProfileId} đang bị hạ thứ hạng toàn bộ bãi (chế tài #{ownerPenalty.Id}).");
            ownerPenalty.Expire();
            return;
        }

        var existing = await repository.FindActiveLotSanctionsTrackedAsync(lotIds, level, cancellationToken);
        var inEffect = existing.Where(s => s.IsInEffect(now)).ToList();
        if (inEffect.Count > 0)
        {
            var details = string.Join("; ", inEffect.OrderBy(s => s.ParkingLotId).Select(s => $"bãi {s.ParkingLotId} – chế tài #{s.Id}"));
            throw new ConflictException(
                $"Bãi {SanctionRules.JoinIds(inEffect.Select(s => s.ParkingLotId!.Value))} đang có chế tài " +
                $"{SanctionRules.LevelLabel(level)} còn hiệu lực ({details}). Không áp chế tài nào trong yêu cầu này.");
        }
        foreach (var stale in existing) stale.Expire();
    }

    /// <summary>Cảnh báo về một bãi gắn với chế tài của đúng bãi đó; còn lại gắn chế tài đầu tiên.</summary>
    private static Sanction PickForRiskFlag(IReadOnlyList<Sanction> inserted, RiskFlag riskFlag)
        => riskFlag.SubjectType == RiskSubjectType.ParkingLot
            ? inserted.First(s => s.ParkingLotId == riskFlag.SubjectId)
            : inserted[0];

    private static AuditLog BuildIssuedAuditLog(Sanction sanction, int performedByUserId) => new()
    {
        SourceService = "AdminService",
        UserId = performedByUserId,
        OwnerProfileId = sanction.OwnerProfileId,
        Action = "Sanction.Issued",
        EntityName = "Sanction",
        EntityId = sanction.Id.ToString(),
        NewValuesJson = JsonSerializer.Serialize(new
        {
            level = sanction.Level.ToString(),
            parkingLotId = sanction.ParkingLotId,
            ownerUserId = sanction.OwnerUserId,
            startsAtUtc = sanction.StartsAtUtc,
            endsAtUtc = sanction.EndsAtUtc,
            correctionDueAtUtc = sanction.CorrectionDueAtUtc,
            appealDeadlineUtc = sanction.AppealDeadlineUtc,
            penaltyAmount = sanction.PenaltyAmount,
        }, AuditJson),
        Reason = sanction.Reason,
    };
}

internal static class StringTruncation
{
    public static string Truncate(this string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
