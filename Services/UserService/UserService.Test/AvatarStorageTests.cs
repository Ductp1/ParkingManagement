using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;
using SkiaSharp;
using UserService.Infrastructure.Storage;
using UserService.Application.Features.Users;

namespace UserService.Test;

public sealed class AvatarStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pm-avatar-test-" + Guid.NewGuid().ToString("N"));
    private LocalAvatarStorage Storage => new(new ConfigurationBuilder().Build(), new Host(root), NullLogger<LocalAvatarStorage>.Instance);
    [Fact]
    public async Task Real_image_is_resized_reencoded_and_named_by_random_id()
    {
        using var original = new SKBitmap(1000, 500);
        using var encoded = original.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = new MemoryStream(encoded.ToArray());
        var url = await Storage.SaveAsync(stream, default);
        var id = Guid.ParseExact(Path.GetFileNameWithoutExtension(url), "N");
        var bytes = await Storage.ReadAsync(id, default);
        using var result = SKBitmap.Decode(bytes!);
        Assert.Equal(512, result.Width); Assert.Equal(256, result.Height);
        Assert.Equal(new byte[] {137,80,78,71,13,10,26,10}, bytes![..8]);
        await Storage.DeleteAsync(url, default);
        Assert.Null(await Storage.ReadAsync(id, default));
    }
    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' />")]
    [InlineData("not a png")]
    [InlineData("")]
    public async Task Non_images_are_rejected(string value)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
        await Assert.ThrowsAsync<ValidationException>(() => Storage.SaveAsync(stream, default));
    }
    [Fact]
    public async Task Oversized_actual_stream_is_rejected()
    {
        using var stream = new MemoryStream(new byte[AvatarUseCases.MaxBytes + 1]);
        await Assert.ThrowsAsync<ValidationException>(() => Storage.SaveAsync(stream, default));
    }
    [Fact]
    public async Task Oversized_dimensions_are_rejected_before_decode()
    {
        using var image = new SKBitmap(4097, 1);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(encoded.ToArray());
        await Assert.ThrowsAsync<ValidationException>(() => Storage.SaveAsync(stream, default));
    }
    [Fact]
    public async Task Cleanup_never_follows_external_urls_or_traversal()
    {
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "keep.png"); await File.WriteAllTextAsync(sentinel, "keep");
        await Storage.DeleteAsync("/api/v1/users/avatars/../../keep.png", default);
        await Storage.DeleteAsync("https://external.test/keep.png", default);
        Assert.True(File.Exists(sentinel));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed class Host(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "AvatarTest";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
