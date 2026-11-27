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

        // Lấy tất cả các slot trong DB của tầng (cả active lẫn inactive để phân biệt trạng thái)
        var allDbSlots = await slotRepository.GetByFloorIdAsync(floorId, cancellationToken);

        var slotByCode = allDbSlots
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var layoutVersion = await layoutVersionRepository.GetLatestPublishedByFloorIdAsync(floorId, cancellationToken);

        if (layoutVersion != null && !string.IsNullOrWhiteSpace(layoutVersion.LayoutJson) && layoutVersion.LayoutJson != "{}")
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(layoutVersion.LayoutJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lỗi cú pháp LayoutJson của Floor Id={FloorId}. Chuyển sang fallback synthesis.", floorId);
            }

            if (doc != null)
            {
                using (doc)
                {
                    var root = doc.RootElement;

                    var declaredColumns = root.TryGetProperty("columns", out var colProp) && colProp.TryGetInt32(out var c) && c > 0
                        ? c
                        : (floor.GridColumns > 0 ? floor.GridColumns : 20);

                    var declaredRows = root.TryGetProperty("rows", out var rowProp) && rowProp.TryGetInt32(out var r) && r > 0
                        ? r
                        : (floor.GridRows > 0 ? floor.GridRows : 10);

                    var cellsList = new List<LayoutCellDto>();
                    var mappedSlotCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (root.TryGetProperty("cells", out var cellsProp) && cellsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cellEl in cellsProp.EnumerateArray())
                        {
                            try
                            {
                                if (cellEl.ValueKind != JsonValueKind.Object)
                                    continue;

                                var rawX = cellEl.TryGetProperty("x", out var xProp) && xProp.TryGetInt32(out var cx) ? cx : 0;
                                var rawY = cellEl.TryGetProperty("y", out var yProp) && yProp.TryGetInt32(out var cy) ? cy : 0;
                                var x = Math.Max(0, rawX);
                                var y = Math.Max(0, rawY);

                                var type = cellEl.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "slot" : "slot";
                                var code = cellEl.TryGetProperty("code", out var cdProp) ? cdProp.GetString() : null;

                                var widthCells = cellEl.TryGetProperty("widthCells", out var wcProp) && wcProp.TryGetInt32(out var wc) && wc > 0 ? wc : 1;
                                var heightCells = cellEl.TryGetProperty("heightCells", out var hcProp) && hcProp.TryGetInt32(out var hc) && hc > 0 ? hc : 1;

                                if (string.Equals(type, "slot", StringComparison.OrdinalIgnoreCase))
                                {
                                    string? trimmedCode = !string.IsNullOrWhiteSpace(code) ? code.Trim() : null;
                                    string stateStr;
                                    string slotTypeStr;
                                    string vehicleTypeStr;

                                    if (trimmedCode != null && slotByCode.TryGetValue(trimmedCode, out var matchedSlot))
                                    {
                                        mappedSlotCodes.Add(trimmedCode);

                                        if (matchedSlot.IsActive)
                                        {
                                            // Slot tồn tại và đang hoạt động -> lấy trạng thái realtime
                                            stateStr = matchedSlot.State.ToString();
                                            slotTypeStr = matchedSlot.SlotType.ToString();
                                            vehicleTypeStr = matchedSlot.MaxVehicleType.ToString();
                                            widthCells = matchedSlot.WidthCells > 0 ? matchedSlot.WidthCells : widthCells;
                                            heightCells = matchedSlot.HeightCells > 0 ? matchedSlot.HeightCells : heightCells;
                                        }
                                        else
                                        {
                                            // Slot tồn tại trong DB nhưng bị inactive -> Unavailable
                                            stateStr = "Unavailable";
                                            slotTypeStr = matchedSlot.SlotType.ToString();
                                            vehicleTypeStr = matchedSlot.MaxVehicleType.ToString();
                                        }
                                    }
                                    else
                                    {
                                        // Slot không tồn tại trong DB (phantom slot) hoặc thiếu mã code: tuyệt đối không mặc định Available
                                        stateStr = "Unavailable";
                                        slotTypeStr = cellEl.TryGetProperty("slotType", out var stProp) ? stProp.GetString() ?? SlotType.Standard.ToString() : SlotType.Standard.ToString();
                                        vehicleTypeStr = cellEl.TryGetProperty("maxVehicleType", out var vtProp) ? vtProp.GetString() ?? VehicleType.Suv.ToString() : VehicleType.Suv.ToString();
                                    }

                                    cellsList.Add(new LayoutCellDto(
                                        X: x,
                                        Y: y,
                                        Type: "slot",
                                        Code: trimmedCode,
                                        SlotType: slotTypeStr,
                                        MaxVehicleType: vehicleTypeStr,
                                        State: stateStr,
                                        WidthCells: widthCells,
                                        HeightCells: heightCells));
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
                            catch (Exception cellEx)
                            {
                                logger.LogWarning(cellEx, "Bỏ qua cell không hợp lệ trong LayoutVersion của Floor Id={FloorId}", floorId);
                            }
                        }
                    }

                    // Stale published layout handling: Bổ sung các slot đang active trong DB nhưng chưa có trong bản vẽ xuất bản
                    var unmappedActiveSlots = allDbSlots
                        .Where(s => s.IsActive && !string.IsNullOrWhiteSpace(s.Code) && !mappedSlotCodes.Contains(s.Code.Trim()))
                        .ToList();

                    if (unmappedActiveSlots.Count > 0)
                    {
                        logger.LogInformation("Phát hiện {Count} slots active trong DB chưa có trong LayoutVersion của Floor Id={FloorId}. Bổ sung tự động vào sơ đồ.",
                            unmappedActiveSlots.Count, floorId);

                        foreach (var unmapped in unmappedActiveSlots)
                        {
                            cellsList.Add(new LayoutCellDto(
                                X: Math.Max(0, unmapped.GridX),
                                Y: Math.Max(0, unmapped.GridY),
                                Type: "slot",
                                Code: unmapped.Code,
                                SlotType: unmapped.SlotType.ToString(),
                                MaxVehicleType: unmapped.MaxVehicleType.ToString(),
                                State: unmapped.State.ToString(),
                                WidthCells: unmapped.WidthCells > 0 ? unmapped.WidthCells : 1,
                                HeightCells: unmapped.HeightCells > 0 ? unmapped.HeightCells : 1));
                        }
                    }

                    // Clamping kích thước Columns và Rows để luôn bao trọn tất cả các cells
                    var maxCellX = cellsList.Count > 0
                        ? cellsList.Max(c => c.X + c.WidthCells - 1)
                        : 0;
                    var maxCellY = cellsList.Count > 0
                        ? cellsList.Max(c => c.Y + c.HeightCells - 1)
                        : 0;

                    var finalColumns = Math.Max(Math.Max(declaredColumns, floor.GridColumns), maxCellX + 1);
                    var finalRows = Math.Max(Math.Max(declaredRows, floor.GridRows), maxCellY + 1);

                    finalColumns = Math.Max(1, finalColumns);
                    finalRows = Math.Max(1, finalRows);

                    logger.LogInformation("Trả về published LayoutVersion v{VersionNo} cho Floor Id={FloorId} với {CellCount} cells (lưới {Cols}x{Rows})",
                        layoutVersion.VersionNo, floorId, cellsList.Count, finalColumns, finalRows);

                    return new FloorLayoutDto(
                        FloorId: floor.Id,
                        FloorName: floor.Name,
                        Level: floor.Level,
                        VersionNo: layoutVersion.VersionNo,
                        IsPublished: layoutVersion.IsPublished,
                        PublishedAtUtc: layoutVersion.PublishedAtUtc,
                        Columns: finalColumns,
                        Rows: finalRows,
                        Cells: cellsList);
                }
            }
        }

        // Fallback Synthesis: Sinh sơ đồ trực tiếp từ các ô đỗ active hiện có khi không có bản vẽ hoặc JSON lỗi
        var activeOnlySlots = allDbSlots.Where(s => s.IsActive).ToList();
        logger.LogInformation("Floor Id={FloorId} chưa có LayoutVersion xuất bản hợp lệ. Tự động tổng hợp sơ đồ fallback từ {Count} active slots.",
            floorId, activeOnlySlots.Count);

        var maxSlotX = activeOnlySlots.Count > 0 ? activeOnlySlots.Max(s => Math.Max(0, s.GridX) + Math.Max(s.WidthCells, 1) - 1) : 0;
        var maxSlotY = activeOnlySlots.Count > 0 ? activeOnlySlots.Max(s => Math.Max(0, s.GridY) + Math.Max(s.HeightCells, 1) - 1) : 0;

        var fallbackColumns = Math.Max(floor.GridColumns > 0 ? floor.GridColumns : 20, maxSlotX + 1);
        var fallbackRows = Math.Max(floor.GridRows > 0 ? floor.GridRows : 10, maxSlotY + 1);

        var synthesizedCells = activeOnlySlots.Select(s => new LayoutCellDto(
            X: Math.Max(0, s.GridX),
            Y: Math.Max(0, s.GridY),
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
