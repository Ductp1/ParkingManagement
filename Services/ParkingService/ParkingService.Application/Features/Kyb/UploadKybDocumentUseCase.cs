using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Kyb;

public interface IUploadKybDocumentUseCase
{
    Task<UploadKybDocumentResultDto> ExecuteAsync(
        int lotId,
        KybDocumentType documentType,
        string originalFileName,
        string contentType,
        long length,
        Stream stream,
        int? ownerProfileId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tải lên file tài liệu thật (ĐKKD, PCCC, Ảnh hiện trường) cho hồ sơ thẩm định KYB (US-054).
/// Kiểm duyệt kích thước, Magic Bytes định dạng và quyền sở hữu của chủ bãi.
/// </summary>
public sealed class UploadKybDocumentUseCase(
    IParkingLotManagementRepository lotRepository,
    IKybDocumentStorage storage) : IUploadKybDocumentUseCase
{
    public const long MaxDocumentBytes = 10 * 1024 * 1024; // 10 MB
    public const long MaxPhotoBytes = 5 * 1024 * 1024;      // 5 MB

    public async Task<UploadKybDocumentResultDto> ExecuteAsync(
        int lotId,
        KybDocumentType documentType,
        string originalFileName,
        string contentType,
        long length,
        Stream stream,
        int? ownerProfileId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (stream is null || length <= 0)
            throw new ValidationException("File tài liệu không được để trống.");

        var lot = await lotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", lotId);

        if (!isAdmin)
        {
            if (ownerProfileId is null or <= 0)
                throw new ValidationException("OwnerProfileId không hợp lệ.");

            if (lot.OwnerProfileId != ownerProfileId.Value)
                throw new ConflictException("Bạn không có quyền quản lý tài liệu của bãi đỗ này.");
        }

        // 1. Kiểm tra kích thước tối đa theo loại tài liệu
        long maxBytes = documentType is KybDocumentType.SitePhoto ? MaxPhotoBytes : MaxDocumentBytes;
        if (length > maxBytes)
        {
            var maxMb = maxBytes / (1024 * 1024);
            throw new ValidationException($"Dung lượng file vượt quá mức cho phép (tối đa {maxMb} MB).");
        }

        // 2. Đọc header để xác thực Magic Bytes
        using var memoryStream = new MemoryStream();
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoryStream.Length + bytesRead > maxBytes)
            {
                var maxMb = maxBytes / (1024 * 1024);
                throw new ValidationException($"Dung lượng file thực tế vượt quá mức cho phép (tối đa {maxMb} MB).");
            }
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        memoryStream.Position = 0;
        var header = new byte[Math.Min(16, (int)memoryStream.Length)];
        _ = await memoryStream.ReadAsync(header, cancellationToken);
        memoryStream.Position = 0;

        var (normalizedExt, detectedMimeType) = ValidateAndDetectFormat(documentType, originalFileName, header);

        // 3. Lưu file vào bộ lưu trữ
        var documentUrl = await storage.SaveAsync(
            lotId,
            documentType,
            originalFileName,
            normalizedExt,
            memoryStream,
            cancellationToken);

        var fileId = Path.GetFileName(documentUrl);

        return new UploadKybDocumentResultDto(
            FileId: fileId,
            DocumentUrl: documentUrl,
            OriginalFileName: Path.GetFileName(originalFileName),
            SizeBytes: memoryStream.Length,
            ContentType: detectedMimeType);
    }

    private static (string normalizedExt, string mimeType) ValidateAndDetectFormat(
        KybDocumentType type,
        string originalFileName,
        byte[] header)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
            throw new ValidationException("Tên file phải có phần mở rộng hợp lệ.");

        bool isPdf = header.Length >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D;
        bool isPng = header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        bool isJpeg = header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool isWebp = header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        if (type is KybDocumentType.BusinessLicense or KybDocumentType.FireSafetyCertificate)
        {
            if (isPdf && ext == ".pdf") return (".pdf", "application/pdf");
            if (isPng && ext == ".png") return (".png", "image/png");
            if (isJpeg && ext is ".jpg" or ".jpeg") return (".jpg", "image/jpeg");
            throw new ValidationException("Hồ sơ giấy tờ pháp lý chỉ chấp nhận file định dạng PDF, JPG hoặc PNG hợp lệ.");
        }
        else // SitePhoto
        {
            if (isPng && ext == ".png") return (".png", "image/png");
            if (isJpeg && ext is ".jpg" or ".jpeg") return (".jpg", "image/jpeg");
            if (isWebp && ext == ".webp") return (".webp", "image/webp");
            throw new ValidationException("Ảnh hiện trường bãi xe chỉ chấp nhận file JPG, PNG hoặc WebP hợp lệ.");
        }
    }
}
