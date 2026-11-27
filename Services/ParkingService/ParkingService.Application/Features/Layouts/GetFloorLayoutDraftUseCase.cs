using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IGetFloorLayoutDraftUseCase
{
    Task<FloorLayoutDto> ExecuteAsync(int floorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy bản vẽ sơ đồ nháp (Draft) của tầng phục vụ Layout Configurator 2D (US-059).
/// - Nếu tầng đã có bản nháp chưa publish: Trả về bản nháp đó.
/// - Nếu chưa có bản nháp: Trả về sơ đồ đã xuất bản hiện tại (hoặc tổng hợp từ DB slots) làm khuôn mẫu để chỉnh sửa tiếp.
/// </summary>
public sealed class GetFloorLayoutDraftUseCase(
    IFloorRepository floorRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ISlotRepository slotRepository,
    IGetFloorLayoutUseCase getFloorLayoutUseCase,
    ILogger<GetFloorLayoutDraftUseCase> logger) : IGetFloorLayoutDraftUseCase
{
    public async Task<FloorLayoutDto> ExecuteAsync(int floorId, CancellationToken cancellationToken = default)
    {
        if (floorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        var floor = await floorRepository.GetByIdAsync(floorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", floorId);

        var draft = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(floorId, cancellationToken);

        if (draft != null && !string.IsNullOrWhiteSpace(draft.LayoutJson) && draft.LayoutJson != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(draft.LayoutJson);
                var root = doc.RootElement;

                var declaredColumns = root.TryGetProperty("columns", out var colProp) && colProp.TryGetInt32(out var c) && c > 0
                    ? c
                    : (floor.GridColumns > 0 ? floor.GridColumns : 20);

                var declaredRows = root.TryGetProperty("rows", out var rowProp) && rowProp.TryGetInt32(out var r) && r > 0
                    ? r
                    : (floor.GridRows > 0 ? floor.GridRows : 10);

                var allDbSlots = await slotRepository.GetByFloorIdAsync(floorId, cancellationToken);
                var slotByCode = allDbSlots
                    .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                    .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var cellsList = new List<LayoutCellDto>();

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
                                    stateStr = matchedSlot.IsActive ? matchedSlot.State.ToString() : "Unavailable";
                                    slotTypeStr = matchedSlot.SlotType.ToString();
                                    vehicleTypeStr = matchedSlot.MaxVehicleType.ToString();
                                    widthCells = matchedSlot.WidthCells > 0 ? matchedSlot.WidthCells : widthCells;
                                    heightCells = matchedSlot.HeightCells > 0 ? matchedSlot.HeightCells : heightCells;
                                }
                                else
                                {
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
                            logger.LogWarning(cellEx, "Bỏ qua cell không hợp lệ trong bản nháp Floor Id={FloorId}", floorId);
                        }
                    }
                }

                var maxCellX = cellsList.Count > 0 ? cellsList.Max(c => c.X + c.WidthCells - 1) : 0;
                var maxCellY = cellsList.Count > 0 ? cellsList.Max(c => c.Y + c.HeightCells - 1) : 0;
                var finalColumns = Math.Max(Math.Max(declaredColumns, floor.GridColumns), maxCellX + 1);
                var finalRows = Math.Max(Math.Max(declaredRows, floor.GridRows), maxCellY + 1);

                logger.LogInformation("Trả về bản nháp LayoutVersion v{VersionNo} cho Floor Id={FloorId} với {Count} cells",
                    draft.VersionNo, floorId, cellsList.Count);

                return new FloorLayoutDto(
                    FloorId: floor.Id,
                    FloorName: floor.Name,
                    Level: floor.Level,
                    VersionNo: draft.VersionNo,
                    IsPublished: false,
                    PublishedAtUtc: null,
                    Columns: finalColumns,
                    Rows: finalRows,
                    Cells: cellsList);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lỗi khi parse bản nháp của Floor Id={FloorId}. Lấy bản vẽ hiện tại làm mẫu.", floorId);
            }
        }

        // Nếu chưa có bản nháp, lấy sơ đồ đã xuất bản hiện tại (hoặc fallback) làm bản mẫu để bắt đầu chỉnh sửa
        logger.LogInformation("Floor Id={FloorId} chưa có bản nháp dở dang. Trả về sơ đồ hiện tại làm mẫu biên tập.", floorId);
        var currentLayout = await getFloorLayoutUseCase.ExecuteAsync(floorId, cancellationToken);
        return currentLayout with { IsPublished = false, PublishedAtUtc = null };
    }
}
