using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IGetFloorLayoutUseCase
{
    Task<FloorLayoutDto> ExecuteAsync(int floorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy sơ đồ mặt bằng lưới 2D của tầng phục vụ component LayoutRenderer (SVG) - Task T-502.
/// Tự động làm giàu (enrich) trạng thái slot realtime (Available, Occupied, Reserved, Maintenance)
/// và có Fallback Synthesis Engine tổng hợp lưới từ bảng Slots khi tầng chưa từng có LayoutVersion.
/// </summary>
public sealed class GetFloorLayoutUseCase(
    IFloorRepository floorRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ISlotRepository slotRepository,
    ILogger<GetFloorLayoutUseCase> logger) : IGetFloorLayoutUseCase
{
    public async Task<FloorLayoutDto> ExecuteAsync(int floorId, CancellationToken cancellationToken = default)
    {
        if (floorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        var floor = await floorRepository.GetByIdAsync(floorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", floorId);

        var dbSlots = await slotRepository.GetByFloorIdAsync(floorId, cancellationToken);
        var activeSlots = dbSlots.Where(s => s.IsActive).ToList();

        var slotByCode = activeSlots
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var slotByCoord = activeSlots
            .GroupBy(s => (s.GridX, s.GridY))
            .ToDictionary(g => g.Key, g => g.First());

        var layoutVersion = await layoutVersionRepository.GetLatestPublishedByFloorIdAsync(floorId, cancellationToken);

        if (layoutVersion != null && !string.IsNullOrWhiteSpace(layoutVersion.LayoutJson) && layoutVersion.LayoutJson != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(layoutVersion.LayoutJson);
                var root = doc.RootElement;

                var columns = root.TryGetProperty("columns", out var colProp) && colProp.TryGetInt32(out var c) && c > 0
                    ? c
                    : (floor.GridColumns > 0 ? floor.GridColumns : 20);

                var rows = root.TryGetProperty("rows", out var rowProp) && rowProp.TryGetInt32(out var r) && r > 0
                    ? r
                    : (floor.GridRows > 0 ? floor.GridRows : 10);

                var cellsList = new List<LayoutCellDto>();

                if (root.TryGetProperty("cells", out var cellsProp) && cellsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cellEl in cellsProp.EnumerateArray())
                    {
                        var x = cellEl.TryGetProperty("x", out var xProp) && xProp.TryGetInt32(out var cx) ? cx : 0;
                        var y = cellEl.TryGetProperty("y", out var yProp) && yProp.TryGetInt32(out var cy) ? cy : 0;
                        var type = cellEl.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "slot" : "slot";
                        var code = cellEl.TryGetProperty("code", out var cdProp) ? cdProp.GetString() : null;
                        var widthCells = cellEl.TryGetProperty("widthCells", out var wcProp) && wcProp.TryGetInt32(out var wc) ? wc : 1;
                        var heightCells = cellEl.TryGetProperty("heightCells", out var hcProp) && hcProp.TryGetInt32(out var hc) ? hc : 1;

                        if (string.Equals(type, "slot", StringComparison.OrdinalIgnoreCase))
                        {
                            Slot? matchedSlot = null;
                            if (!string.IsNullOrWhiteSpace(code) && slotByCode.TryGetValue(code.Trim(), out var sCode))
                            {
                                matchedSlot = sCode;
                            }
                            else if (slotByCoord.TryGetValue((x, y), out var sCoord))
                            {
                                matchedSlot = sCoord;
                            }

                            var slotTypeStr = matchedSlot?.SlotType.ToString()
                                ?? (cellEl.TryGetProperty("slotType", out var stProp) ? stProp.GetString() : SlotType.Standard.ToString());
                            var vehicleTypeStr = matchedSlot?.MaxVehicleType.ToString()
                                ?? (cellEl.TryGetProperty("maxVehicleType", out var vtProp) ? vtProp.GetString() : VehicleType.Suv.ToString());
                            var stateStr = matchedSlot?.State.ToString() ?? SlotState.Available.ToString();
                            var finalCode = matchedSlot?.Code ?? code;

                            cellsList.Add(new LayoutCellDto(
                                X: x,
                                Y: y,
                                Type: "slot",
                                Code: finalCode,
                                SlotType: slotTypeStr,
                                MaxVehicleType: vehicleTypeStr,
                                State: stateStr,
                                WidthCells: matchedSlot?.WidthCells ?? widthCells,
                                HeightCells: matchedSlot?.HeightCells ?? heightCells));
                        }
                        else
                        {
                            cellsList.Add(new LayoutCellDto(
                                X: x,
                                Y: y,
                                Type: type.ToLowerInvariant(),
                                WidthCells: widthCells,
                                HeightCells: heightCells));
                        }
                    }
                }

                logger.LogInformation("Trả về published LayoutVersion v{VersionNo} cho Floor Id={FloorId} với {CellCount} cells",
                    layoutVersion.VersionNo, floorId, cellsList.Count);

                return new FloorLayoutDto(
                    FloorId: floor.Id,
                    FloorName: floor.Name,
                    Level: floor.Level,
                    VersionNo: layoutVersion.VersionNo,
                    IsPublished: layoutVersion.IsPublished,
                    PublishedAtUtc: layoutVersion.PublishedAtUtc,
                    Columns: columns,
                    Rows: rows,
                    Cells: cellsList);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lỗi khi parse LayoutJson cho Floor Id={FloorId}. Chuyển sang fallback synthesis.", floorId);
            }
        }

        // Fallback Synthesis: Sinh sơ đồ trực tiếp từ các ô đỗ hiện có
        logger.LogInformation("Floor Id={FloorId} chưa có LayoutVersion xuất bản. Tự động tổng hợp sơ đồ fallback từ {Count} slots.",
            floorId, activeSlots.Count);

        var maxSlotX = activeSlots.Count > 0 ? activeSlots.Max(s => s.GridX + Math.Max(s.WidthCells, 1) - 1) : 0;
        var maxSlotY = activeSlots.Count > 0 ? activeSlots.Max(s => s.GridY + Math.Max(s.HeightCells, 1) - 1) : 0;

        var fallbackColumns = floor.GridColumns > 0
            ? Math.Max(floor.GridColumns, maxSlotX + 1)
            : Math.Max(20, maxSlotX + 1);

        var fallbackRows = floor.GridRows > 0
            ? Math.Max(floor.GridRows, maxSlotY + 1)
            : Math.Max(10, maxSlotY + 1);

        var synthesizedCells = activeSlots.Select(s => new LayoutCellDto(
            X: s.GridX,
            Y: s.GridY,
            Type: "slot",
            Code: s.Code,
            SlotType: s.SlotType.ToString(),
            MaxVehicleType: s.MaxVehicleType.ToString(),
            State: s.State.ToString(),
            WidthCells: s.WidthCells > 0 ? s.WidthCells : 1,
            HeightCells: s.HeightCells > 0 ? s.HeightCells : 1
        )).ToList();

        return new FloorLayoutDto(
            FloorId: floor.Id,
            FloorName: floor.Name,
            Level: floor.Level,
            VersionNo: 0,
            IsPublished: false,
            PublishedAtUtc: null,
            Columns: fallbackColumns,
            Rows: fallbackRows,
            Cells: synthesizedCells);
    }
}
