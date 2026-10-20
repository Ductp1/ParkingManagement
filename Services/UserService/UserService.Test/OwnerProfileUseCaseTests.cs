using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Users;
using UserService.Domain.Entities;

namespace UserService.Test;

public class OwnerProfileUseCaseTests
{
    private static void SetId(BaseEntity entity, int id)
        => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);

    [Fact]
    public async Task Lock_owner_updates_profile_and_user_status_and_is_idempotent()
    {
        var user = new User
        {
            FullName = "Nguyễn Văn Chủ Bãi",
            Email = "owner@test.com",
            PhoneNumber = "0901234567",
            Status = UserStatus.Active
        };
        SetId(user, 10);

        var profile = new OwnerProfile
        {
            UserId = user.Id,
            BusinessName = "Bãi Xe Vincom",
            IsLocked = false,
            Status = OwnerStatus.Active,
            User = user
        };
        SetId(profile, 5);

        var repo = new FakeOwnerRepo(profile);
        var useCase = new LockOwnerProfileUseCase(repo);

        var lockTime = DateTime.UtcNow.AddDays(7);
        var result = await useCase.ExecuteAsync(5, new LockOwnerProfileRequest("Vi phạm chính sách", lockTime, 1));

        Assert.NotNull(result);
        Assert.Equal(5, result.OwnerProfileId);
        Assert.Equal(10, result.UserId);
        Assert.Equal("Bãi Xe Vincom", result.BusinessName);
        Assert.Equal("Nguyễn Văn Chủ Bãi", result.FullName);
        Assert.Equal("owner@test.com", result.Email);
        Assert.Equal("0901234567", result.PhoneNumber);
        Assert.True(result.IsLocked);
        Assert.Equal("Suspended", result.OwnerStatus);
        Assert.Equal("Active", result.UserStatus);

        Assert.True(profile.IsLocked);
        Assert.Equal(OwnerStatus.Suspended, profile.Status);
        Assert.Equal("Vi phạm chính sách", user.LockReason);
        Assert.Equal(lockTime, user.LockedUntilUtc);

        // Gọi lặp lại (idempotent) vẫn thành công
        var repeatResult = await useCase.ExecuteAsync(5, new LockOwnerProfileRequest("Vi phạm chính sách lần 2", null, 1));
        Assert.NotNull(repeatResult);
        Assert.True(repeatResult.IsLocked);
        Assert.Equal("Suspended", repeatResult.OwnerStatus);
        Assert.Null(user.LockedUntilUtc); // null = vô thời hạn
    }

    [Fact]
    public async Task Unlock_owner_restores_profile_and_clears_user_lock()
    {
        var user = new User
        {
            FullName = "Nguyễn Văn Chủ Bãi",
            Email = "owner@test.com",
            PhoneNumber = "0901234567",
            Status = UserStatus.Active,
            LockReason = "Vi phạm",
            LockedUntilUtc = DateTime.UtcNow.AddDays(7)
        };
        SetId(user, 10);

        var profile = new OwnerProfile
        {
            UserId = user.Id,
            BusinessName = "Bãi Xe Vincom",
            IsLocked = true,
            Status = OwnerStatus.Suspended,
            User = user
        };
        SetId(profile, 5);

        var repo = new FakeOwnerRepo(profile);
        var useCase = new UnlockOwnerProfileUseCase(repo);

        var result = await useCase.ExecuteAsync(5, new UnlockOwnerProfileRequest("Đã giải quyết khiếu nại", 1));

        Assert.NotNull(result);
        Assert.False(result.IsLocked);
        Assert.Equal("Active", result.OwnerStatus);
        Assert.Equal("Active", result.UserStatus);

        Assert.False(profile.IsLocked);
        Assert.Equal(OwnerStatus.Active, profile.Status);
        Assert.Null(user.LockReason);
        Assert.Null(user.LockedUntilUtc);

        // Gọi lặp lại vẫn thành công
        var repeatResult = await useCase.ExecuteAsync(5, new UnlockOwnerProfileRequest("Thử lại", 1));
        Assert.NotNull(repeatResult);
        Assert.False(repeatResult.IsLocked);
        Assert.Equal("Active", repeatResult.OwnerStatus);
    }

    [Fact]
    public async Task Lock_or_unlock_missing_owner_returns_null()
    {
        var repo = new FakeOwnerRepo(null);
        var lockUseCase = new LockOwnerProfileUseCase(repo);
        var unlockUseCase = new UnlockOwnerProfileUseCase(repo);

        var lockResult = await lockUseCase.ExecuteAsync(999, new LockOwnerProfileRequest("Lý do", null, 1));
        var unlockResult = await unlockUseCase.ExecuteAsync(999, new UnlockOwnerProfileRequest("Lý do", 1));

        Assert.Null(lockResult);
        Assert.Null(unlockResult);
    }

    private sealed class FakeOwnerRepo(OwnerProfile? profile) : IOwnerProfileRepository
    {
        public Task<OwnerProfile?> FindByIdWithUserAsync(int ownerProfileId, CancellationToken cancellationToken)
            => Task.FromResult(profile?.Id == ownerProfileId ? profile : null);

        public Task UpdateAsync(OwnerProfile ownerProfile, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
