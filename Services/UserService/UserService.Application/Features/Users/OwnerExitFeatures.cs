using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Users;

// ===== DTOs (US-057) =====
public sealed record RequestOwnerExitRequest(string Reason, int? NoticeDays = 30);
public sealed record CancelOwnerExitRequest(string? Reason = null);

public sealed record OwnerExitStatusDto(
    int OwnerProfileId,
    int UserId,
    string BusinessName,
    string Status,
    DateTime? ExitRequestedAtUtc,
    DateTime? ExitEffectiveAtUtc,
    int? RemainingDays,
    bool CanCancel,
    string? ExitReason);

// ===== USE CASES (US-057) =====
public interface IRequestOwnerExitUseCase
{
    Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, RequestOwnerExitRequest request, CancellationToken cancellationToken = default);
}

public sealed class RequestOwnerExitUseCase(IOwnerProfileRepository repository, TimeProvider timeProvider) : IRequestOwnerExitUseCase
{
    public async Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, RequestOwnerExitRequest request, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithUserAsync(ownerProfileId, cancellationToken)
            ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        owner.RequestExit(request.Reason, request.NoticeDays ?? 30, nowUtc);

        await repository.UpdateAsync(owner, cancellationToken);

        var remainingDays = owner.ExitEffectiveAtUtc.HasValue
            ? (int)Math.Max(0, Math.Ceiling((owner.ExitEffectiveAtUtc.Value - nowUtc).TotalDays))
            : (int?)null;

        return new OwnerExitStatusDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.Status.ToString(),
            owner.ExitRequestedAtUtc,
            owner.ExitEffectiveAtUtc,
            remainingDays,
            CanCancel: true,
            owner.ExitReason);
    }
}

public interface ICancelOwnerExitUseCase
{
    Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancelOwnerExitRequest? request = null, CancellationToken cancellationToken = default);
}

public sealed class CancelOwnerExitUseCase(IOwnerProfileRepository repository, TimeProvider timeProvider) : ICancelOwnerExitUseCase
{
    public async Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancelOwnerExitRequest? request = null, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithUserAsync(ownerProfileId, cancellationToken)
            ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        owner.CancelExit(nowUtc);

        await repository.UpdateAsync(owner, cancellationToken);

        return new OwnerExitStatusDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.Status.ToString(),
            owner.ExitRequestedAtUtc,
            owner.ExitEffectiveAtUtc,
            RemainingDays: null,
            CanCancel: false,
            owner.ExitReason);
    }
}

public interface ITerminateOwnerExitUseCase
{
    Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class TerminateOwnerExitUseCase(IOwnerProfileRepository repository, TimeProvider timeProvider) : ITerminateOwnerExitUseCase
{
    public async Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithStaffAssignmentsAsync(ownerProfileId, cancellationToken)
            ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        owner.Terminate(nowUtc);

        await repository.UpdateAsync(owner, cancellationToken);

        return new OwnerExitStatusDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.Status.ToString(),
            owner.ExitRequestedAtUtc,
            owner.ExitEffectiveAtUtc,
            RemainingDays: 0,
            CanCancel: false,
            owner.ExitReason);
    }
}

public interface IGetOwnerExitStatusUseCase
{
    Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class GetOwnerExitStatusUseCase(IOwnerProfileRepository repository, TimeProvider timeProvider) : IGetOwnerExitStatusUseCase
{
    public async Task<OwnerExitStatusDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithUserAsync(ownerProfileId, cancellationToken)
            ?? throw new NotFoundException("Hồ sơ chủ bãi", ownerProfileId);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var remainingDays = (owner.Status == OwnerStatus.Leaving && owner.ExitEffectiveAtUtc.HasValue)
            ? (int)Math.Max(0, Math.Ceiling((owner.ExitEffectiveAtUtc.Value - nowUtc).TotalDays))
            : (int?)null;

        var canCancel = owner.Status == OwnerStatus.Leaving && (!owner.ExitEffectiveAtUtc.HasValue || nowUtc < owner.ExitEffectiveAtUtc.Value);

        return new OwnerExitStatusDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.Status.ToString(),
            owner.ExitRequestedAtUtc,
            owner.ExitEffectiveAtUtc,
            remainingDays,
            canCancel,
            owner.ExitReason);
    }
}
