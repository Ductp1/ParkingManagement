using System.Security.Claims;

namespace ParkingManagement.ServiceDefaults;

public interface ITokenPrincipalValidator
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct);
}
