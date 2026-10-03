namespace ParkingManagement.SharedKernel.Domain;

/// <summary>
/// Lớp cơ sở cho mọi Entity trong Domain.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; protected set; }

    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    /// <summary>Tự cập nhật bởi DbContext mỗi khi entity bị sửa.</summary>
    public DateTime? UpdatedAtUtc { get; protected set; }
}
