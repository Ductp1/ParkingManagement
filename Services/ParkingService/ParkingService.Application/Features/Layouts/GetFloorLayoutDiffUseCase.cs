using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IGetFloorLayoutDiffUseCase
{
    Task<FloorLayoutDiffDto> ExecuteAsync(int floorId, int fromVersionNo, int? toVersionNo = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: So sánh sự khác biệt (Diff & Impact Analysis) giữa 2 phiên bản sơ đồ tầng (US-060).
/// Phân loại chi tiết Added, Removed, Modified slots và tự động xác định các vi phạm an toàn booking active (IsSafetyBlocker).
/// </summary>
public sealed class GetFloorLayoutDiffUseCase(
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<GetFloorLayoutDiffUseCase> logger) : IGetFloorLayoutDiffUseCase
{
    public async Task<FloorLayoutDiffDto> ExecuteAsync(int floorId, int fromVersionNo, int? toVersionNo = null, CancellationToken cancellationToken = default)
    {
        if (floorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (fromVersionNo <= 0)
            throw new ValidationException("Số phiên bản gốc (fromVersionNo) phải lớn hơn 0.");

        var floor = await floorRepository.GetByIdAsync(floorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", floorId);

        // 1. Lấy phiên bản gốc FromVersion
        var fromVersion = await layoutVersionRepository.GetByFloorIdAndVersionAsync(floorId, fromVersionNo, cancellationToken)
            ?? throw new NotFoundException($"Phiên bản sơ đồ v{fromVersionNo} của tầng", floorId);

        // 2. Lấy phiên bản mục tiêu ToVersion (nếu null lấy bản nháp dở dang hoặc bản xuất bản mới nhất)
        LayoutVersion? toVersion = null;
        if (toVersionNo.HasValue && toVersionNo.Value > 0)
        {
            toVersion = await layoutVersionRepository.GetByFloorIdAndVersionAsync(floorId, toVersionNo.Value, cancellationToken)
                ?? throw new NotFoundException($"Phiên bản sơ đồ mục tiêu v{toVersionNo.Value} của tầng", floorId);
        }
        else
        {
            toVersion = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(floorId, cancellationToken)
                        ?? await layoutVersionRepository.GetLatestPublishedByFloorIdAsync(floorId, cancellationToken);
        }


        logger.LogInformation("So sánh khác biệt sơ đồ Floor Id={FloorId} từ v{From} sang v{To}", floorId, fromVersionNo, toVersion != null ? toVersion.VersionNo : null);

        var parsedFrom = ParseLayout(fromVersion?.LayoutJson, floor);
        var parsedTo = ParseLayout(toVersion?.LayoutJson, floor);

        // 3. Lấy thông tin trạng thái realtime của tất cả Slot trong DB
        var dbSlots = await slotRepository.GetByFloorIdAsync(floorId, cancellationToken);
        var dbSlotMap = dbSlots
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var fromSlotMap = parsedFrom.SlotCells
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var toSlotMap = parsedTo.SlotCells
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var addedSlots = new List<LayoutSlotDiffDto>();
        var removedSlots = new List<LayoutSlotDiffDto>();
        var modifiedSlots = new List<LayoutSlotDiffDto>();
        var safetyBlockers = new List<string>();
        var warnings = new List<string>();

        // 4a. Phân tích AddedSlots (có trong To nhưng không có trong From)
        foreach (var (code, toCell) in toSlotMap)
        {
            if (!fromSlotMap.ContainsKey(code))
            {
                dbSlotMap.TryGetValue(code, out var dbSlot);
                addedSlots.Add(new LayoutSlotDiffDto(
                    Code: code,
                    DiffType: "Added",
                    OldX: null,
                    OldY: null,
                    NewX: toCell.X,
                    NewY: toCell.Y,
                    OldSlotType: null,
                    NewSlotType: toCell.SlotType?.ToString() ?? "Standard",
                    OldMaxVehicleType: null,
                    NewMaxVehicleType: toCell.MaxVehicleType?.ToString() ?? "Suv",
                    CurrentSlotState: dbSlot?.State.ToString() ?? "Available",
                    HasActiveBooking: false,
                    IsSafetyBlocker: false));
            }
        }

        // 4b. Phân tích RemovedSlots (có trong From nhưng bị gỡ khỏi To)
        foreach (var (code, fromCell) in fromSlotMap)
        {
            if (!toSlotMap.ContainsKey(code))
            {
                dbSlotMap.TryGetValue(code, out var dbSlot);
                var isActiveBooking = dbSlot != null && (dbSlot.State == SlotState.Occupied || dbSlot.State == SlotState.Reserved);
                var isBlocker = isActiveBooking;
                string? blockerReason = null;

                if (isBlocker)
                {
                    var reason = dbSlot!.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
                    blockerReason = $"Ô đỗ '{code}' {reason}, không thể gỡ bỏ khỏi bản vẽ.";
                    safetyBlockers.Add(blockerReason);
                }
                else
                {
                    warnings.Add($"Ô đỗ '{code}' sẽ bị vô hiệu hóa khi áp dụng bản vẽ mới.");
                }

                removedSlots.Add(new LayoutSlotDiffDto(
                    Code: code,
                    DiffType: "Removed",
                    OldX: fromCell.X,
                    OldY: fromCell.Y,
                    NewX: null,
                    NewY: null,
                    OldSlotType: fromCell.SlotType?.ToString() ?? "Standard",
                    NewSlotType: null,
                    OldMaxVehicleType: fromCell.MaxVehicleType?.ToString() ?? "Suv",
                    NewMaxVehicleType: null,
                    CurrentSlotState: dbSlot?.State.ToString() ?? "Unavailable",
                    HasActiveBooking: isActiveBooking,
                    IsSafetyBlocker: isBlocker,
                    BlockerReason: blockerReason));
            }
        }

        // 4c. Phân tích ModifiedSlots (có trong cả 2 nhưng khác thông số)
        foreach (var (code, fromCell) in fromSlotMap)
        {
            if (toSlotMap.TryGetValue(code, out var toCell))
            {
                var coordChanged = fromCell.X != toCell.X || fromCell.Y != toCell.Y;
                var sizeChanged = fromCell.WidthCells != toCell.WidthCells || fromCell.HeightCells != toCell.HeightCells;
                var typeChanged = fromCell.SlotType != toCell.SlotType;
                var vehicleChanged = fromCell.MaxVehicleType != toCell.MaxVehicleType;

                if (coordChanged || sizeChanged || typeChanged || vehicleChanged)
                {
                    dbSlotMap.TryGetValue(code, out var dbSlot);
                    var isActiveBooking = dbSlot != null && (dbSlot.State == SlotState.Occupied || dbSlot.State == SlotState.Reserved);
                    var isBlocker = false;
                    string? blockerReason = null;

                    // Kiểm tra hạ cấp loại xe cho slot đang active
                    if (isActiveBooking && vehicleChanged)
                    {
                        var oldRank = GetVehicleTypeRank(fromCell.MaxVehicleType ?? VehicleType.Suv);
                        var newRank = GetVehicleTypeRank(toCell.MaxVehicleType ?? VehicleType.Suv);
                        if (newRank < oldRank)
                        {
                            isBlocker = true;
                            var reason = dbSlot!.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
                            blockerReason = $"Ô đỗ '{code}' {reason}, không thể hạ cấp loại xe tối đa từ '{fromCell.MaxVehicleType}' xuống '{toCell.MaxVehicleType}'.";
                            safetyBlockers.Add(blockerReason);
                        }
                    }

                    if (coordChanged && isActiveBooking)
                    {
                        warnings.Add($"Ô đỗ '{code}' đang có phương tiện/giữ chỗ và bị đổi tọa độ từ ({fromCell.X},{fromCell.Y}) sang ({toCell.X},{toCell.Y}).");
                    }

                    modifiedSlots.Add(new LayoutSlotDiffDto(
                        Code: code,
                        DiffType: "Modified",
                        OldX: fromCell.X,
                        OldY: fromCell.Y,
                        NewX: toCell.X,
                        NewY: toCell.Y,
                        OldSlotType: fromCell.SlotType?.ToString() ?? "Standard",
                        NewSlotType: toCell.SlotType?.ToString() ?? "Standard",
                        OldMaxVehicleType: fromCell.MaxVehicleType?.ToString() ?? "Suv",
                        NewMaxVehicleType: toCell.MaxVehicleType?.ToString() ?? "Suv",
                        CurrentSlotState: dbSlot?.State.ToString() ?? "Available",
                        HasActiveBooking: isActiveBooking,
                        IsSafetyBlocker: isBlocker,
                        BlockerReason: blockerReason));
                }
            }
        }

        var gridSizeChanged = parsedFrom.Columns != parsedTo.Columns || parsedFrom.Rows != parsedTo.Rows;
        if (gridSizeChanged)
        {
            warnings.Add($"Kích thước lưới thay đổi từ {parsedFrom.Columns}x{parsedFrom.Rows} thành {parsedTo.Columns}x{parsedTo.Rows}.");
        }

        var canSafelyApply = safetyBlockers.Count == 0;

        return new FloorLayoutDiffDto(
            FloorId: floor.Id,
            FromVersionNo: fromVersionNo,
            ToVersionNo: toVersion?.VersionNo,
            GridSizeChanged: gridSizeChanged,
            OldColumns: parsedFrom.Columns,
            OldRows: parsedFrom.Rows,
            NewColumns: parsedTo.Columns,
            NewRows: parsedTo.Rows,
            AddedSlots: addedSlots,
            RemovedSlots: removedSlots,
            ModifiedSlots: modifiedSlots,
            CanSafelyApply: canSafelyApply,
            SafetyBlockers: safetyBlockers,
            Warnings: warnings);
    }

    private static int GetVehicleTypeRank(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Motorbike => 1,
        VehicleType.Sedan => 2,
        VehicleType.Suv => 3,
        VehicleType.Pickup => 4,
        VehicleType.Van => 5,
        VehicleType.Oversized => 6,
        _ => 2
    };

    private static ParsedDiffLayout ParseLayout(string? layoutJson, Floor floor)
    {
        var cols = floor.GridColumns > 0 ? floor.GridColumns : 20;
        var rows = floor.GridRows > 0 ? floor.GridRows : 10;
        var slotCells = new List<LayoutCellInputDto>();

        if (string.IsNullOrWhiteSpace(layoutJson) || layoutJson == "{}")
        {
            return new ParsedDiffLayout(cols, rows, slotCells);
        }

        try
        {
            using var doc = JsonDocument.Parse(layoutJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("columns", out var cp) && cp.TryGetInt32(out var c) && c > 0)
                cols = c;
            if (root.TryGetProperty("rows", out var rp) && rp.TryGetInt32(out var r) && r > 0)
                rows = r;

            if (root.TryGetProperty("cells", out var cellsProp) && cellsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in cellsProp.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var type = el.TryGetProperty("type", out var tp) ? tp.GetString() ?? "slot" : "slot";
                    if (!string.Equals(type, "slot", StringComparison.OrdinalIgnoreCase)) continue;

                    var x = el.TryGetProperty("x", out var xp) && xp.TryGetInt32(out var vx) ? Math.Max(0, vx) : 0;
                    var y = el.TryGetProperty("y", out var yp) && yp.TryGetInt32(out var vy) ? Math.Max(0, vy) : 0;
                    var code = el.TryGetProperty("code", out var cdp) ? cdp.GetString() : null;

                    var w = el.TryGetProperty("widthCells", out var wp) && wp.TryGetInt32(out var vw) && vw > 0 ? vw : 1;
                    var h = el.TryGetProperty("heightCells", out var hp) && hp.TryGetInt32(out var vh) && vh > 0 ? vh : 1;

                    SlotType? slotType = null;
                    if (el.TryGetProperty("slotType", out var stp) && Enum.TryParse<SlotType>(stp.GetString(), true, out var st))
                        slotType = st;

                    VehicleType? vehicleType = null;
                    if (el.TryGetProperty("maxVehicleType", out var mvp) && Enum.TryParse<VehicleType>(mvp.GetString(), true, out var vt))
                        vehicleType = vt;

                    slotCells.Add(new LayoutCellInputDto(x, y, type, code, slotType, vehicleType, w, h));
                }
            }
        }
        catch (JsonException)
        {
        }

        return new ParsedDiffLayout(cols, rows, slotCells);
    }

    private sealed record ParsedDiffLayout(int Columns, int Rows, IReadOnlyList<LayoutCellInputDto> SlotCells);
}
