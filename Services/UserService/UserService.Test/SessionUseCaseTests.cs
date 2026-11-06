using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Auth;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Test;

public class SessionUseCaseTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
    private const string Raw = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task Refresh_rotates_token_preserves_expiry_and_uses_current_roles_and_tenant()
    {
        var repo = new Sessions();
        repo.Token.User.Roles.Add(new UserRole { Role = UserRoleType.LotOwner });
        repo.Token.User.OwnerProfile = new OwnerProfile();
        var jwt = new Jwt();
        var result = await Refresh(repo, jwt).ExecuteAsync(new(Raw));
        Assert.Equal("access", result.AccessToken);
        Assert.NotEqual(Raw, result.RefreshToken);
        Assert.Equal(64, result.RefreshToken.Length);
        Assert.Equal(RefreshTokenCodec.Hash(result.RefreshToken), repo.Replacement!.TokenHash);
        Assert.Equal(repo.Token.ExpiresAtUtc, repo.Replacement.ExpiresAtUtc);
        Assert.Equal(Now, repo.Token.RevokedAtUtc);
        Assert.Equal(repo.Replacement.TokenHash, repo.Token.ReplacedByTokenHash);
        Assert.Contains("LotOwner", jwt.Roles);
        Assert.Same(repo.Token.User.OwnerProfile, jwt.User!.OwnerProfile);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Expired_token_cannot_refresh(int seconds)
    {
        var repo = new Sessions();
        repo.Token.ExpiresAtUtc = Now.AddSeconds(seconds);
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
        Assert.Null(repo.Replacement);
    }

    [Theory]
    [InlineData(UserStatus.Banned)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Ineligible_user_cannot_refresh(UserStatus status)
    {
        var repo = new Sessions();
        repo.Token.User.Status = status;
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
        Assert.Null(repo.Replacement);
    }

    [Fact]
    public async Task Deleted_user_cannot_refresh()
    {
        var repo = new Sessions();
        repo.Token.User.IsDeleted = true;
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
    }

    [Fact]
    public async Task Temporarily_locked_user_cannot_refresh()
    {
        var repo = new Sessions();
        repo.Token.User.LockedUntilUtc = Now.AddMinutes(1);
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
    }

    [Fact]
    public async Task Unknown_token_is_unauthorized()
    {
        var repo = new Sessions();
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(new string('B', 64))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public async Task Malformed_token_is_rejected_before_lookup(string raw)
    {
        var repo = new Sessions();
        await Assert.ThrowsAsync<ValidationException>(() => Refresh(repo).ExecuteAsync(new(raw)));
        Assert.Equal(0, repo.Lookups);
    }

    [Fact]
    public async Task Reusing_old_token_revokes_replacement()
    {
        var repo = new Sessions();
        await Refresh(repo).ExecuteAsync(new(Raw));
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
        Assert.Equal(Now, repo.Replacement!.RevokedAtUtc);
    }

    [Fact]
    public async Task Lost_rotation_race_does_not_return_tokens()
    {
        var repo = new Sessions { RejectRotation = true };
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
        Assert.Null(repo.Replacement);
    }

    [Fact]
    public async Task Logout_old_token_revokes_rotated_session_and_is_repeatable()
    {
        var repo = new Sessions();
        await Refresh(repo).ExecuteAsync(new(Raw));
        var logout = new LogoutUseCase(repo, new Clock());
        await logout.ExecuteAsync(new(Raw));
        await logout.ExecuteAsync(new(Raw));
        Assert.Equal(Now, repo.Token.RevokedAtUtc);
        Assert.Equal(Now, repo.Replacement!.RevokedAtUtc);
        await Assert.ThrowsAsync<AuthenticationException>(() => Refresh(repo).ExecuteAsync(new(Raw)));
    }

    [Fact]
    public async Task Logout_unknown_token_completes_without_disclosing_session()
        => await new LogoutUseCase(new Sessions(), new Clock()).ExecuteAsync(new(new string('B', 64)));

    [Fact]
    public async Task Logout_malformed_token_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => new LogoutUseCase(new Sessions(), new Clock()).ExecuteAsync(new("bad")));

    private static RefreshSessionUseCase Refresh(Sessions repo, Jwt? jwt = null) => new(repo, jwt ?? new Jwt(), new Clock());
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Jwt : IJwtTokenGenerator
    {
        public User? User { get; private set; }
        public string[] Roles { get; private set; } = [];
        public string GenerateToken(User user, IEnumerable<string> roles, string? sessionHash = null)
        {
            User = user;
            Roles = roles.ToArray();
            return "access";
        }
    }
    private sealed class Sessions : ISessionRepository
    {
        public RefreshToken Token { get; } = new()
        {
            TokenHash = RefreshTokenCodec.Hash(Raw), ExpiresAtUtc = Now.AddDays(7),
            User = new User { Status = UserStatus.Active }
        };
        public RefreshToken? Replacement { get; private set; }
        public bool RejectRotation { get; init; }
        public int Lookups { get; private set; }
        public Task<RefreshToken?> FindAsync(string hash, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(hash == Token.TokenHash ? Token : Replacement?.TokenHash == hash ? Replacement : null);
        }
        public Task<bool> TryRotateAsync(string hash, RefreshToken replacement, DateTime now, CancellationToken cancellationToken)
        {
            if (RejectRotation || hash != Token.TokenHash || Token.RevokedAtUtc is not null) return Task.FromResult(false);
            Token.RevokedAtUtc = now;
            Token.ReplacedByTokenHash = replacement.TokenHash;
            replacement.User = Token.User;
            Replacement = replacement;
            return Task.FromResult(true);
        }
        public Task RevokeAsync(string hash, DateTime now, CancellationToken cancellationToken)
        {
            if (hash == Token.TokenHash)
            {
                Token.RevokedAtUtc ??= now;
                if (Replacement is not null) Replacement.RevokedAtUtc ??= now;
            }
            else if (hash == Replacement?.TokenHash) Replacement.RevokedAtUtc ??= now;
            return Task.CompletedTask;
        }
    }
}
