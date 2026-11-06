using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;
using SkiaSharp;
using UserService.Infrastructure.Storage;

namespace UserService.Test;

public sealed class KycStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pm-kyc-test-" + Guid.NewGuid().ToString("N"));
    private readonly string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private EncryptedKycDocumentStorage Storage(string? encryptionKey = null) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = encryptionKey ?? key }).Build(),
        new Host(root), NullLogger<EncryptedKycDocumentStorage>.Instance);

    private static MemoryStream Image(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(encoded.ToArray());
    }

    [Fact]
    public async Task Image_is_reencoded_encrypted_at_rest_and_round_trips()
    {
        using var input = Image(3000, 1890);
        var stored = await Storage().SaveAsync(input, default);
        Assert.StartsWith("kyc:", stored);
        var file = Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        var raw = await File.ReadAllBytesAsync(file);
        Assert.NotEqual(new byte[] { 0xFF, 0xD8 }, raw[..2]); // không phải JPEG thô trên đĩa

        var bytes = await Storage().ReadAsync(stored, default);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes![..2]);
        using var decoded = SKBitmap.Decode(bytes);
        Assert.Equal(2000, decoded.Width);

        await Storage().DeleteAsync(stored, default);
        Assert.Null(await Storage().ReadAsync(stored, default));
    }

    [Fact]
    public async Task Wrong_key_cannot_decrypt()
    {
        using var input = Image(800, 500);
        var stored = await Storage().SaveAsync(input, default);
        var other = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await Assert.ThrowsAnyAsync<CryptographicException>(() => Storage(other).ReadAsync(stored, default));
    }

    [Fact]
    public async Task Tiny_or_non_images_are_rejected()
    {
        using var tiny = Image(100, 60);
        await Assert.ThrowsAsync<ValidationException>(() => Storage().SaveAsync(tiny, default));
        using var text = new MemoryStream("not an image"u8.ToArray());
        await Assert.ThrowsAsync<ValidationException>(() => Storage().SaveAsync(text, default));
    }

    [Fact]
    public async Task Keys_outside_format_are_ignored()
    {
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "App_Data", "keep.bin"); Directory.CreateDirectory(Path.Combine(root, "App_Data", "kyc"));
        await File.WriteAllTextAsync(sentinel, "keep");
        await Storage().DeleteAsync("kyc:../keep", default);
        await Storage().DeleteAsync("/api/v1/users/avatars/keep.bin", default);
        Assert.Null(await Storage().ReadAsync("kyc:../../etc/passwd", default));
        Assert.True(File.Exists(sentinel));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class Host(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "KycTest";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
