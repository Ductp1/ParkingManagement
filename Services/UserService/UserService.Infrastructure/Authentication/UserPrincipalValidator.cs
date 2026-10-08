using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Enums;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Authentication;

public sealed class UserPrincipalValidator(UserDbContext db, TimeProvider clock) : ITokenPrincipalValidator
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var sid = principal.FindFirstValue("sid");
        if (sid is null) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.Users.AsNoTracking().Include(u => u.Roles).Include(u => u.OwnerProfile).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || user.Status != UserStatus.Active || user.LockedUntilUtc > now) return false;
        if (!await db.RefreshTokens.AnyAsync(t => t.UserId == id && t.TokenHash == sid && t.RevokedAtUtc == null && t.ExpiresAtUtc > now, ct)) return false;
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(claim);
        foreach (var claim in identity.FindAll("OwnerProfileId").ToList()) identity.RemoveClaim(claim);
        foreach (var claim in identity.FindAll("ParkingLotId").ToList()) identity.RemoveClaim(claim);
        foreach (var role in user.Roles)
        {
            if (role.Role == UserRoleType.LotOwner && (user.OwnerProfile is null || user.OwnerProfile.IsLocked || user.OwnerProfile.Status != OwnerStatus.Active)) continue;
            if (role.Role == UserRoleType.Staff && !await db.StaffAssignments.AnyAsync(s => s.StaffUserId == id && s.IsActive
                && !s.OwnerProfile.IsLocked && s.OwnerProfile.Status == OwnerStatus.Active, ct)) continue;
            identity.AddClaim(new Claim(ClaimTypes.Role, role.Role.ToString()));
        }
        if (identity.HasClaim(ClaimTypes.Role, "LotOwner")) identity.AddClaim(new Claim("OwnerProfileId", user.OwnerProfile!.Id.ToString()));
        else if (identity.HasClaim(ClaimTypes.Role, "Staff"))
        {
            var tenant = await db.StaffAssignments.Where(s => s.StaffUserId == id && s.IsActive && !s.OwnerProfile.IsLocked
                && s.OwnerProfile.Status == OwnerStatus.Active).Select(s => s.OwnerProfileId).Distinct().ToListAsync(ct);
            if (tenant.Count != 1) return false;
            identity.AddClaim(new Claim("OwnerProfileId", tenant[0].ToString()));
            foreach (var lot in await db.StaffAssignments.Where(s => s.StaffUserId == id && s.IsActive
                && s.OwnerProfileId == tenant[0]).Select(s => s.ParkingLotId).Distinct().ToListAsync(ct))
                identity.AddClaim(new Claim("ParkingLotId", lot.ToString()));
        }
        return true;
    }
}
