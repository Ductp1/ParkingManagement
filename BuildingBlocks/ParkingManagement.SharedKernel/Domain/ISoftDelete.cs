namespace ParkingManagement.SharedKernel.Domain;

/// <summary>
/// Entity không được xóa cứng (Nghiệp vụ v3 §4.1: lưu SOFT-DELETE 7 năm).
/// DbContext tự lọc bản ghi đã xóa bằng global query filter.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}
