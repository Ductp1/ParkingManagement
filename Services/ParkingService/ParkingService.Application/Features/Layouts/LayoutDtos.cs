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
/// Dữ liệu đầu vào của một cell khi lưu bản nháp hoặc xuất bản layout (US-059).
/// </summary>
public sealed record LayoutCellInputDto(
    int X,
    int Y,
    string Type,
    string? Code = null,
    SlotType? SlotType = null,
    VehicleType? MaxVehicleType = null,
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
/// Thông tin tóm tắt phiên bản sơ đồ của tầng (US-059).
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

/// <summary>
/// Chi tiết đầy đủ của một phiên bản sơ đồ tầng (US-059).
/// </summary>
public sealed record FloorLayoutDetailDto(
    int FloorId,
    string FloorName,
    int Level,
    int VersionNo,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    int Columns,
    int Rows,
    string? ChangeNote,
    int CreatedByUserId,
    DateTime CreatedAtUtc,
    IReadOnlyList<LayoutCellDto> Cells);

/// <summary>
/// Lệnh lưu bản nháp layout sơ đồ lưới của tầng (US-059).
/// Không tác động đến bảng vật lý Slots.
/// </summary>
public sealed record SaveFloorLayoutDraftCommand(
    int FloorId,
    int Columns,
    int Rows,
    IReadOnlyList<LayoutCellInputDto> Cells,
    string? ChangeNote = null,
    int UserId = 0);

/// <summary>
/// Lệnh xuất bản layout mới và kích hoạt đồng bộ hóa bảng Slots (US-059).
/// </summary>
public sealed record PublishFloorLayoutCommand(
    int FloorId,
    int Columns,
    int Rows,
    IReadOnlyList<LayoutCellInputDto> Cells,
    string? ChangeNote = null,
    int UserId = 0);

/// <summary>
/// Request DTO cho API lưu bản nháp layout (US-059).
/// </summary>
public sealed record SaveFloorLayoutDraftRequest(
    int Columns,
    int Rows,
    IReadOnlyList<LayoutCellInputDto> Cells,
    string? ChangeNote = null);

/// <summary>
/// Request DTO cho API xuất bản layout (US-059).
/// </summary>
public sealed record PublishFloorLayoutRequest(
    int Columns,
    int Rows,
    IReadOnlyList<LayoutCellInputDto> Cells,
    string? ChangeNote = null);
