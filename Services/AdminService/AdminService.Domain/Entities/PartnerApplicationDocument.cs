using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Giấy tờ pháp lý đối tác nộp kèm hồ sơ đăng ký chủ bãi (US-053). File nằm trong kho file của AdminService
/// (khóa <see cref="FileKey"/> do server sinh); chỉ Admin tải được qua api/v1/admin/partner-applications/{id}/documents.
/// Không lưu URL do client gửi.
/// </summary>
public class PartnerApplicationDocument : BaseEntity
{
    public int PartnerApplicationId { get; private set; }
    public PartnerApplication? PartnerApplication { get; private set; }

    public PartnerDocumentType DocumentType { get; private set; }

    /// <summary>Khóa trong IFileStorage, ví dụ "partner-applications/3f2a…/business-license-1.pdf".</summary>
    public string FileKey { get; private set; } = string.Empty;

    /// <summary>MIME nhận diện từ nội dung file (không tin Content-Type của client).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Tên file gốc (chỉ để hiển thị cho Admin, đã cắt bỏ đường dẫn).</summary>
    public string OriginalFileName { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    private PartnerApplicationDocument() { }

    public static PartnerApplicationDocument Create(PartnerApplication application, PartnerDocumentType type, string fileKey,
        string contentType, string originalFileName, long sizeBytes, string sha256) => new()
    {
        PartnerApplication = application,
        DocumentType = type,
        FileKey = fileKey,
        ContentType = contentType,
        OriginalFileName = originalFileName,
        SizeBytes = sizeBytes,
        Sha256 = sha256,
    };
}
