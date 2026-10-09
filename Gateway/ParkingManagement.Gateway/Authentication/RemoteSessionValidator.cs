using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;

namespace ParkingManagement.Gateway.Authentication;

// Gateway kiểm tra trạng thái phiên ở UserService sau khi JWT đã được kiểm tra chữ ký.
public sealed class RemoteSessionValidator(IHttpClientFactory clients, IConfiguration config, IHttpContextAccessor accessor) : ITokenPrincipalValidator
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var url = config["Jwt:SessionValidationUrl"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Authorization", accessor.HttpContext?.Request.Headers.Authorization.ToString());
        using var response = await clients.CreateClient("SessionValidation").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return false;
        var state = await response.Content.ReadFromJsonAsync<State>(cancellationToken: ct);
        if (state is null || state.UserId.ToString() != principal.FindFirstValue(ClaimTypes.NameIdentifier)) return false;
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
}

public static class RemoteSessionValidationExtensions
{
    public static IServiceCollection AddRemoteSessionValidation(this IServiceCollection services)
    {
        services.AddHttpClient("SessionValidation", client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<ITokenPrincipalValidator, RemoteSessionValidator>();
        return services;
    }
}
