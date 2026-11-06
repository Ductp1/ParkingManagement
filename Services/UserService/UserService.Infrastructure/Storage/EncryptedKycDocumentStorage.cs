using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Kyc;

namespace UserService.Infrastructure.Storage;

/// <summary>
/// Ảnh CCCD/GPLX: giải mã + mã hóa lại thành JPEG (bỏ EXIF/GPS), sau đó mã hóa AES-256-GCM trước khi ghi đĩa.
/// Khóa lưu dạng "kyc:{guid}" — không phải URL, chỉ đọc được qua API có kiểm tra quyền.
/// </summary>
public sealed class EncryptedKycDocumentStorage(IConfiguration config, IHostEnvironment environment,
    ILogger<EncryptedKycDocumentStorage> logger) : IKycDocumentStorage
{
    private const string Prefix = "kyc:";
    private const int MaxSide = 2000;
    private static readonly byte[] Aad = Encoding.UTF8.GetBytes("kyc-image");
    private string Root => Path.GetFullPath(config["Security:KycPath"] ?? "App_Data/kyc", environment.ContentRootPath);

    public async Task<string> SaveAsync(Stream input, CancellationToken ct)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > KycUseCases.MaxImageBytes) throw new ValidationException("Ảnh giấy tờ tối đa 5 MB.");
            await bytes.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        bytes.Position = 0;
        using var codec = SKCodec.Create(bytes);
        if (codec is null) throw new ValidationException("File không phải ảnh JPG, PNG hoặc WebP hợp lệ.");
        var info = codec.Info;
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)
            || info.Width is < 300 or > 8000 || info.Height is < 200 or > 8000)
            throw new ValidationException("Ảnh giấy tờ phải là JPG, PNG hoặc WebP, tối thiểu 300 × 200 pixel để đọc rõ chữ.");
        using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ValidationException("Ảnh bị hỏng hoặc không thể giải mã.");
        var scale = Math.Min(1d, (double)MaxSide / Math.Max(info.Width, info.Height));
        var size = new SKImageInfo(Math.Max(1, (int)Math.Round(info.Width * scale)), Math.Max(1, (int)Math.Round(info.Height * scale)));
        using var resized = decoded.Resize(size, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null) throw new ValidationException("Không thể xử lý ảnh.");
        using var normalized = resized.Encode(SKEncodedImageFormat.Jpeg, 88);

        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, id + ".bin");
        try { await File.WriteAllBytesAsync(path, Encrypt(normalized.ToArray()), ct); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return Prefix + id;
    }

    public async Task<byte[]?> ReadAsync(string? key, CancellationToken ct)
    {
        if (Name(key) is not { } name) return null;
        try { return Decrypt(await File.ReadAllBytesAsync(Path.Combine(Root, name), ct)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public Task DeleteAsync(string? key, CancellationToken ct)
    {
        if (Name(key) is not { } name) return Task.CompletedTask;
        try { File.Delete(Path.Combine(Root, name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { logger.LogWarning("Không thể xóa ảnh giấy tờ {KycFile}.", name); }
        return Task.CompletedTask;
    }

    /// <summary>Chỉ chấp nhận khóa đúng định dạng: chặn path traversal.</summary>
    private static string? Name(string? key)
        => key is { Length: 36 } && key.StartsWith(Prefix, StringComparison.Ordinal) && Guid.TryParseExact(key[Prefix.Length..], "N", out _)
            ? key[Prefix.Length..] + ".bin" : null;

    private byte[] Key()
    {
        var encoded = config["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encoded)) throw new DependencyUnavailableException("Chưa cấu hình Security:EncryptionKey (AES-256, Base64).");
        try { var key = Convert.FromBase64String(encoded); if (key.Length == 32) return key; }
        catch (FormatException) { }
        throw new DependencyUnavailableException("Khóa mã hóa phải là 32 byte Base64.");
    }

    private byte[] Encrypt(byte[] plain)
    {
        var output = new byte[12 + 16 + plain.Length];
        var nonce = output.AsSpan(0, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Key(), 16);
        aes.Encrypt(nonce, plain, output.AsSpan(28), output.AsSpan(12, 16), Aad);
        return output;
    }

    private byte[] Decrypt(byte[] data)
    {
        if (data.Length < 28) throw new CryptographicException("Tệp ảnh giấy tờ không hợp lệ.");
        var plain = new byte[data.Length - 28];
        using var aes = new AesGcm(Key(), 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain, Aad);
        return plain;
    }
}
