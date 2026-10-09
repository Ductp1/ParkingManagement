using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Domain.Entities;

namespace UserService.Application.Features.Identity;

public sealed record RegisterRequest(string FullName, string Contact, string Password, UserRoleType Role = UserRoleType.Driver, string? BusinessName = null);
public sealed record OtpRequest(string Contact, OtpPurpose Purpose = OtpPurpose.Register);
public sealed record VerifyOtpRequest(string Contact, string Code);

public interface IIdentityTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IIdentityStore
{
    Task<IIdentityTransaction> BeginAsync(CancellationToken ct);
    Task<User?> FindContactAsync(string contact, CancellationToken ct);
    Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct);
    Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct);
    Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct);
    Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct);
    Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct);
    void Add(object entity);
    void Event(IntegrationEvent integrationEvent);
    Task SaveAsync(CancellationToken ct);
}
public interface ISecretCipher
{
    string Encrypt(string value, string purpose);
    string Decrypt(string value, string purpose);
}
public interface IOtpDeliveryConfiguration
{
    void EnsureAvailable();
    void EnsureDestination(string contact) { }
}

public static partial class IdentityRules
{
    public static string Contact(string value)
    {
        var contact = value?.Trim() ?? "";
        if (contact.Length is < 3 or > 256) throw new ValidationException("Email/SĐT không hợp lệ.");
        if (contact.Contains('@'))
        {
            if (!MailAddress.TryCreate(contact, out var mail) || mail.Address != contact)
                throw new ValidationException("Email không hợp lệ.");
            return contact.ToLowerInvariant();
        }
        if (!Phone().IsMatch(contact)) throw new ValidationException("SĐT phải gồm 9–15 chữ số, có thể bắt đầu bằng +.");
        return contact;
    }
    public static void Password(string value)
    {
        if (value is null || value.Length < 8 || Encoding.UTF8.GetByteCount(value) > 72
            || !value.Any(char.IsLetter) || !value.Any(char.IsDigit))
            throw new ValidationException("Mật khẩu phải có chữ và số, ít nhất 8 ký tự, tối đa 72 byte UTF-8.");
    }
    public static string Name(string value, int max = 150)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 1 || name.Length > max) throw new ValidationException("Tên không hợp lệ.");
        return name;
    }
    public static bool OtpFormat(string? code) => code is { Length: 6 } && code.All(c => c is >= '0' and <= '9');
    public static void CanAuthenticate(User user, DateTime now, bool pendingAllowed = false)
    {
        if (user.IsDeleted || (user.Status != UserStatus.Active && !(pendingAllowed && user.Status == UserStatus.PendingVerification))
            || user.LockedUntilUtc > now) throw new AuthenticationException("Tài khoản không được phép thực hiện thao tác.");
    }
    [GeneratedRegex(@"^\+?[0-9]{9,15}$")] private static partial Regex Phone();
}
