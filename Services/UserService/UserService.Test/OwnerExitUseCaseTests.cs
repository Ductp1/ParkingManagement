using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Users;
using UserService.Domain.Entities;
using Xunit;

namespace UserService.Test;

public class OwnerExitUseCaseTests
{
    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static void SetId(BaseEntity entity, int id)
        => typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);

    private static OwnerProfile CreateActiveOwner(int profileId = 1, int userId = 10)
    {
        var user = new User
        {
            FullName = "Nguyễn Văn Chủ Bãi",
            Email = "owner@test.com",
            PhoneNumber = "0901234567",
            Status = UserStatus.Active
        };
        SetId(user, userId);

        var profile = new OwnerProfile
        {
            UserId = user.Id,
            BusinessName = "Bãi Xe Vincom",
            IsLocked = false,
            Status = OwnerStatus.Active,
            User = user
        };
        SetId(profile, profileId);

        var staff = new StaffAssignment
        {
            StaffUserId = 20,
            OwnerProfileId = profileId,
            ParkingLotId = 100,
            IsActive = true
        };
        SetId(staff, 1);
        profile.StaffAssignments.Add(staff);

        return profile;
    }

    [Fact]
    public async Task RequestExit_WhenOwnerActive_ShouldSetLeaving_AndCalculate30DaysDeadline()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new RequestOwnerExitUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id, new RequestOwnerExitRequest("Hết hạn thuê mặt bằng"));

        Assert.NotNull(result);
        Assert.Equal("Leaving", result.Status);
        Assert.Equal("Hết hạn thuê mặt bằng", result.ExitReason);
        Assert.Equal(now.UtcDateTime, result.ExitRequestedAtUtc);
        Assert.Equal(now.UtcDateTime.AddDays(30), result.ExitEffectiveAtUtc);
        Assert.Equal(30, result.RemainingDays);
        Assert.True(result.CanCancel);

        Assert.Equal(OwnerStatus.Leaving, profile.Status);
        Assert.Equal(now.UtcDateTime.AddDays(30), profile.ExitEffectiveAtUtc);
    }

    [Fact]
    public async Task RequestExit_WithCustomNoticeDays_ShouldCalculateCorrectDeadline()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new RequestOwnerExitUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id, new RequestOwnerExitRequest("Chuyển đổi kinh doanh", NoticeDays: 45));

        Assert.Equal(45, result.RemainingDays);
        Assert.Equal(now.UtcDateTime.AddDays(45), result.ExitEffectiveAtUtc);
    }

    [Fact]
    public async Task RequestExit_WhenOwnerNotActive_ShouldThrowConflictException()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var profile = CreateActiveOwner();
        profile.Status = OwnerStatus.Suspended;
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new RequestOwnerExitUseCase(repo, timeProvider);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(profile.Id, new RequestOwnerExitRequest("Hết hợp đồng")));
    }

    [Fact]
    public async Task RequestExit_WithEmptyReason_ShouldThrowValidationException()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var profile = CreateActiveOwner();
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new RequestOwnerExitUseCase(repo, timeProvider);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(profile.Id, new RequestOwnerExitRequest("   ")));
    }

    [Fact]
    public async Task RequestExit_WhenOwnerNotFound_ShouldThrowNotFoundException()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var repo = new FakeExitOwnerRepo(null);

        var useCase = new RequestOwnerExitUseCase(repo, timeProvider);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(999, new RequestOwnerExitRequest("Hết hợp đồng")));
    }

    [Fact]
    public async Task CancelExit_WhenOwnerLeavingBeforeDeadline_ShouldRestoreActive()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        profile.RequestExit("Tạm dừng", 30, now.UtcDateTime.AddDays(-5));
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new CancelOwnerExitUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id, new CancelOwnerExitRequest("Đã thương lượng lại mặt bằng"));

        Assert.Equal("Active", result.Status);
        Assert.Null(result.ExitRequestedAtUtc);
        Assert.Null(result.ExitEffectiveAtUtc);
        Assert.Null(result.ExitReason);
        Assert.False(result.CanCancel);

        Assert.Equal(OwnerStatus.Active, profile.Status);
    }

    [Fact]
    public async Task CancelExit_WhenOwnerNotLeaving_ShouldThrowConflictException()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var profile = CreateActiveOwner();
        profile.Status = OwnerStatus.Active;
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new CancelOwnerExitUseCase(repo, timeProvider);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(profile.Id));
    }

    [Fact]
    public async Task CancelExit_WhenPastEffectiveDeadline_ShouldThrowConflictException()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        profile.RequestExit("Nghỉ", 30, now.UtcDateTime.AddDays(-31)); // Đã quá hạn 30 ngày
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new CancelOwnerExitUseCase(repo, timeProvider);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(profile.Id));
    }

    [Fact]
    public async Task Terminate_ShouldSetTerminated_LockAccount_AndRevokeStaffAssignments()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        profile.RequestExit("Nghỉ", 30, now.UtcDateTime.AddDays(-30));
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new TerminateOwnerExitUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id);

        Assert.Equal("Terminated", result.Status);
        Assert.Equal(0, result.RemainingDays);
        Assert.False(result.CanCancel);

        Assert.Equal(OwnerStatus.Terminated, profile.Status);
        Assert.True(profile.IsLocked);
        Assert.All(profile.StaffAssignments, s =>
        {
            Assert.False(s.IsActive);
            Assert.Equal(now.UtcDateTime, s.RevokedAtUtc);
        });
    }

    [Fact]
    public async Task GetExitStatus_WhenLeaving_ShouldReturnRemainingDaysAndCanCancelTrue()
    {
        var now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var profile = CreateActiveOwner();
        profile.RequestExit("Nghỉ kinh doanh", 30, now.UtcDateTime.AddDays(-10));
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new GetOwnerExitStatusUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id);

        Assert.Equal("Leaving", result.Status);
        Assert.Equal(20, result.RemainingDays);
        Assert.True(result.CanCancel);
        Assert.Equal("Nghỉ kinh doanh", result.ExitReason);
    }

    [Fact]
    public async Task GetExitStatus_WhenActive_ShouldReturnRemainingDaysNullAndCanCancelFalse()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var profile = CreateActiveOwner();
        var repo = new FakeExitOwnerRepo(profile);

        var useCase = new GetOwnerExitStatusUseCase(repo, timeProvider);

        var result = await useCase.ExecuteAsync(profile.Id);

        Assert.Equal("Active", result.Status);
        Assert.Null(result.RemainingDays);
        Assert.False(result.CanCancel);
    }

    private sealed class FakeExitOwnerRepo(OwnerProfile? profile) : IOwnerProfileRepository
    {
        public Task<OwnerProfile?> FindByIdWithUserAsync(int ownerProfileId, CancellationToken cancellationToken)
            => Task.FromResult(profile?.Id == ownerProfileId ? profile : null);

        public Task<OwnerProfile?> FindByIdWithStaffAssignmentsAsync(int ownerProfileId, CancellationToken cancellationToken)
            => Task.FromResult(profile?.Id == ownerProfileId ? profile : null);

        public Task<OwnerProfile?> FindByUserIdAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(profile?.UserId == userId ? profile : null);

        public Task UpdateAsync(OwnerProfile ownerProfile, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
