using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Application.Features.Auth;

// ===== DTO =====
public sealed record LoginDto(string EmailOrPhone, string Password);
public sealed record TokenResponseDto(string AccessToken, string RefreshToken);

// ===== PORT (Infrastructure cài đặt bằng EF Core) =====
public interface IAuthRepository
{
    Task<User?> GetUserByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface ILoginUseCase
{
    Task<TokenResponseDto> ExecuteAsync(LoginDto request, CancellationToken cancellationToken = default);
}

public sealed class LoginUseCase(IAuthRepository repository, IJwtTokenGenerator jwtTokenGenerator, IPasswordHasher passwordHasher, TimeProvider timeProvider) : ILoginUseCase
{
    public async Task<TokenResponseDto> ExecuteAsync(LoginDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrPhone) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Tài khoản và mật khẩu không được rỗng.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var identifier = request.EmailOrPhone.Trim();
        if (identifier.Contains('@')) identifier = identifier.ToLowerInvariant();
        if (System.Text.Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new ValidationException("Mật khẩu vượt giới hạn 72 byte UTF-8.");
        var user = await repository.GetUserByEmailOrPhoneAsync(identifier, cancellationToken)
            ?? throw new AuthenticationException("Tài khoản hoặc mật khẩu không chính xác.");

        if (user.IsDeleted || user.Status != UserStatus.Active)
            throw new AuthenticationException("Tài khoản không được phép đăng nhập.");

        // Kiểm tra khóa tài khoản
        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > now)
        {
            throw new AuthenticationException($"Tài khoản bị khóa đến {user.LockedUntilUtc.Value:O}.");
        }

        // Kiểm tra mật khẩu (thông qua Port, không phụ thuộc thư viện ngoài)
        bool isPasswordValid = passwordHasher.Verify(request.Password, user.PasswordHash);
        
        if (!isPasswordValid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5) // Khóa 15p nếu sai 5 lần (theo board, OTP sai 3 lần, Pass sai có thể set 5)
            {
                user.LockedUntilUtc = now.AddMinutes(15);
                user.FailedLoginCount = 0;
                user.LockReason = "Nhập sai mật khẩu quá 5 lần.";
            }
            await repository.UpdateUserAsync(user, cancellationToken);
            throw new AuthenticationException("Tài khoản hoặc mật khẩu không chính xác.");
        }

        // Đăng nhập thành công -> Reset bộ đếm
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LockReason = null;
        user.LastLoginAtUtc = now;

        var roles = user.Roles.Select(r => r.Role.ToString()).ToList();

        // Sinh JWT
        // Sinh Refresh Token
        var refreshToken = RefreshTokenCodec.Generate();
        var accessToken = jwtTokenGenerator.GenerateToken(user, roles, RefreshTokenCodec.Hash(refreshToken));
        user.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = RefreshTokenCodec.Hash(refreshToken),
            ExpiresAtUtc = now.AddDays(7)
        });

        await repository.UpdateUserAsync(user, cancellationToken);

        return new TokenResponseDto(accessToken, refreshToken);
    }
}
