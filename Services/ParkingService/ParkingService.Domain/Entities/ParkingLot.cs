using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Bãi đỗ xe (Facility) – cấp cao nhất trong cây Facility → Zone → Floor → Slot.
/// Chứa dữ liệu + hành vi nghiệp vụ thuần, không phụ thuộc framework/DB.
/// </summary>
public class ParkingLot : BaseEntity, ISoftDelete
{
    /// <summary>Chủ bãi sở hữu (OwnerProfile bên UserService, không FK) – khóa tenant để cách ly dữ liệu.</summary>
    public int OwnerProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    /// <summary>Tỉnh/thành – MVP 1 thành phố, sẵn sàng đa thành phố (US-115).</summary>
    public string City { get; private set; } = "TP.HCM";
    public string? District { get; private set; }
    public string? Description { get; private set; }
    public string? HotlinePhone { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    /// <summary>Tổng số chỗ đỗ ô tô của bãi.</summary>
    public int TotalSlots { get; private set; }

    /// <summary>Số chỗ còn trống hiện tại.</summary>
    public int AvailableSlots { get; private set; }

    /// <summary>Chiều cao thông thủy tối đa (cm) – dùng cho Hard Height Clearance Rule.</summary>
    public int MaxHeightCm { get; private set; }

    public TimeOnly OpenTime { get; private set; }
    public TimeOnly CloseTime { get; private set; }

    public ParkingLotStatus Status { get; private set; }
    public IntegrationTier IntegrationTier { get; private set; } = IntegrationTier.Manual;

    /// <summary>Điểm đánh giá trung bình – cập nhật khi nhận sự kiện ReviewCreated từ Support Service.</summary>
    public decimal RatingAverage { get; private set; }
    public int RatingCount { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<Zone> Zones { get; private set; } = new List<Zone>();
    public LotCapacityConfig? CapacityConfig { get; private set; }
    public ICollection<LotOperatingHour> OperatingHours { get; private set; } = new List<LotOperatingHour>();
    public ICollection<LotAmenity> Amenities { get; private set; } = new List<LotAmenity>();
    public ICollection<LotPhoto> Photos { get; private set; } = new List<LotPhoto>();

    // Constructor rỗng cho EF Core.
    private ParkingLot() { }

    public ParkingLot(int id, string name, string address, double latitude, double longitude,
                      int totalSlots, int availableSlots, int maxHeightCm,
                      TimeOnly openTime, TimeOnly closeTime, ParkingLotStatus status,
                      int ownerProfileId = 0, IntegrationTier integrationTier = IntegrationTier.Manual,
                      string? description = null, string? hotlinePhone = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tên bãi không được rỗng.", nameof(name));
        if (totalSlots <= 0) throw new ArgumentOutOfRangeException(nameof(totalSlots), "Tổng số chỗ phải > 0.");
        if (availableSlots < 0 || availableSlots > totalSlots)
            throw new ArgumentOutOfRangeException(nameof(availableSlots), "Số chỗ trống phải nằm trong [0, TotalSlots].");

        Id = id;
        Name = name;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        TotalSlots = totalSlots;
        AvailableSlots = availableSlots;
        MaxHeightCm = maxHeightCm;
        OpenTime = openTime;
        CloseTime = closeTime;
        Status = status;
        OwnerProfileId = ownerProfileId;
        IntegrationTier = integrationTier;
        Description = description;
        HotlinePhone = hotlinePhone;
    }

    // ===================== Hành vi nghiệp vụ (Domain logic) =====================

    /// <summary>Bãi có đang hiển thị công khai cho tài xế không.</summary>
    public bool IsPubliclyVisible => Status == ParkingLotStatus.Active && !IsDeleted;

    /// <summary>Tỷ lệ lấp đầy (%) – làm tròn 1 chữ số.</summary>
    public double OccupancyRate => Math.Round((TotalSlots - AvailableSlots) * 100.0 / TotalSlots, 1);

    /// <summary>
    /// Bãi có mở cửa tại thời điểm <paramref name="time"/> không.
    /// Hỗ trợ cả bãi mở qua đêm (VD 18:00 → 07:00) và 24/7 (Open == Close).
    /// </summary>
    public bool IsOpenAt(TimeOnly time)
    {
        if (OpenTime == CloseTime) return true;                       // 24/7
        if (OpenTime < CloseTime) return time >= OpenTime && time < CloseTime;
        return time >= OpenTime || time < CloseTime;                  // mở qua đêm
    }

    /// <summary>Đồng bộ lại bộ đếm chỗ khi cấu trúc slot hoặc trạng thái slot thay đổi.</summary>
    public void SetSlotCounters(int totalSlots, int availableSlots)
    {
        if (totalSlots < 0 || availableSlots < 0 || availableSlots > totalSlots)
            throw new ArgumentOutOfRangeException(nameof(availableSlots), "Bộ đếm chỗ không hợp lệ.");
        TotalSlots = totalSlots;
        AvailableSlots = availableSlots;
    }

    /// <summary>Admin duyệt KYB → bãi được công khai.</summary>
    public void Activate() => Status = ParkingLotStatus.Active;

    /// <summary>Gán quận/thành phố (dùng khi tạo bãi và khi seed dữ liệu).</summary>
    public void SetLocation(string city, string? district)
    {
        City = string.IsNullOrWhiteSpace(city) ? throw new ArgumentException("Thành phố không được rỗng.", nameof(city)) : city;
        District = district;
    }

    /// <summary>Chế tài SLA cấp 3 hoặc Emergency Stop.</summary>
    public void Suspend() => Status = ParkingLotStatus.Suspended;
}
