using ParkingManagement.SharedKernel.Domain;

namespace UserService.Domain.Entities;

/// <summary>[UserService] Refresh token 7 ngày (JWT access token 24h).</summary>
public class RefreshToken : BaseEntity
{
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public User User { get; set; } = null!;
}
