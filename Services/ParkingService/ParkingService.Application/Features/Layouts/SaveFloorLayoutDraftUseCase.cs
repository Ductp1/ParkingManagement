using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface ISaveFloorLayoutDraftUseCase
{
    Task<FloorLayoutDto> ExecuteAsync(SaveFloorLayoutDraftCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lưu bản vẽ sơ đồ dạng nháp (Draft) cho tầng (US-059).
/// Bản nháp được lưu riêng vào bảng LayoutVersions với IsPublished = false và
/// HOÀN TOÀN KHÔNG làm thay đổi bảng vật lý Slots, giúp chủ bãi biên tập mà không ảnh hưởng vận hành.
/// </summary>
public sealed class SaveFloorLayoutDraftUseCase(
    IFloorRepository floorRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<SaveFloorLayoutDraftUseCase> logger) : ISaveFloorLayoutDraftUseCase
{
    public async Task<FloorLayoutDto> ExecuteAsync(SaveFloorLayoutDraftCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (command.Columns <= 0 || command.Rows <= 0)
            throw new ValidationException("Kích thước lưới (Columns và Rows) phải lớn hơn 0.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        // Kiểm tra tính hợp lệ của tọa độ các cell trên lưới
        if (command.Cells != null)
        {
            foreach (var cell in command.Cells)
            {
                var width = cell.WidthCells > 0 ? cell.WidthCells : 1;
                var height = cell.HeightCells > 0 ? cell.HeightCells : 1;
                if (cell.X < 0 || cell.X + width > command.Columns || cell.Y < 0 || cell.Y + height > command.Rows)
                {
                    throw new ValidationException(
                        $"Tọa độ ({cell.X}, {cell.Y}) của ô với kích thước {width}x{height} vượt quá kích thước lưới ({command.Columns}x{command.Rows}).");
                }
            }

            // Kiểm tra trùng lặp mã ô đỗ trong bản vẽ
            var slotCells = command.Cells
                .Where(c => string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Code))
                .ToList();

            var duplicateCode = slotCells
                .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateCode != null)
            {
                throw new ValidationException($"Mã ô đỗ '{duplicateCode.Key}' bị trùng lặp trong bản vẽ.");
            }
        }

        // Đóng gói cấu trúc JSON lưu trữ
        var cellsToStore = (command.Cells ?? [])
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

        // Tìm bản nháp chưa publish hiện có hoặc tạo mới
        var existingDraft = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(command.FloorId, cancellationToken);
        int versionNo;

        if (existingDraft != null)
        {
            versionNo = existingDraft.VersionNo;
            existingDraft.LayoutJson = layoutJson;
            existingDraft.ChangeNote = command.ChangeNote;
            if (command.UserId > 0)
                existingDraft.CreatedByUserId = command.UserId;

            await layoutVersionRepository.UpdateAsync(existingDraft, cancellationToken);
            logger.LogInformation("Cập nhật bản nháp sơ đồ v{VersionNo} cho Floor Id={FloorId}", versionNo, command.FloorId);
        }
        else
        {
            versionNo = await layoutVersionRepository.GetNextVersionNoAsync(command.FloorId, cancellationToken);
            var newDraft = new LayoutVersion
            {
                FloorId = command.FloorId,
                VersionNo = versionNo,
                LayoutJson = layoutJson,
                IsPublished = false,
                PublishedAtUtc = null,
                ChangeNote = command.ChangeNote,
                CreatedByUserId = command.UserId
            };

            await layoutVersionRepository.AddAsync(newDraft, cancellationToken);
            logger.LogInformation("Tạo mới bản nháp sơ đồ v{VersionNo} cho Floor Id={FloorId}", versionNo, command.FloorId);
        }

        await layoutVersionRepository.SaveChangesAsync(cancellationToken);

        var returnedCells = (command.Cells ?? [])
            .Select(c => new LayoutCellDto(
                X: c.X,
                Y: c.Y,
                Type: c.Type.ToLowerInvariant(),
                Code: !string.IsNullOrWhiteSpace(c.Code) ? c.Code.Trim() : null,
                SlotType: c.SlotType?.ToString(),
                MaxVehicleType: c.MaxVehicleType?.ToString(),
                State: string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase) ? "Draft" : null,
                WidthCells: c.WidthCells > 0 ? c.WidthCells : 1,
                HeightCells: c.HeightCells > 0 ? c.HeightCells : 1))
            .ToList();

        return new FloorLayoutDto(
            FloorId: floor.Id,
            FloorName: floor.Name,
            Level: floor.Level,
            VersionNo: versionNo,
            IsPublished: false,
            PublishedAtUtc: null,
            Columns: command.Columns,
            Rows: command.Rows,
            Cells: returnedCells);
    }
}
