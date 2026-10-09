using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Auth;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Test;

public class LoginUseCaseTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(UserStatus.Banned)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Ineligible_account_cannot_receive_tokens(UserStatus status)
    {
        var repo = new Repository { User = new User { Status = status } };
        await Assert.ThrowsAsync<AuthenticationException>(() => Create(repo).ExecuteAsync(new("driver@example.com", "password")));
        Assert.Equal(0, repo.Saves);
        Assert.Empty(repo.User.RefreshTokens);
    }

    [Fact]
    public async Task Active_temporary_lock_blocks_login()
    {
        var repo = new Repository { User = new User { Status = UserStatus.Active, LockedUntilUtc = Now.AddMinutes(1) } };
        await Assert.ThrowsAsync<AuthenticationException>(() => Create(repo).ExecuteAsync(new("driver@example.com", "password")));
        Assert.Empty(repo.User.RefreshTokens);
    }

    [Fact]
    public async Task Successful_login_clears_expired_lock_and_issues_seven_day_refresh_token()
    {
        var repo = new Repository { User = new User { Status = UserStatus.Active, LockedUntilUtc = Now.AddMinutes(-1), FailedLoginCount = 4, LockReason = "expired" } };
        var result = await Create(repo).ExecuteAsync(new(" driver@example.com ", "password"));
        Assert.Equal("driver@example.com", repo.Identifier);
        Assert.Null(repo.User.LockedUntilUtc);
        Assert.Null(repo.User.LockReason);
        Assert.Equal(0, repo.User.FailedLoginCount);
        Assert.Equal(Now, repo.User.LastLoginAtUtc);
        Assert.Equal(Now.AddDays(7), Assert.Single(repo.User.RefreshTokens).ExpiresAtUtc);
        Assert.Equal(64, result.RefreshToken.Length);
        Assert.Equal(RefreshTokenCodec.Hash(result.RefreshToken), Assert.Single(repo.User.RefreshTokens).TokenHash);
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task Fifth_failed_password_starts_lock_and_resets_attempt_window()
    {
        var repo = new Repository { User = new User { Status = UserStatus.Active, FailedLoginCount = 4 } };
        await Assert.ThrowsAsync<AuthenticationException>(() => Create(repo).ExecuteAsync(new("driver@example.com", "wrong")));
        Assert.Equal(Now.AddMinutes(15), repo.User.LockedUntilUtc);
        Assert.Equal(0, repo.User.FailedLoginCount);
        Assert.Empty(repo.User.RefreshTokens);
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task Empty_identifier_is_rejected_before_repository_access()
    {
        var repo = new Repository();
        await Assert.ThrowsAsync<ValidationException>(() => Create(repo).ExecuteAsync(new(" ", "password")));
        Assert.Null(repo.Identifier);
    }

    private static LoginUseCase Create(Repository repo) => new(repo, new Tokens(), new Hasher(), new Clock());
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Repository : IAuthRepository
    {
        public User User { get; init; } = new() { Status = UserStatus.Active };
        public int Saves { get; private set; }
        public string? Identifier { get; private set; }
        public Task<User?> GetUserByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken)
        {
            Identifier = emailOrPhone;
            return Task.FromResult<User?>(User);
        }
        public Task UpdateUserAsync(User user, CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }
    private sealed class Tokens : IJwtTokenGenerator
    {
        public string GenerateToken(User user, IEnumerable<string> roles, string? sessionHash = null) => "access";
    }
    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool Verify(string password, string hash) => password == "password";
    }
}
