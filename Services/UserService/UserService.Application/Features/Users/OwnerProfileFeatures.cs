using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Users;

// ===== DTO =====
public sealed record LockOwnerProfileRequest(string? Reason, DateTime? LockedUntilUtc, int? PerformedByUserId);
public sealed record UnlockOwnerProfileRequest(string? Reason, int? PerformedByUserId);

public sealed record OwnerAccountDto(
    int OwnerProfileId,
    int UserId,
    string BusinessName,
    string FullName,
    string? Email,
    string? PhoneNumber,
    bool IsLocked,
    string OwnerStatus,
    string UserStatus);

// ===== PORT =====
public interface IOwnerProfileRepository
{
    Task<OwnerProfile?> FindByIdWithUserAsync(int ownerProfileId, CancellationToken cancellationToken);
    Task UpdateAsync(OwnerProfile ownerProfile, CancellationToken cancellationToken);
}

// ===== USE CASES =====
public interface ILockOwnerProfileUseCase
{
    Task<OwnerAccountDto?> ExecuteAsync(int ownerProfileId, LockOwnerProfileRequest request, CancellationToken cancellationToken = default);
}

public sealed class LockOwnerProfileUseCase(IOwnerProfileRepository repository) : ILockOwnerProfileUseCase
{
    public async Task<OwnerAccountDto?> ExecuteAsync(int ownerProfileId, LockOwnerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithUserAsync(ownerProfileId, cancellationToken);
        if (owner is null) return null;

        // Idempotent: nếu đã khóa rồi thì giữ nguyên trạng thái khóa, cập nhật lý do và thời gian
        owner.IsLocked = true;
        owner.Status = OwnerStatus.Suspended;
        if (owner.User is not null)
        {
            owner.User.LockReason = request.Reason;
            owner.User.LockedUntilUtc = request.LockedUntilUtc;
        }

        await repository.UpdateAsync(owner, cancellationToken);

        return new OwnerAccountDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.User?.FullName ?? string.Empty,
            owner.User?.Email,
            owner.User?.PhoneNumber,
            owner.IsLocked,
            owner.Status.ToString(),
            owner.User?.Status.ToString() ?? "Active"
        );
    }
}

public interface IUnlockOwnerProfileUseCase
{
    Task<OwnerAccountDto?> ExecuteAsync(int ownerProfileId, UnlockOwnerProfileRequest request, CancellationToken cancellationToken = default);
}

public sealed class UnlockOwnerProfileUseCase(IOwnerProfileRepository repository) : IUnlockOwnerProfileUseCase
{
    public async Task<OwnerAccountDto?> ExecuteAsync(int ownerProfileId, UnlockOwnerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var owner = await repository.FindByIdWithUserAsync(ownerProfileId, cancellationToken);
        if (owner is null) return null;

        // Idempotent: nếu đã mở khóa rồi thì giữ nguyên
        owner.IsLocked = false;
        owner.Status = OwnerStatus.Active;
        if (owner.User is not null)
        {
            owner.User.LockReason = null;
            owner.User.LockedUntilUtc = null;
        }

        await repository.UpdateAsync(owner, cancellationToken);

        return new OwnerAccountDto(
            owner.Id,
            owner.UserId,
            owner.BusinessName,
            owner.User?.FullName ?? string.Empty,
            owner.User?.Email,
            owner.User?.PhoneNumber,
            owner.IsLocked,
            owner.Status.ToString(),
            owner.User?.Status.ToString() ?? "Active"
        );
    }
}
