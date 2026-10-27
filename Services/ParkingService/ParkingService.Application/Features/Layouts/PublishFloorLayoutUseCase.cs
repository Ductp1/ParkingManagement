using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IPublishFloorLayoutUseCase
{
    Task<FloorLayoutDto> ExecuteAsync(PublishFloorLayoutCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xuất bản sơ đồ lưới tầng và kích hoạt Slot Synchronization Engine (US-059).
/// - Kiểm tra an toàn: Tuyệt đối không cho phép xóa ô đỗ đang có xe (Occupied) hoặc đang giữ chỗ (Reserved).
/// - Tự động đồng bộ hóa bảng vật lý Slots: thêm mới, cập nhật tọa độ/loại xe, và soft-delete ô đỗ bị gỡ bỏ.
/// - Cập nhật kích thước Floor.GridColumns/GridRows.
/// - Lưu bản ghi LayoutVersion đã xuất bản với số phiên bản VersionNo mới.
/// </summary>
public sealed class PublishFloorLayoutUseCase(
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<PublishFloorLayoutUseCase> logger) : IPublishFloorLayoutUseCase
{
    public async Task<FloorLayoutDto> ExecuteAsync(PublishFloorLayoutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (command.Columns <= 0 || command.Rows <= 0)
            throw new ValidationException("Kích thước lưới (Columns và Rows) phải lớn hơn 0.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        var cells = command.Cells ?? [];

        // 1. Kiểm tra tính hợp lệ của tọa độ các cell trên lưới
        foreach (var cell in cells)
        {
            var width = cell.WidthCells > 0 ? cell.WidthCells : 1;
            var height = cell.HeightCells > 0 ? cell.HeightCells : 1;
            if (cell.X < 0 || cell.X + width > command.Columns || cell.Y < 0 || cell.Y + height > command.Rows)
            {
                throw new ValidationException(
                    $"Tọa độ ({cell.X}, {cell.Y}) của ô với kích thước {width}x{height} vượt quá kích thước lưới ({command.Columns}x{command.Rows}).");
            }
        }

        // 2. Kiểm tra ô đỗ (slot): Mọi ô đỗ phải có Code và không được trùng lặp mã trong tầng
        var slotCells = cells
            .Where(c => string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var slotCell in slotCells)
        {
            if (string.IsNullOrWhiteSpace(slotCell.Code))
            {
                throw new ValidationException(
                    $"Ô đỗ tại tọa độ ({slotCell.X}, {slotCell.Y}) thiếu mã ô đỗ (Code). Mọi ô đỗ bắt buộc phải có mã khi xuất bản.");
            }
        }

        var duplicateCode = slotCells
            .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateCode != null)
        {
            throw new ValidationException($"Mã ô đỗ '{duplicateCode.Key}' bị trùng lặp trong bản vẽ xuất bản.");
        }

        // 3. KIỂM TRA RÀNG BUỘC AN TOÀN (CRUCIAL SAFETY CONSTRAINT):
        // Lấy tất cả slot hiện có trong DB của tầng này
        var existingDbSlots = await slotRepository.GetByFloorIdAsync(command.FloorId, cancellationToken);
        var activeDbSlots = existingDbSlots.Where(s => s.IsActive).ToList();

        var newLayoutSlotCodes = new HashSet<string>(
            slotCells.Select(c => c.Code!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        // Chặn xóa bất kỳ slot nào đang có xe (Occupied) hoặc đang giữ chỗ (Reserved)
        var protectedSlotRemoved = activeDbSlots
            .FirstOrDefault(s => (s.State == SlotState.Occupied || s.State == SlotState.Reserved)
                                 && !newLayoutSlotCodes.Contains(s.Code.Trim()));

        if (protectedSlotRemoved != null)
        {
            var reason = protectedSlotRemoved.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
            throw new ConflictException(
                $"Không thể xuất bản sơ đồ: Ô đỗ '{protectedSlotRemoved.Code}' {reason} nhưng không có mặt trong bản vẽ mới.");
        }

        // 4. SLOT SYNCHRONIZATION ENGINE (Đồng bộ hóa bảng Slots vật lý)
        var dbSlotByCode = existingDbSlots
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var synchronizedSlots = new List<Slot>();

        foreach (var slotCell in slotCells)
        {
            var code = slotCell.Code!.Trim().ToUpperInvariant();

            if (dbSlotByCode.TryGetValue(code, out var existingSlot))
            {
                // Cập nhật thông số tọa độ, kích thước và loại xe cho slot đã tồn tại
                existingSlot.GridX = slotCell.X;
                existingSlot.GridY = slotCell.Y;
                existingSlot.WidthCells = slotCell.WidthCells > 0 ? slotCell.WidthCells : 1;
                existingSlot.HeightCells = slotCell.HeightCells > 0 ? slotCell.HeightCells : 1;
                if (slotCell.SlotType.HasValue)
                    existingSlot.SlotType = slotCell.SlotType.Value;
                if (slotCell.MaxVehicleType.HasValue)
                    existingSlot.MaxVehicleType = slotCell.MaxVehicleType.Value;

                existingSlot.IsActive = true; // Kích hoạt lại nếu trước đó bị tắt

                await slotRepository.UpdateAsync(existingSlot, cancellationToken);
                synchronizedSlots.Add(existingSlot);
            }
            else
            {
                // Thêm mới slot vào cơ sở dữ liệu
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

        // Vô hiệu hóa (Soft-delete) các ô đỗ cũ không còn xuất hiện trong bản vẽ mới
        foreach (var dbSlot in activeDbSlots)
        {
            if (!newLayoutSlotCodes.Contains(dbSlot.Code.Trim()))
            {
                dbSlot.IsActive = false;
                await slotRepository.UpdateAsync(dbSlot, cancellationToken);
                logger.LogInformation("Vô hiệu hóa Slot {Code} (Id={Id}) trên Floor Id={FloorId} do đã bị gỡ khỏi layout mới.",
                    dbSlot.Code, dbSlot.Id, command.FloorId);
            }
        }

        await slotRepository.SaveChangesAsync(cancellationToken);

        // 5. Cập nhật kích thước lưới của Floor
        if (floor.GridColumns != command.Columns || floor.GridRows != command.Rows)
        {
            floor.GridColumns = command.Columns;
            floor.GridRows = command.Rows;
            await floorRepository.UpdateAsync(floor, cancellationToken);
            await floorRepository.SaveChangesAsync(cancellationToken);
        }

        // 6. Đóng gói JSON lưu trữ LayoutVersion
        var cellsToStore = cells
            .Select(c => new
            {
                x = c.X,
                y = c.Y,
                type = c.Type.ToLowerInvariant(),
                code = !string.IsNullOrWhiteSpace(c.Code) ? c.Code.Trim() : null,
                slotType = c.SlotType?.ToString(),
                maxVehicleType = c.MaxVehicleType?.ToString(),
                widthCells = c.WidthCells > 0 ? c.WidthCells : 1,
                heightCells = c.HeightCells > 0 ? c.HeightCells : 1
            })
            .ToList();

        var payload = new
        {
            columns = command.Columns,
            rows = command.Rows,
            cells = cellsToStore
        };

        var layoutJson = JsonSerializer.Serialize(payload);
        var now = DateTime.UtcNow;

        // Nếu tầng đang có bản nháp dở dang, chuyển bản nháp thành published
        var existingDraft = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(command.FloorId, cancellationToken);
        int versionNo;

        if (existingDraft != null)
        {
            versionNo = existingDraft.VersionNo;
            existingDraft.LayoutJson = layoutJson;
            existingDraft.IsPublished = true;
            existingDraft.PublishedAtUtc = now;
            existingDraft.ChangeNote = command.ChangeNote;
            if (command.UserId > 0)
                existingDraft.CreatedByUserId = command.UserId;

            await layoutVersionRepository.UpdateAsync(existingDraft, cancellationToken);
            logger.LogInformation("Chuyển đổi bản nháp v{VersionNo} thành xuất bản chính thức cho Floor Id={FloorId}", versionNo, command.FloorId);
        }
        else
        {
            versionNo = await layoutVersionRepository.GetNextVersionNoAsync(command.FloorId, cancellationToken);
            var newVersion = new LayoutVersion
            {
                FloorId = command.FloorId,
                VersionNo = versionNo,
                LayoutJson = layoutJson,
                IsPublished = true,
                PublishedAtUtc = now,
                ChangeNote = command.ChangeNote,
                CreatedByUserId = command.UserId
            };

            await layoutVersionRepository.AddAsync(newVersion, cancellationToken);
            logger.LogInformation("Tạo mới LayoutVersion v{VersionNo} xuất bản cho Floor Id={FloorId}", versionNo, command.FloorId);
        }

        await layoutVersionRepository.SaveChangesAsync(cancellationToken);

        // 7. Tạo FloorLayoutDto trả về cho Client
        var syncSlotDict = synchronizedSlots
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var returnedCells = cells
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
            VersionNo: versionNo,
            IsPublished: true,
            PublishedAtUtc: now,
            Columns: command.Columns,
            Rows: command.Rows,
            Cells: returnedCells);
    }
}
