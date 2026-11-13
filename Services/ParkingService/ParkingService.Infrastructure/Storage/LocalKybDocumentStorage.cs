using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Kyb;

namespace ParkingService.Infrastructure.Storage;

/// <summary>
/// Bộ lưu trữ tài liệu KYB trên ổ đĩa cục bộ (Local Disk Storage) (US-054).
/// Chuẩn hóa đường dẫn, sinh tên file ngẫu nhiên (GUID) và chống tấn công Path Traversal.
/// </summary>
public sealed class LocalKybDocumentStorage(
    IConfiguration config,
    IHostEnvironment environment,
    ILogger<LocalKybDocumentStorage> logger) : IKybDocumentStorage
{
    private string BaseDirectory => Path.GetFullPath(
        config["Security:KybDocumentPath"] ?? "App_Data/kyb-documents",
        environment.ContentRootPath);

    public async Task<string> SaveAsync(
        int lotId,
        KybDocumentType type,
        string originalFileName,
        string fileExtension,
        Stream input,
        CancellationToken ct = default)
    {
        var fileGuid = Guid.NewGuid().ToString("N");
        var prefix = type switch
        {
            KybDocumentType.BusinessLicense => "license_",
            KybDocumentType.FireSafetyCertificate => "pccc_",
            KybDocumentType.SitePhoto => "photo_",
            _ => "doc_"
        };

        var fileName = $"{prefix}{fileGuid}{fileExtension}";
        var lotDir = Path.Combine(BaseDirectory, lotId.ToString());
        Directory.CreateDirectory(lotDir);

        var fullPath = Path.Combine(lotDir, fileName);

        // Đảm bảo đường dẫn nằm trong lotDir (Chống Path Traversal)
        var fullNormalized = Path.GetFullPath(fullPath);
        var lotDirNormalized = Path.GetFullPath(lotDir);
        if (!fullNormalized.StartsWith(lotDirNormalized, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Đường dẫn lưu file không hợp lệ.");

        try
        {
            await using var fileStream = new FileStream(fullNormalized, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await input.CopyToAsync(fileStream, ct);
        }
        catch
        {
            if (File.Exists(fullNormalized))
                File.Delete(fullNormalized);
            throw;
        }

        return $"/api/v1/parking-lots/{lotId}/kyb/documents/{fileName}";
    }

    public async Task<KybDocumentFileContent?> ReadAsync(int lotId, string fileId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Contains("..") || fileId.Contains('/') || fileId.Contains('\\'))
            return null;

        var lotDir = Path.Combine(BaseDirectory, lotId.ToString());
        var fullPath = Path.GetFullPath(Path.Combine(lotDir, fileId));
        var lotDirNormalized = Path.GetFullPath(lotDir);

        if (!fullPath.StartsWith(lotDirNormalized, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!File.Exists(fullPath))
            return null;

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        return new KybDocumentFileContent(bytes, contentType, fileId);
    }

    public Task DeleteAsync(int lotId, string fileId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Contains("..") || fileId.Contains('/') || fileId.Contains('\\'))
            return Task.CompletedTask;

        var lotDir = Path.Combine(BaseDirectory, lotId.ToString());
        var fullPath = Path.GetFullPath(Path.Combine(lotDir, fileId));
        var lotDirNormalized = Path.GetFullPath(lotDir);

        if (!fullPath.StartsWith(lotDirNormalized, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Không thể xóa file tài liệu cũ {FileId} của bãi {LotId}.", fileId, lotId);
        }

        return Task.CompletedTask;
    }
}
