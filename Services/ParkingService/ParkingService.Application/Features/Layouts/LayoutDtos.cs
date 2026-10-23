using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Features.Layouts;

/// <summary>
/// Phần tử cell trên lưới 2D của tầng (T-502, US-059).
/// Type có thể là: "slot", "road", "entry", "exit", "wall", "pillar", "empty".
/// </summary>
public sealed record LayoutCellDto(
    int X,
    int Y,
    string Type,
    string? Code = null,
    string? SlotType = null,
    string? MaxVehicleType = null,
    string? State = null,
    int WidthCells = 1,
    int HeightCells = 1);

/// <summary>
/// Dữ liệu sơ đồ mặt bằng tầng phục vụ component LayoutRenderer (SVG).
/// </summary>
public sealed record FloorLayoutDto(
    int FloorId,
    string FloorName,
    int Level,
    int VersionNo,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    int Columns,
    int Rows,
    IReadOnlyList<LayoutCellDto> Cells);

/// <summary>
/// Thông tin tóm tắt phiên bản sơ đồ của tầng.
/// </summary>
public sealed record LayoutVersionSummaryDto(
    int Id,
    int FloorId,
    int VersionNo,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    int CreatedByUserId,
    string? ChangeNote,
    DateTime CreatedAtUtc);
