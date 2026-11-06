namespace ParkingService.Application.Features.LotDetails;

/// <summary>
/// Danh mục tiện ích chuẩn của hệ thống (US-064 / US-016 / US-018).
/// </summary>
public static class StandardAmenitiesCatalog
{
    public static readonly IReadOnlyList<AmenityCatalogItemDto> Items =
    [
        new("COVERED", "Bãi có mái che", "Hầm để xe hoặc bãi có mái che chắn nắng mưa"),
        new("CCTV", "Camera giám sát 24/7", "Hệ thống camera an ninh giám sát liên tục"),
        new("SECURITY_24H", "Bảo vệ túc trực 24/7", "Có nhân viên bảo vệ điều phối và kiểm soát"),
        new("EV_CHARGER", "Trụ sạc xe điện", "Có trang bị trụ sạc AC/DC cho xe ô tô điện"),
        new("CAR_WASH", "Dịch vụ rửa xe", "Có dịch vụ rửa xe, chăm sóc xe tại chỗ"),
        new("OVERSIZED", "Nhận xe quá khổ", "Có ô đỗ kích thước lớn cho xe van/bán tải/xe 16 chỗ"),
        new("DISABLED_ACCESS", "Lối đi người khuyết tật", "Lối lên xuống dốc và ô đỗ riêng gần thang máy"),
        new("VALET", "Dịch vụ đỗ xe hộ", "Có nhân viên nhận và đánh xe vào bãi"),
        new("SHUTTLE", "Xe đưa đón", "Xe điện/shuttle bus đưa đón ra sân bay, trung tâm"),
        new("TOILET", "Nhà vệ sinh công cộng", "Có nhà vệ sinh sạch sẽ cho tài xế"),
        new("ELEVATOR", "Thang máy", "Có thang máy kết nối giữa các tầng hầm"),
        new("TIRE_INFLATION", "Bơm lốp / Cứu hộ lốp", "Có trang bị máy bơm hơi lốp xe miễn phí")
    ];

    private static readonly Dictionary<string, AmenityCatalogItemDto> ByCode =
        Items.ToDictionary(i => i.Code, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string code, out AmenityCatalogItemDto? item)
        => ByCode.TryGetValue(code, out item);

    public static string GetDefaultName(string code)
        => ByCode.TryGetValue(code, out var item) ? item.Name : code;
}

// ==================== AMENITIES DTOs ====================

public sealed record AmenityCatalogItemDto(string Code, string Name, string Description);

public sealed record LotAmenityDto(int Id, int ParkingLotId, string Code, string Name, string? Note);

public sealed record UpdateLotAmenitiesRequest(IReadOnlyList<LotAmenityInputDto> Amenities);

public sealed record LotAmenityInputDto(string Code, string? Name = null, string? Note = null);

// ==================== PHOTOS DTOs ====================

public sealed record LotPhotoDto(int Id, int ParkingLotId, string Url, string? Caption, int SortOrder, bool IsCover);

public sealed record AddLotPhotoRequest(string Url, string? Caption = null, int SortOrder = 0, bool IsCover = false);

public sealed record UpdateLotPhotoRequest(string? Caption, int? SortOrder);

public sealed record ReorderLotPhotosRequest(IReadOnlyList<PhotoOrderItemDto> Items);

public sealed record PhotoOrderItemDto(int PhotoId, int SortOrder);

// ==================== LOT ATTRIBUTES DTOs ====================

public sealed record LotAttributesDto(
    int ParkingLotId,
    string Name,
    string? Description,
    string? HotlinePhone,
    int MaxHeightCm,
    string? CoverImageUrl,
    IReadOnlyList<LotAmenityDto> Amenities,
    IReadOnlyList<LotPhotoDto> Photos);

public sealed record UpdateLotAttributesRequest(
    string? Description,
    string? HotlinePhone,
    int? MaxHeightCm);
