using ParkingManagement.SharedKernel.Enums;

namespace UserService.Domain.Entities;

/// <summary>[UserService] Quan hệ User – Role, khóa chính ghép (UserId, Role).</summary>
public class UserRole
{
    public int UserId { get; set; }
    public UserRoleType Role { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
