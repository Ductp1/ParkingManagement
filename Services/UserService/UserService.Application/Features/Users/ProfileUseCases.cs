using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;

namespace UserService.Application.Features.Users;

// Contact, roles, tenant, status and KYC require separate verified workflows.
public sealed record UpdateProfileRequest(string FullName);

public sealed class ProfileUseCases(IIdentityStore store, ICurrentUser current, TimeProvider clock)
{
    public async Task UpdateAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
        var name = IdentityRules.Name(request.FullName);
        await using var tx = await store.BeginAsync(ct);
        var user = await store.FindUserAsync(id, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
        IdentityRules.CanAuthenticate(user, clock.GetUtcNow().UtcDateTime);
        user.FullName = name;
        await store.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }
}
