using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingManagement.ServiceDefaults.Security;

/// <summary>
/// T-802: xác thực service gọi endpoint nội bộ (service-to-service). Mỗi service một khóa trong
/// "InternalEvents:ApiKeys:{TênService}" (đặt qua biến môi trường, không commit), bằng đúng EventBus:ApiKey của service đó –
/// IOutboxTransport gửi khóa trong header "Authorization: Bearer {khóa}".
/// Trả tên service sở hữu khóa để use case ràng buộc SourceService. So khớp thời gian cố định (FixedTimeEquals trên SHA-256).
/// </summary>
public sealed class InternalApiKeyAuthenticator(IConfiguration configuration)
{
    public const string ApiKeysSection = "InternalEvents:ApiKeys";
    public const int MinKeyLength = 32;
    private const string BearerPrefix = "Bearer ";

    public string Authenticate(string? authorizationHeader)
    {
        var keys = configuration.GetSection(ApiKeysSection).GetChildren()
            .Where(k => !string.IsNullOrWhiteSpace(k.Value))
            .Select(k => (Service: k.Key, Key: k.Value!))
            .ToList();
        // Fail closed: chưa cấu hình (hoặc cấu hình sai) thì từ chối tất cả – 503 để dispatcher retry, không mất sự kiện.
        if (keys.Count == 0)
            throw new DependencyUnavailableException("Service nhận chưa cấu hình khóa nội bộ.");
        if (keys.Any(k => k.Key.Length < MinKeyLength) || keys.DistinctBy(k => k.Key, StringComparer.Ordinal).Count() != keys.Count)
            throw new DependencyUnavailableException($"Khóa nội bộ nhận sự kiện phải dài tối thiểu {MinKeyLength} ký tự và khác nhau giữa các service.");

        if (string.IsNullOrEmpty(authorizationHeader)
            || !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            || authorizationHeader.Length == BearerPrefix.Length)
            throw new AuthenticationException("Thiếu khóa nội bộ (Authorization: Bearer).");

        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(authorizationHeader[BearerPrefix.Length..].Trim()));
        string? caller = null;
        foreach (var (service, key) in keys)
            // Duyệt hết danh sách, không dừng sớm: thời gian không lộ khóa nào gần đúng.
            if (CryptographicOperations.FixedTimeEquals(presented, SHA256.HashData(Encoding.UTF8.GetBytes(key))))
                caller = service;

        return caller ?? throw new AuthenticationException("Khóa nội bộ không hợp lệ.");
    }

    /// <summary>Endpoint chỉ dành cho một số service: thiếu / sai khóa → 401, khóa của service khác → 403.</summary>
    public void Require(string? authorizationHeader, params string[] services)
    {
        if (!services.Contains(Authenticate(authorizationHeader)))
            throw new ForbiddenException("Service này không được gọi endpoint nội bộ đang yêu cầu.");
    }

    /// <summary>Endpoint nhận cả JWT người dùng lẫn khóa nội bộ: không ném lỗi khi header không phải khóa.</summary>
    public bool IsFrom(string? authorizationHeader, params string[] services)
    {
        try { return services.Contains(Authenticate(authorizationHeader)); }
        catch (Exception ex) when (ex is AuthenticationException or DependencyUnavailableException) { return false; }
    }
}

public static class InternalEventsServiceCollectionExtensions
{
    /// <summary>Đăng ký phần HTTP của endpoint nhận sự kiện nội bộ (dùng chung cho Program.cs và test).</summary>
    public static IServiceCollection AddInternalEventReceiver(this IServiceCollection services)
        => services.AddSingleton<InternalApiKeyAuthenticator>();
}
