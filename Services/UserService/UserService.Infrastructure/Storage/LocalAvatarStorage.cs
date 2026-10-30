using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Users;

namespace UserService.Infrastructure.Storage;

public sealed class LocalAvatarStorage(IConfiguration config, IHostEnvironment environment, ILogger<LocalAvatarStorage> logger) : IAvatarStorage
{
    private const string Prefix = "/api/v1/users/avatars/";
    private string Root => Path.GetFullPath(config["Security:AvatarPath"] ?? "App_Data/avatars", environment.ContentRootPath);
    public async Task<string> SaveAsync(Stream input, CancellationToken ct)
    {
        // Bound actual bytes too: do not rely on multipart's declared length or MIME type.
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > AvatarUseCases.MaxBytes) throw new ValidationException("Ảnh tối đa 2 MB.");
            await bytes.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        bytes.Position = 0;
        using var codec = SKCodec.Create(bytes);
        if (codec is null) throw new ValidationException("File không phải ảnh JPG, PNG hoặc WebP hợp lệ.");
        var info = codec.Info;
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)
            || info.Width is < 1 or > 4096 || info.Height is < 1 or > 4096)
            throw new ValidationException("Chỉ nhận ảnh JPG, PNG, WebP, kích thước tối đa 4096 × 4096 pixel.");
        using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ValidationException("Ảnh bị hỏng hoặc không thể giải mã.");
        var scale = Math.Min(1d, 512d / Math.Max(info.Width, info.Height));
        var size = new SKImageInfo(Math.Max(1, (int)Math.Round(info.Width * scale)), Math.Max(1, (int)Math.Round(info.Height * scale)));
        using var resized = decoded.Resize(size, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null) throw new ValidationException("Không thể xử lý ảnh.");
        // Re-encode pixels; never serve the original upload or original filename/metadata.
        using var normalized = resized.Encode(SKEncodedImageFormat.Png, 100);
        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, id + ".png");
        try { await File.WriteAllBytesAsync(path, normalized.ToArray(), ct); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return Prefix + id + ".png";
    }

    public Task DeleteAsync(string? url, CancellationToken ct)
    {
        if (url is null || !url.StartsWith(Prefix, StringComparison.Ordinal)) return Task.CompletedTask;
        var name = url[Prefix.Length..];
        if (name.Length != 36 || !name.EndsWith(".png", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[..32], "N", out _)) return Task.CompletedTask;
        try { File.Delete(Path.Combine(Root, name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { logger.LogWarning("Không thể dọn ảnh đại diện cũ {AvatarFile}.", name); }
        return Task.CompletedTask;
    }
    public async Task<byte[]?> ReadAsync(Guid id, CancellationToken ct)
    {
        try { return await File.ReadAllBytesAsync(Path.Combine(Root, id.ToString("N") + ".png"), ct); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
