using System.Security.Cryptography;
using System.Text;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Auth;

public sealed record RefreshTokenRequest(string RefreshToken);

// Token ngẫu nhiên 256 bit, không phải mật khẩu; hash cố định cho phép tra cứu bằng index.
public static class RefreshTokenCodec
{
    public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static void Validate(string? token)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new ValidationException("Refresh token không đúng định dạng.");
    }
}

public interface ISessionRepository
{
    Task<RefreshToken?> FindAsync(string hash, CancellationToken cancellationToken);
    // Phải kiểm tra lại token/tài khoản và thay token trong cùng transaction.
    Task<bool> TryRotateAsync(string hash, RefreshToken replacement, DateTime now, CancellationToken cancellationToken);
    // Thu hồi cả chuỗi thay thế, bao gồm token mới nếu client gửi token cũ.
    Task RevokeAsync(string hash, DateTime now, CancellationToken cancellationToken);
}

public interface IRefreshSessionUseCase
{
    Task<TokenResponseDto> ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}

public sealed class RefreshSessionUseCase(ISessionRepository repository, IJwtTokenGenerator jwt, TimeProvider clock) : IRefreshSessionUseCase
{
    public async Task<TokenResponseDto> ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        RefreshTokenCodec.Validate(request.RefreshToken);
        var hash = RefreshTokenCodec.Hash(request.RefreshToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var token = await repository.FindAsync(hash, cancellationToken);
        if (token is null) throw new AuthenticationException("Phiên đăng nhập không hợp lệ.");
        if (token.RevokedAtUtc is not null)
        {
            await repository.RevokeAsync(hash, now, cancellationToken);
            throw new AuthenticationException("Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.");
        }
        if (token.ExpiresAtUtc <= now || token.User.IsDeleted || token.User.Status != UserStatus.Active
            || token.User.LockedUntilUtc > now)
            throw new AuthenticationException("Phiên đã hết hạn hoặc tài khoản không được phép đăng nhập.");

        var raw = RefreshTokenCodec.Generate();
        var accessToken = jwt.GenerateToken(token.User, token.User.Roles.Select(r => r.Role.ToString()), RefreshTokenCodec.Hash(raw));
        var replacement = new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = RefreshTokenCodec.Hash(raw),
            // Giữ giới hạn 7 ngày của phiên gốc, tránh gia hạn vô hạn.
            ExpiresAtUtc = token.ExpiresAtUtc
        };
        if (!await repository.TryRotateAsync(hash, replacement, now, cancellationToken))
            throw new AuthenticationException("Phiên đã thay đổi. Vui lòng đăng nhập lại.");
        return new TokenResponseDto(accessToken, raw);
    }
}

public interface ILogoutUseCase
{
    Task ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}

public sealed class LogoutUseCase(ISessionRepository repository, TimeProvider clock) : ILogoutUseCase
{
    public Task ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        RefreshTokenCodec.Validate(request.RefreshToken);
        return repository.RevokeAsync(RefreshTokenCodec.Hash(request.RefreshToken), clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
