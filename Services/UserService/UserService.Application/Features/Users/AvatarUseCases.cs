using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;

namespace UserService.Application.Features.Users;

public interface IAvatarStorage
{
    Task<string> SaveAsync(Stream image, CancellationToken ct);
    Task DeleteAsync(string? url, CancellationToken ct);
    Task<byte[]?> ReadAsync(Guid id, CancellationToken ct);
}
public sealed record AvatarResponse(string? AvatarUrl);

public sealed class AvatarUseCases(IIdentityStore store, ICurrentUser current, IAvatarStorage storage, TimeProvider clock)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public async Task<AvatarResponse> UploadAsync(Stream image, long length, CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
        if (length <= 0 || length > MaxBytes) throw new ValidationException("Ảnh phải có dung lượng từ 1 byte đến 2 MB.");
        var url = await storage.SaveAsync(image, ct);
        string? old;
        try
        {
            await using var tx = await store.BeginAsync(ct);
            var user = await store.FindUserAsync(id, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
            IdentityRules.CanAuthenticate(user, clock.GetUtcNow().UtcDateTime);
            old = user.AvatarUrl;
            user.AvatarUrl = url;
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch { await storage.DeleteAsync(url, CancellationToken.None); throw; }
        await storage.DeleteAsync(old, CancellationToken.None);
        return new(url);
    }
    public async Task RemoveAsync(CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
        string? old;
        await using (var tx = await store.BeginAsync(ct))
        {
            var user = await store.FindUserAsync(id, true, ct) ?? throw new AuthenticationException("Tài khoản không hợp lệ.");
            IdentityRules.CanAuthenticate(user, clock.GetUtcNow().UtcDateTime);
            old = user.AvatarUrl;
            user.AvatarUrl = null;
            await store.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        await storage.DeleteAsync(old, CancellationToken.None);
    }
}
