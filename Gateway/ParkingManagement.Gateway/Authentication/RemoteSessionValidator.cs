using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace ParkingManagement.Gateway.Authentication;

// Gateway kiểm tra trạng thái phiên ở UserService sau khi JWT đã được kiểm tra chữ ký.
public sealed class RemoteSessionValidator(IHttpClientFactory clients, IConfiguration config, IHttpContextAccessor accessor,
    TimeProvider clock) : ITokenPrincipalValidator, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(10);
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    // Giới hạn số khóa và gộp các request đồng thời của cùng token.
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var url = config["Jwt:SessionValidationUrl"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            return false;
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        // Không lưu raw token; cùng user nhưng khác token không dùng chung kết quả.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri + "\n" + authorization));
        var key = Convert.ToHexString(hash);
        var gate = gates[hash[0] % gates.Length];
        State? state;
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out CachedState? cached) && cached is not null && cached.ExpiresAt > clock.GetUtcNow())
                state = cached.State;
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
                using var response = await clients.CreateClient("SessionValidation").SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return false;
                state = await response.Content.ReadFromJsonAsync<State>(cancellationToken: ct);
                if (state is null || state.Roles is null || state.UserId.ToString() != principal.FindFirstValue(ClaimTypes.NameIdentifier)) return false;
                // Hạn cố định, không gia hạn khi cache hit; không cache lỗi hoặc phiên bị thu hồi.
                cache.Set(key, new CachedState(state, clock.GetUtcNow().Add(Lifetime)),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime, Size = 1 });
            }
        }
        finally { gate.Release(); }
        if (state.UserId.ToString() != principal.FindFirstValue(ClaimTypes.NameIdentifier)) return false;
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(claim);
        foreach (var claim in identity.FindAll("OwnerProfileId").ToList()) identity.RemoveClaim(claim);
        foreach (var claim in identity.FindAll("ParkingLotId").ToList()) identity.RemoveClaim(claim);
        foreach (var role in state.Roles) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        if (state.OwnerProfileId is { } ownerId) identity.AddClaim(new Claim("OwnerProfileId", ownerId.ToString()));
        foreach (var lotId in state.ParkingLotIds ?? []) identity.AddClaim(new Claim("ParkingLotId", lotId.ToString()));
        return true;
    }
    private sealed record State(int UserId, string[] Roles, int? OwnerProfileId, int[]? ParkingLotIds);
    private sealed record CachedState(State State, DateTimeOffset ExpiresAt);
    public void Dispose()
    {
        cache.Dispose();
        foreach (var gate in gates) gate.Dispose();
    }
}

public static class RemoteSessionValidationExtensions
{
    public static IServiceCollection AddRemoteSessionValidation(this IServiceCollection services)
    {
        services.AddHttpClient("SessionValidation", client => client.Timeout = TimeSpan.FromSeconds(5));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenPrincipalValidator, RemoteSessionValidator>();
        return services;
    }
}
