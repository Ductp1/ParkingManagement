using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Lịch ngày lễ để áp hệ số HolidayMultiplier của RateCard (US-071).
/// Admin quản lý chung cho toàn sàn; City = null nghĩa là áp cho mọi thành phố.
/// </summary>
public class Holiday : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
}
