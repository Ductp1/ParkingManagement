namespace ParkingService.Application.Features.Kyb;

/// <summary>
/// Phân loại tài liệu trong hồ sơ thẩm định KYB của bãi đỗ xe (US-054).
/// </summary>
public enum KybDocumentType
{
    /// <summary>Giấy phép đăng ký kinh doanh / Giấy chứng nhận ĐKKD (PDF, JPG, PNG; tối đa 10MB).</summary>
    BusinessLicense = 1,

    /// <summary>Giấy chứng nhận thẩm duyệt hoặc phương án an toàn PCCC (PDF, JPG, PNG; tối đa 10MB).</summary>
    FireSafetyCertificate = 2,

    /// <summary>Ảnh chụp hiện trường mặt tiền / cổng / lối vào bãi xe (JPG, PNG, WebP; tối đa 5MB).</summary>
    SitePhoto = 3
}

/// <summary>
/// DTO kết quả sau khi upload tài liệu KYB thành công.
/// </summary>
public sealed record UploadKybDocumentResultDto(
    string FileId,
    string DocumentUrl,
    string OriginalFileName,
    long SizeBytes,
    string ContentType);

/// <summary>
/// Dữ liệu nội dung file tài liệu KYB đọc từ bộ lưu trữ.
/// </summary>
public sealed record KybDocumentFileContent(
    byte[] Bytes,
    string ContentType,
    string FileName);
