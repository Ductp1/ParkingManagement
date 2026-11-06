using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IRollbackFloorLayoutUseCase
{
    Task<FloorLayoutDto> ExecuteAsync(RollbackFloorLayoutCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Khôi phục sơ đồ tầng về phiên bản lịch sử TargetVersionNo và kích hoạt Slot Synchronization Engine (US-060).
/// - Bảo vệ tuyệt đối: Chặn 409 Conflict nếu có ô đỗ đang có xe (Occupied) hoặc giữ chỗ (Reserved) bị xóa khỏi bản vẽ cũ hoặc bị hạ cấp loại xe.
/// - Đồng bộ hóa bảng vật lý Slots: Cập nhật tọa độ, kích thước, khôi phục IsActive = true, và soft-delete an toàn các ô không còn trong phiên bản cũ.
/// - Cập nhật kích thước lưới Floor (GridColumns, GridRows).
/// - Dọn dẹp bản nháp dở dang nếu có.
/// - Tạo bản ghi LayoutVersion đã xuất bản mới với VersionNo = max + 1 (Append-only versioning để bảo toàn lịch sử).
/// </summary>
public sealed class RollbackFloorLayoutUseCase(
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<RollbackFloorLayoutUseCase> logger) : IRollbackFloorLayoutUseCase
{
    public async Task<FloorLayoutDto> ExecuteAsync(RollbackFloorLayoutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (command.TargetVersionNo <= 0)
            throw new ValidationException("Số phiên bản mục tiêu (TargetVersionNo) phải lớn hơn 0.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        var targetVersion = await layoutVersionRepository.GetByFloorIdAndVersionAsync(command.FloorId, command.TargetVersionNo, cancellationToken)
            ?? throw new NotFoundException($"Phiên bản sơ đồ v{command.TargetVersionNo} của tầng", command.FloorId);

        // 1. Phân tích LayoutJson của phiên bản mục tiêu
        var parsedLayout = ParseLayoutJson(targetVersion.LayoutJson, floor);

        // 2. RÀNG BUỘC AN TOÀN BẢO VỆ BOOKING ACTIVE (US-060)
        var existingDbSlots = await slotRepository.GetByFloorIdAsync(command.FloorId, cancellationToken);
        var activeDbSlots = existingDbSlots.Where(s => s.IsActive).ToList();

        var targetSlotMap = parsedLayout.SlotCells
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 2a. Chặn khôi phục nếu ô đỗ đang Occupied hoặc Reserved bị thiếu trong bản vẽ cũ
        var protectedSlotMissing = activeDbSlots
            .FirstOrDefault(s => (s.State == SlotState.Occupied || s.State == SlotState.Reserved)
                                 && !targetSlotMap.ContainsKey(s.Code.Trim()));

        if (protectedSlotMissing != null)
        {
            var reason = protectedSlotMissing.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
            throw new ConflictException(
                $"Không thể khôi phục phiên bản v{command.TargetVersionNo}: Ô đỗ '{protectedSlotMissing.Code}' {reason} nhưng không có mặt trong phiên bản này.");
        }

        // 2b. Chặn khôi phục nếu ô đỗ đang Occupied/Reserved bị hạ cấp loại xe (VehicleType Downgrade)
        foreach (var activeSlot in activeDbSlots.Where(s => s.State == SlotState.Occupied || s.State == SlotState.Reserved))
        {
            if (targetSlotMap.TryGetValue(activeSlot.Code.Trim(), out var targetCell))
            {
                var targetVehicleType = targetCell.MaxVehicleType ?? VehicleType.Suv;
                if (GetVehicleTypeRank(targetVehicleType) < GetVehicleTypeRank(activeSlot.MaxVehicleType))
                {
                    var reason = activeSlot.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
                    throw new ConflictException(
                        $"Không thể khôi phục phiên bản v{command.TargetVersionNo}: Ô đỗ '{activeSlot.Code}' {reason} " +
                        $"nhưng phiên bản v{command.TargetVersionNo} hạ cấp loại xe tối đa từ '{activeSlot.MaxVehicleType}' xuống '{targetVehicleType}'.");
                }
            }
        }

        // 3. KÍCH HOẠT SLOT SYNCHRONIZATION ENGINE
        var dbSlotByCode = existingDbSlots
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var synchronizedSlots = new List<Slot>();

        foreach (var slotCell in parsedLayout.SlotCells)
        {
            var code = slotCell.Code!.Trim().ToUpperInvariant();

            if (dbSlotByCode.TryGetValue(code, out var existingSlot))
            {
                existingSlot.GridX = slotCell.X;
                existingSlot.GridY = slotCell.Y;
                existingSlot.WidthCells = slotCell.WidthCells > 0 ? slotCell.WidthCells : 1;
                existingSlot.HeightCells = slotCell.HeightCells > 0 ? slotCell.HeightCells : 1;
                if (slotCell.SlotType.HasValue)
                    existingSlot.SlotType = slotCell.SlotType.Value;
                if (slotCell.MaxVehicleType.HasValue)
                    existingSlot.MaxVehicleType = slotCell.MaxVehicleType.Value;

                existingSlot.IsActive = true;

                await slotRepository.UpdateAsync(existingSlot, cancellationToken);
                synchronizedSlots.Add(existingSlot);
            }
            else
            {
                var newSlot = new Slot
                {
                    FloorId = command.FloorId,
                    Code = code,
                    GridX = slotCell.X,
                    GridY = slotCell.Y,
                    WidthCells = slotCell.WidthCells > 0 ? slotCell.WidthCells : 1,
                    HeightCells = slotCell.HeightCells > 0 ? slotCell.HeightCells : 1,
                    SlotType = slotCell.SlotType ?? SlotType.Standard,
                    MaxVehicleType = slotCell.MaxVehicleType ?? VehicleType.Suv,
                    State = SlotState.Available,
                    IsActive = true
                };

                await slotRepository.AddAsync(newSlot, cancellationToken);
                synchronizedSlots.Add(newSlot);
            }
        }

        // Vô hiệu hóa (Soft-delete) an toàn các slot hiện có không nằm trong bản vẽ cũ
        var targetCodes = new HashSet<string>(targetSlotMap.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var dbSlot in activeDbSlots)
        {
            if (!targetCodes.Contains(dbSlot.Code.Trim()))
            {
                dbSlot.IsActive = false;
                await slotRepository.UpdateAsync(dbSlot, cancellationToken);
                logger.LogInformation("Vô hiệu hóa an toàn Slot {Code} (Id={Id}) khi rollback về v{VersionNo}",
                    dbSlot.Code, dbSlot.Id, command.TargetVersionNo);
            }
        }

        await slotRepository.SaveChangesAsync(cancellationToken);

        // 4. CẬP NHẬT KÍCH THƯỚC LƯỚI FLOOR NẾU THAY ĐỔI
        if (floor.GridColumns != parsedLayout.Columns || floor.GridRows != parsedLayout.Rows)
        {
            floor.GridColumns = parsedLayout.Columns;
            floor.GridRows = parsedLayout.Rows;
            await floorRepository.UpdateAsync(floor, cancellationToken);
            await floorRepository.SaveChangesAsync(cancellationToken);
        }

        // 5. DỌN DẸP BẢN NHÁP DỞ DANG NẾU CÓ
        var existingDraft = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(command.FloorId, cancellationToken);
        if (existingDraft != null)
        {
            await layoutVersionRepository.DeleteAsync(existingDraft, cancellationToken);
            logger.LogInformation("Đã xóa bản nháp dở dang của Floor Id={FloorId} sau khi thực hiện rollback", command.FloorId);
        }

        // 6. TẠO PHIÊN BẢN MỚI ĐÃ XUẤT BẢN (APPEND-ONLY VERSIONING)
        var newVersionNo = await layoutVersionRepository.GetNextVersionNoAsync(command.FloorId, cancellationToken);
        var now = DateTime.UtcNow;
        var changeNote = string.IsNullOrWhiteSpace(command.ChangeNote)
            ? $"Khôi phục từ phiên bản v{command.TargetVersionNo}"
            : command.ChangeNote;

        var newPublishedVersion = new LayoutVersion
        {
            FloorId = command.FloorId,
            VersionNo = newVersionNo,
            LayoutJson = targetVersion.LayoutJson,
            IsPublished = true,
            PublishedAtUtc = now,
            CreatedByUserId = command.UserId,
            ChangeNote = changeNote
        };

        await layoutVersionRepository.AddAsync(newPublishedVersion, cancellationToken);
        await layoutVersionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rollback thành công sơ đồ Floor Id={FloorId} về nội dung v{TargetVersion}, xuất bản dưới v{NewVersion}",
            command.FloorId, command.TargetVersionNo, newVersionNo);

        // 7. TRẢ VỀ DTO KÈM TRẠNG THÁI REALTIME
        var syncSlotDict = synchronizedSlots
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var returnedCells = parsedLayout.AllCells
            .Select(c =>
            {
                var isSlot = string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase);
                string? state = null;
                string? code = !string.IsNullOrWhiteSpace(c.Code) ? c.Code.Trim() : null;

                if (isSlot && code != null && syncSlotDict.TryGetValue(code, out var s))
                {
                    state = s.State.ToString();
                }

                return new LayoutCellDto(
                    X: c.X,
                    Y: c.Y,
                    Type: c.Type.ToLowerInvariant(),
                    Code: code,
                    SlotType: c.SlotType?.ToString(),
                    MaxVehicleType: c.MaxVehicleType?.ToString(),
                    State: state,
                    WidthCells: c.WidthCells > 0 ? c.WidthCells : 1,
                    HeightCells: c.HeightCells > 0 ? c.HeightCells : 1);
            })
            .ToList();

        return new FloorLayoutDto(
            FloorId: floor.Id,
            FloorName: floor.Name,
            Level: floor.Level,
            VersionNo: newVersionNo,
            IsPublished: true,
            PublishedAtUtc: now,
            Columns: parsedLayout.Columns,
            Rows: parsedLayout.Rows,
            Cells: returnedCells);
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

    private static ParsedLayoutResult ParseLayoutJson(string layoutJson, Floor floor)
    {
        var cols = floor.GridColumns > 0 ? floor.GridColumns : 20;
        var rows = floor.GridRows > 0 ? floor.GridRows : 10;
        var allCells = new List<LayoutCellInputDto>();

        if (string.IsNullOrWhiteSpace(layoutJson) || layoutJson == "{}")
        {
            return new ParsedLayoutResult(cols, rows, allCells, []);
        }

        try
        {
            using var doc = JsonDocument.Parse(layoutJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("columns", out var cProp) && cProp.TryGetInt32(out var c) && c > 0)
                cols = c;
            if (root.TryGetProperty("rows", out var rProp) && rProp.TryGetInt32(out var r) && r > 0)
                rows = r;

            if (root.TryGetProperty("cells", out var cellsProp) && cellsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in cellsProp.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var x = el.TryGetProperty("x", out var xp) && xp.TryGetInt32(out var vx) ? Math.Max(0, vx) : 0;
                    var y = el.TryGetProperty("y", out var yp) && yp.TryGetInt32(out var vy) ? Math.Max(0, vy) : 0;
                    var type = el.TryGetProperty("type", out var tp) ? tp.GetString() ?? "slot" : "slot";
                    var code = el.TryGetProperty("code", out var cdp) ? cdp.GetString() : null;

                    var w = el.TryGetProperty("widthCells", out var wp) && wp.TryGetInt32(out var vw) && vw > 0 ? vw : 1;
                    var h = el.TryGetProperty("heightCells", out var hp) && hp.TryGetInt32(out var vh) && vh > 0 ? vh : 1;

                    SlotType? slotType = null;
                    if (el.TryGetProperty("slotType", out var stp) && Enum.TryParse<SlotType>(stp.GetString(), true, out var st))
                        slotType = st;

                    VehicleType? vehicleType = null;
                    if (el.TryGetProperty("maxVehicleType", out var mvp) && Enum.TryParse<VehicleType>(mvp.GetString(), true, out var vt))
                        vehicleType = vt;

                    allCells.Add(new LayoutCellInputDto(x, y, type, code, slotType, vehicleType, w, h));
                }
            }
        }
        catch (JsonException)
        {
            // Bỏ qua lỗi JSON hỏng, trả về danh sách rỗng
        }

        var slotCells = allCells
            .Where(c => string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ParsedLayoutResult(cols, rows, allCells, slotCells);
    }

    private sealed record ParsedLayoutResult(
        int Columns,
        int Rows,
        IReadOnlyList<LayoutCellInputDto> AllCells,
        IReadOnlyList<LayoutCellInputDto> SlotCells);
}
