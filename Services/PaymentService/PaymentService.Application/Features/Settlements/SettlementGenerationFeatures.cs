using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Settlements;

// ===== USE CASE: job sinh kỳ =====

/// <summary>Kết quả một lượt job: Skipped = instance khác đang chạy; Period = null khi chưa có kỳ nào kết thúc.</summary>
public sealed record SettlementRunResult(bool Skipped, SettlementPeriod? Period, int Created, int SkippedOwners)
{
    public static readonly SettlementRunResult SkippedRun = new(true, null, 0, 0);
}

public interface IRunSettlementGenerationUseCase
{
    Task<SettlementRunResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080 (AC1): bộ hẹn giờ sinh kỳ Draft cho kỳ vừa kết thúc của mọi chủ bãi có phát sinh. Không có JWT Admin nên không lấy được
/// khoản giữ tranh chấp → kỳ bị chặn HoldsNotLoaded cho tới khi Admin "Tính lại". Kỳ đã có thì bỏ qua (không ghi đè khoản giữ Admin đã nạp).
/// AdminService / BookingService lỗi → dừng lượt (DependencyUnavailableException), lượt sau thử lại – không đoán chu kỳ / trạng thái.
/// Chạy lặp an toàn: khóa advisory + index duy nhất (kỳ / chủ bãi, giao dịch, hoàn tiền, khoản giữ, voucher).
/// </summary>
public sealed class RunSettlementGenerationUseCase(ISettlementGenerationRepository repository, SettlementBuilder builder,
    TimeProvider timeProvider, ILogger<RunSettlementGenerationUseCase> logger) : IRunSettlementGenerationUseCase
{
    public async Task<SettlementRunResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var runLock = await repository.TryAcquireRunLockAsync(cancellationToken);
        if (runLock is null) return SettlementRunResult.SkippedRun;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var period = await builder.EnsurePeriodsAsync(now, cancellationToken);
        if (period is null) return new SettlementRunResult(false, null, 0, 0);

        var created = 0;
        var skipped = 0;
        foreach (var owner in await repository.ListCandidateOwnerIdsAsync(period.CutoffUtc, cancellationToken))
        {
            var outcome = await builder.GenerateForOwnerAsync(owner, period, now, userId: null, holds: null, regenerateDraft: false,
                cancellationToken);
            if (outcome.Outcome == SettlementOutcomes.Created)
            {
                created++;
                logger.LogInformation("US-080: đã sinh kỳ {Code} (chủ bãi {OwnerProfileId}), thực chi {NetPayout}, chặn: {Blocked}.",
                    outcome.Code, owner, outcome.NetPayout, string.Join(",", outcome.BlockedReasons));
            }
            else
            {
                skipped++;
                logger.LogDebug("US-080: bỏ qua chủ bãi {OwnerProfileId} kỳ {PeriodStart}: {Reason}.", owner, period.PeriodStart, outcome.Reason);
            }
        }
        return new SettlementRunResult(false, period, created, skipped);
    }
}

// ===== USE CASE: Admin sinh kỳ =====

/// <summary>PeriodStart = ngày đầu một kỳ đã kết thúc (bỏ trống = kỳ gần nhất); OwnerProfileId bỏ trống = mọi chủ bãi có phát sinh.</summary>
public sealed record GenerateSettlementsCommand(DateOnly? PeriodStart, int? OwnerProfileId);

public interface IGenerateSettlementsUseCase
{
    Task<SettlementGenerationResultDto> ExecuteAsync(GenerateSettlementsCommand command, int? adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080: Admin sinh (hoặc tính lại Draft) kỳ cho một / mọi chủ bãi, kèm khoản giữ tranh chấp lấy bằng JWT Admin.
/// Đang có lượt khác chạy → 409; kỳ chưa kết thúc / không phải đầu kỳ → 400; nguồn lỗi → 503.
/// </summary>
public sealed class GenerateSettlementsUseCase(ISettlementGenerationRepository repository, SettlementBuilder builder, IPayoutHoldClient holds,
    TimeProvider timeProvider) : IGenerateSettlementsUseCase
{
    public async Task<SettlementGenerationResultDto> ExecuteAsync(GenerateSettlementsCommand command, int? adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId is not > 0) throw new AuthenticationException("Không xác định được Admin từ token.");
        if (command.OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");

        await using var runLock = await repository.TryAcquireRunLockAsync(cancellationToken)
            ?? throw new ConflictException("Đang có lượt sinh kỳ quyết toán khác chạy, hãy thử lại sau ít phút.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var latest = await builder.EnsurePeriodsAsync(now, cancellationToken)
            ?? throw new ValidationException("Chưa có kỳ quyết toán nào kết thúc.");
        var period = latest;
        if (command.PeriodStart is { } start && start != latest.PeriodStart)
        {
            period = await repository.FindPeriodAsync(start, cancellationToken)
                ?? throw new ValidationException($"{start:yyyy-MM-dd} không phải ngày đầu của một kỳ quyết toán đã kết thúc.");
            if (period.PeriodStart > latest.PeriodStart)
                throw new ValidationException($"Kỳ bắt đầu {start:yyyy-MM-dd} chưa kết thúc.");
        }

        var openHolds = await holds.ListOpenHoldsAsync(command.OwnerProfileId, cancellationToken);
        // Chủ bãi chỉ có khoản giữ mới mà không có phát sinh nào khác thì không sinh kỳ (không có tiền để giữ);
        // chủ bãi cần giải phóng khoản giữ cũ đã nằm trong danh sách ứng viên (đọc từ các dòng DisputeHold).
        IReadOnlyList<int> owners = command.OwnerProfileId is { } owner
            ? [owner]
            : await repository.ListCandidateOwnerIdsAsync(period.CutoffUtc, cancellationToken);

        var outcomes = new List<OwnerSettlementOutcomeDto>(owners.Count);
        foreach (var ownerProfileId in owners)
            outcomes.Add(await builder.GenerateForOwnerAsync(ownerProfileId, period, now, adminUserId, openHolds, regenerateDraft: true,
                cancellationToken));
        return new SettlementGenerationResultDto(period.PeriodStart, period.PeriodEnd, period.Cycle, outcomes);
    }
}

// ===== USE CASE: Admin tính lại kỳ Draft =====

public interface IRegenerateSettlementUseCase
{
    Task<OwnerSettlementOutcomeDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080: tính lại một kỳ Draft với dữ liệu mới nhất (hoa hồng job vừa tính, booking vừa kết thúc, khoản giữ hiện tại, điều chỉnh vừa duyệt).
/// rowVersion (base64, lấy từ lần đọc trước) bắt buộc: dữ liệu đã đổi → 409 "tải lại". Kỳ không còn Draft → 409.
/// </summary>
public sealed class RegenerateSettlementUseCase(ISettlementGenerationRepository repository, SettlementBuilder builder, IPayoutHoldClient holds,
    TimeProvider timeProvider) : IRegenerateSettlementUseCase
{
    public async Task<OwnerSettlementOutcomeDto> ExecuteAsync(int settlementId, string? rowVersion, int? adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId is not > 0) throw new AuthenticationException("Không xác định được Admin từ token.");
        if (settlementId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var expected = ParseRowVersion(rowVersion);

        await using var runLock = await repository.TryAcquireRunLockAsync(cancellationToken)
            ?? throw new ConflictException("Đang có lượt sinh kỳ quyết toán khác chạy, hãy thử lại sau ít phút.");

        var draft = await repository.FindSettlementAsync(settlementId, cancellationToken)
            ?? throw new NotFoundException("Kỳ quyết toán", settlementId);
        var openHolds = await holds.ListOpenHoldsAsync(draft.OwnerProfileId, cancellationToken);
        return await builder.RegenerateAsync(draft, expected, timeProvider.GetUtcNow().UtcDateTime, adminUserId, openHolds, cancellationToken);
    }

    public static byte[] ParseRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) throw new ValidationException("rowVersion không được để trống.");
        try
        {
            return Convert.FromBase64String(rowVersion.Trim());
        }
        catch (FormatException)
        {
            throw new ValidationException("rowVersion không hợp lệ.");
        }
    }
}
