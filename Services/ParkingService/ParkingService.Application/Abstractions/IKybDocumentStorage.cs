using ParkingService.Application.Features.Kyb;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// Cổng lưu trữ tài liệu nhị phân (PDF/ảnh) cho hồ sơ thẩm định KYB (US-054).
/// </summary>
public interface IKybDocumentStorage
{
    /// <summary>
    /// Lưu file tài liệu vào bộ lưu trữ, trả về đường dẫn URL nội bộ an toàn.
    /// </summary>
    Task<string> SaveAsync(int lotId, KybDocumentType type, string originalFileName, string fileExtension, Stream input, CancellationToken ct = default);

    /// <summary>
    /// Đọc file tài liệu từ bộ lưu trữ theo lotId và fileId.
    /// </summary>
    Task<KybDocumentFileContent?> ReadAsync(int lotId, string fileId, CancellationToken ct = default);

    /// <summary>
    /// Xóa file tài liệu khỏi bộ lưu trữ nếu có.
    /// </summary>
    Task DeleteAsync(int lotId, string fileId, CancellationToken ct = default);
}
