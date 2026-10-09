using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ParkingManagement.SharedKernel.Contracts;

namespace ParkingManagement.ServiceDefaults;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public IReadOnlyList<int> ParkingLotIds => User?.FindAll("ParkingLotId")
        .Select(c => int.TryParse(c.Value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList() ?? [];

    public int? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles => 
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();

    public int? OwnerProfileId
    {
        get
        {
            var value = User?.FindFirstValue("OwnerProfileId");
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
