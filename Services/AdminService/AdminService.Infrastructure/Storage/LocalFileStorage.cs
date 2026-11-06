using System.Text.RegularExpressions;
using AdminService.Application.Features.PartnerOnboarding;

namespace AdminService.Infrastructure.Storage;

/// <summary>
/// US-116 (dev): lưu file trong thư mục local (mặc định App_Data/files – đã ignore trong git). Khóa chỉ gồm chữ thường, số,
/// '-', '.', '/' và không được thoát ra ngoài thư mục gốc. Ghi qua file tạm rồi đổi tên để không để lại file dở dang.
/// </summary>
public sealed partial class LocalFileStorage(string rootPath) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, content, cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (!KeyPattern().IsMatch(key) || key.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException($"Khóa file không hợp lệ: {key}", nameof(key));
        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Khóa file không hợp lệ: {key}", nameof(key));
        return path;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9./-]{0,299}$")]
    private static partial Regex KeyPattern();
}
