using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.Layouts;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class FloorLayoutTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetFloorLayout_WithInvalidFloorId_ThrowsValidationException(int floorId)
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(floorId));
    }

    [Fact]
    public async Task GetFloorLayout_WhenFloorNotFound_ThrowsNotFoundException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task GetFloorLayout_WhenPublishedLayoutExists_ReturnsLayoutAndEnrichesRealtimeSlotStates()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng B1",
            Level = -1,
            GridColumns = 20,
            GridRows = 10
        }, 1);
        await floorRepo.AddAsync(floor);

        // Ô đỗ trong DB có trạng thái realtime: A-01 đang Occupied, A-02 đang Reserved
        var slot1 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Occupied,
            IsActive = true
        }, 101);

        var slot2 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-02",
            GridX = 1,
            GridY = 0,
            SlotType = SlotType.EvCharging,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Reserved,
            IsActive = true
        }, 102);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Layout version đã xuất bản (lưu cell tĩnh nhưng không có trạng thái realtime)
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = "A-01" },
                new { x = 1, y = 0, type = "slot", code = "A-02" },
                new { x = 0, y = 1, type = "road" },
                new { x = 19, y = 9, type = "exit" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        // Act
        var result = await useCase.ExecuteAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FloorId);
        Assert.Equal("Tầng B1", result.FloorName);
        Assert.Equal(-1, result.Level);
        Assert.Equal(1, result.VersionNo);
        Assert.True(result.IsPublished);
        Assert.NotNull(result.PublishedAtUtc);
        Assert.Equal(20, result.Columns);
        Assert.Equal(10, result.Rows);
        Assert.Equal(4, result.Cells.Count);

        var cellA01 = result.Cells.FirstOrDefault(c => c.Code == "A-01");
        Assert.NotNull(cellA01);
        Assert.Equal("slot", cellA01.Type);
        Assert.Equal("Occupied", cellA01.State); // Đã được enrich trạng thái thực tế từ DB

        var cellA02 = result.Cells.FirstOrDefault(c => c.Code == "A-02");
        Assert.NotNull(cellA02);
        Assert.Equal("slot", cellA02.Type);
        Assert.Equal("Reserved", cellA02.State); // Đã được enrich trạng thái thực tế từ DB
        Assert.Equal("EvCharging", cellA02.SlotType);

        var roadCell = result.Cells.FirstOrDefault(c => c.Type == "road");
        Assert.NotNull(roadCell);
        Assert.Equal(0, roadCell.X);
        Assert.Equal(1, roadCell.Y);

        var exitCell = result.Cells.FirstOrDefault(c => c.Type == "exit");
        Assert.NotNull(exitCell);
        Assert.Equal(19, exitCell.X);
        Assert.Equal(9, exitCell.Y);
    }

    [Fact]
    public async Task GetFloorLayout_WhenNoLayoutVersionExists_SynthesizesLayoutFromDbSlots()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng Trệt",
            Level = 0,
            GridColumns = 15,
            GridRows = 8
        }, 2);
        await floorRepo.AddAsync(floor);

        // Tầng chưa có LayoutVersion nào, nhưng đã có 2 slots vật lý
        var slot1 = WithId(new Slot
        {
            FloorId = 2,
            Code = "MD-01",
            GridX = 2,
            GridY = 3,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Sedan,
            State = SlotState.Available,
            IsActive = true
        }, 201);

        var slot2 = WithId(new Slot
        {
            FloorId = 2,
            Code = "MD-02",
            GridX = 4,
            GridY = 5,
            SlotType = SlotType.Vip,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Maintenance,
            IsActive = true
        }, 202);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Act
        var result = await useCase.ExecuteAsync(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.FloorId);
        Assert.Equal("Tầng Trệt", result.FloorName);
        Assert.Equal(0, result.Level);
        Assert.Equal(0, result.VersionNo);
        Assert.False(result.IsPublished);
        Assert.Null(result.PublishedAtUtc);
        Assert.Equal(15, result.Columns);
        Assert.Equal(8, result.Rows);
        Assert.Equal(2, result.Cells.Count);

        var cell1 = result.Cells.FirstOrDefault(c => c.Code == "MD-01");
        Assert.NotNull(cell1);
        Assert.Equal(2, cell1.X);
        Assert.Equal(3, cell1.Y);
        Assert.Equal("Available", cell1.State);

        var cell2 = result.Cells.FirstOrDefault(c => c.Code == "MD-02");
        Assert.NotNull(cell2);
        Assert.Equal(4, cell2.X);
        Assert.Equal(5, cell2.Y);
        Assert.Equal("Vip", cell2.SlotType);
        Assert.Equal("Maintenance", cell2.State);
    }

    [Fact]
    public async Task GetFloorLayout_WhenLayoutJsonIsCorrupted_GracefullyFallsBackToSynthesis()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor
        {
            ZoneId = 1,
            Name = "Tầng 2",
            Level = 2,
            GridColumns = 10,
            GridRows = 10
        }, 3);
        await floorRepo.AddAsync(floor);

        var slot = WithId(new Slot
        {
            FloorId = 3,
            Code = "T2-01",
            GridX = 1,
            GridY = 1,
            State = SlotState.Available,
            IsActive = true
        }, 301);
        await slotRepo.AddAsync(slot);

        // LayoutVersion chứa JSON lỗi
        var brokenLayout = WithId(new LayoutVersion
        {
            FloorId = 3,
            VersionNo = 1,
            IsPublished = true,
            LayoutJson = "BROKEN_CORRUPTED_JSON{{"
        }, 10);
        await layoutRepo.AddAsync(brokenLayout);

        // Act - Không bị ném lỗi mà tự động fallback synthesis
        var result = await useCase.ExecuteAsync(3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.VersionNo);
        Assert.False(result.IsPublished);
        Assert.Single(result.Cells);
        Assert.Equal("T2-01", result.Cells[0].Code);
    }

    // ==================== NEW TESTS ADDRESSING REVIEW COMMENTS ====================

    [Fact]
    public async Task GetFloorLayout_WhenSlotInDbIsInactive_SetsStateToUnavailable()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Slot A-01 trong DB bị inactive (IsActive = false)
        var inactiveSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            State = SlotState.Available, // dù State là Available nhưng IsActive = false
            IsActive = false
        }, 101);
        await slotRepo.AddAsync(inactiveSlot);

        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = "A-01" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        var cell = result.Cells.FirstOrDefault(c => c.Code == "A-01");
        Assert.NotNull(cell);
        Assert.Equal("Unavailable", cell.State); // Tuyệt đối không được là Available
    }

    [Fact]
    public async Task GetFloorLayout_WhenCellHasNoMatchingDbSlot_SetsStateToUnavailable()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Không có slot nào trong DB cả, nhưng layout JSON có 1 cell slot "PHANTOM-99"
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 5, y = 5, type = "slot", code = "PHANTOM-99" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        var cell = result.Cells.FirstOrDefault(c => c.Code == "PHANTOM-99");
        Assert.NotNull(cell);
        Assert.Equal("Unavailable", cell.State); // Phantom slot phải là Unavailable
    }

    [Fact]
    public async Task GetFloorLayout_WhenSlotCellLacksCode_DoesNotMatchByCoordinatesAndSetsUnavailable()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Trong DB có slot "REAL-01" tại tọa độ (0, 0)
        var realSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "REAL-01",
            GridX = 0,
            GridY = 0,
            State = SlotState.Occupied,
            IsActive = true
        }, 101);
        await slotRepo.AddAsync(realSlot);

        // Bản vẽ có 1 cell slot tại (0, 0) nhưng code = null hoặc rỗng
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = (string?)null }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        // Cell tại (0, 0) không được tự ý gán code "REAL-01"
        var cellAtZero = result.Cells.FirstOrDefault(c => c.X == 0 && c.Y == 0 && c.Code == null);
        Assert.NotNull(cellAtZero);
        Assert.Null(cellAtZero.Code);
        Assert.Equal("Unavailable", cellAtZero.State);

        // Do "REAL-01" trong DB chưa được map bởi layout cell nào, stale layout handler tự động append nó
        var realCell = result.Cells.FirstOrDefault(c => c.Code == "REAL-01");
        Assert.NotNull(realCell);
        Assert.Equal("Occupied", realCell.State);
    }

    [Fact]
    public async Task GetFloorLayout_WhenSingleCellIsMalformed_PreservesOtherCellsAndLayoutStructure()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        var validSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "GOOD-01",
            GridX = 1,
            GridY = 1,
            State = SlotState.Available,
            IsActive = true
        }, 101);
        await slotRepo.AddAsync(validSlot);

        // LayoutJson có 1 cell hợp lệ GOOD-01, 1 cell road hợp lệ, 1 cell exit hợp lệ, và 1 cell malformed (không phải object)
        var layoutJson = """
        {
            "columns": 20,
            "rows": 10,
            "cells": [
                { "x": 1, "y": 1, "type": "slot", "code": "GOOD-01" },
                "MALFORMED_NON_OBJECT_CELL",
                { "x": 0, "y": 2, "type": "road" },
                { "x": 19, "y": 9, "type": "exit" }
            ]
        }
        """;

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 2,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        // Không bị ném lỗi, không bị vứt bỏ bản vẽ về VersionNo = 0
        Assert.Equal(2, result.VersionNo);
        Assert.True(result.IsPublished);
        Assert.Equal(3, result.Cells.Count); // 3 cells hợp lệ được giữ nguyên vẹn

        Assert.Contains(result.Cells, c => c.Code == "GOOD-01" && c.State == "Available");
        Assert.Contains(result.Cells, c => c.Type == "road");
        Assert.Contains(result.Cells, c => c.Type == "exit");
    }

    [Fact]
    public async Task GetFloorLayout_WhenPublishedLayoutIsStale_AppendsUnmappedActiveSlots()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Slot A-01 có trong layout
        var slot1 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            State = SlotState.Available,
            IsActive = true
        }, 101);

        // Slot B-01 mới thêm vào DB sau khi publish (stale layout)
        var slot2 = WithId(new Slot
        {
            FloorId = 1,
            Code = "B-01",
            GridX = 2,
            GridY = 3,
            SlotType = SlotType.EvCharging,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Occupied,
            IsActive = true
        }, 102);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Layout chỉ vẽ A-01
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = "A-01" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        Assert.Equal(2, result.Cells.Count);
        var cellA01 = result.Cells.FirstOrDefault(c => c.Code == "A-01");
        var cellB01 = result.Cells.FirstOrDefault(c => c.Code == "B-01");

        Assert.NotNull(cellA01);
        Assert.NotNull(cellB01);
        Assert.Equal("Occupied", cellB01.State);
        Assert.Equal("EvCharging", cellB01.SlotType);
        Assert.Equal(2, cellB01.X);
        Assert.Equal(3, cellB01.Y);
    }

    [Fact]
    public async Task GetFloorLayout_WhenCellsExceedDeclaredBounds_ClampsColumnsAndRowsToEncompassAllCells()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Layout khai báo columns = 10, rows = 10, nhưng cell lại đặt ở x = 15, y = 12 với width = 2, height = 1
        var layoutJson = JsonSerializer.Serialize(new
        {
            columns = 10,
            rows = 10,
            cells = new object[]
            {
                new { x = 15, y = 12, widthCells = 2, heightCells = 1, type = "road" }
            }
        });

        var layoutVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            LayoutJson = layoutJson
        }, 1);
        await layoutRepo.AddAsync(layoutVersion);

        var result = await useCase.ExecuteAsync(1);

        // Clamping: columns phải ít nhất là 15 + 2 = 17, rows ít nhất là 12 + 1 = 13
        Assert.True(result.Columns >= 17, $"Columns ({result.Columns}) phải >= 17");
        Assert.True(result.Rows >= 13, $"Rows ({result.Rows}) phải >= 13");
    }

    // ==================== US-059: DRAFT LAYOUT TESTS ====================

    [Fact]
    public async Task GetFloorLayoutDraft_WhenDraftExists_ReturnsDraftLayout()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var getLayoutUseCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);
        var useCase = new GetFloorLayoutDraftUseCase(floorRepo, layoutRepo, slotRepo, getLayoutUseCase, NullLogger<GetFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        var draftJson = JsonSerializer.Serialize(new
        {
            columns = 20,
            rows = 10,
            cells = new object[]
            {
                new { x = 0, y = 0, type = "slot", code = "A-01", slotType = "Standard", maxVehicleType = "Suv", widthCells = 1, heightCells = 1 },
                new { x = 1, y = 0, type = "road", widthCells = 1, heightCells = 1 }
            }
        });

        var draft = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 2,
            IsPublished = false,
            LayoutJson = draftJson,
            ChangeNote = "Đang thử nghiệm phân vùng xe điện",
            CreatedByUserId = 42
        }, 10);
        await layoutRepo.AddAsync(draft);

        var result = await useCase.ExecuteAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.FloorId);
        Assert.Equal("Tầng B1", result.FloorName);
        Assert.False(result.IsPublished);
        Assert.Null(result.PublishedAtUtc);
        Assert.Equal(2, result.VersionNo);
        Assert.Equal(2, result.Cells.Count);
        Assert.Contains(result.Cells, c => c.Code == "A-01" && c.Type == "slot");
        Assert.Contains(result.Cells, c => c.Type == "road");
    }

    [Fact]
    public async Task GetFloorLayoutDraft_WhenNoDraftExists_ReturnsPublishedOrSynthesizedBaselineWithIsPublishedFalse()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var getLayoutUseCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);
        var useCase = new GetFloorLayoutDraftUseCase(floorRepo, layoutRepo, slotRepo, getLayoutUseCase, NullLogger<GetFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 15, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        var publishedJson = JsonSerializer.Serialize(new
        {
            columns = 15,
            rows = 10,
            cells = new object[]
            {
                new { x = 2, y = 2, type = "slot", code = "S-01", slotType = "Standard", maxVehicleType = "Sedan", widthCells = 1, heightCells = 1 }
            }
        });

        var publishedVersion = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-1),
            LayoutJson = publishedJson,
            CreatedByUserId = 10
        }, 1);
        await layoutRepo.AddAsync(publishedVersion);

        var result = await useCase.ExecuteAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.FloorId);
        Assert.False(result.IsPublished, "Bản nháp trả về cho Layout Configurator phải có IsPublished = false");
        Assert.Null(result.PublishedAtUtc);
        Assert.Equal(1, result.VersionNo);
        Assert.Single(result.Cells);
        Assert.Equal("S-01", result.Cells[0].Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetFloorLayoutDraft_WithInvalidFloorId_ThrowsValidationException(int floorId)
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var getLayoutUseCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);
        var useCase = new GetFloorLayoutDraftUseCase(floorRepo, layoutRepo, slotRepo, getLayoutUseCase, NullLogger<GetFloorLayoutDraftUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(floorId));
    }

    [Fact]
    public async Task GetFloorLayoutDraft_WhenFloorNotFound_ThrowsNotFoundException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var getLayoutUseCase = new GetFloorLayoutUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutUseCase>.Instance);
        var useCase = new GetFloorLayoutDraftUseCase(floorRepo, layoutRepo, slotRepo, getLayoutUseCase, NullLogger<GetFloorLayoutDraftUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task SaveFloorLayoutDraft_WithValidData_CreatesNewDraftWithoutModifyingSlots()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var saveDraftUseCase = new SaveFloorLayoutDraftUseCase(floorRepo, layoutRepo, NullLogger<SaveFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Bảng Slots hiện có 1 slot A-01 ở tọa độ (0, 0)
        var existingSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            WidthCells = 1,
            HeightCells = 1,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Suv,
            State = SlotState.Available,
            IsActive = true
        }, 101);
        await slotRepo.AddAsync(existingSlot);

        // Bản draft dự định dời A-01 sang (5, 5) và thêm slot mới A-02 ở (6, 5)
        var command = new SaveFloorLayoutDraftCommand(
            FloorId: 1,
            Columns: 20,
            Rows: 10,
            Cells:
            [
                new LayoutCellInputDto(5, 5, "slot", "A-01", SlotType.Standard, VehicleType.Suv, 1, 1),
                new LayoutCellInputDto(6, 5, "slot", "A-02", SlotType.EvCharging, VehicleType.Suv, 1, 1),
                new LayoutCellInputDto(0, 0, "road", WidthCells: 1, HeightCells: 1)
            ],
            ChangeNote: "Thử nghiệm đổi chỗ A-01",
            UserId: 15);

        var result = await saveDraftUseCase.ExecuteAsync(command);

        // Kiểm tra kết quả trả về của bản draft
        Assert.NotNull(result);
        Assert.False(result.IsPublished);
        Assert.Equal(3, result.Cells.Count);

        // Kiểm tra CSDL bản nháp được lưu kèm ChangeNote và CreatedByUserId
        var savedDraft = await layoutRepo.GetLatestDraftByFloorIdAsync(1);
        Assert.NotNull(savedDraft);
        Assert.False(savedDraft.IsPublished);
        Assert.Equal("Thử nghiệm đổi chỗ A-01", savedDraft.ChangeNote);
        Assert.Equal(15, savedDraft.CreatedByUserId);

        // QUAN TRỌNG: Bảng Slots tuyệt đối không bị thay đổi khi lưu nháp!
        var slotsInDb = await slotRepo.GetByFloorIdAsync(1);
        Assert.Single(slotsInDb);
        var slotInDb = slotsInDb[0];
        Assert.Equal("A-01", slotInDb.Code);
        Assert.Equal(0, slotInDb.GridX); // Vẫn là tọa độ cũ (0, 0), chưa bị đổi thành (5, 5)
        Assert.Equal(0, slotInDb.GridY);
        Assert.DoesNotContain(slotsInDb, s => s.Code == "A-02"); // A-02 chưa hề xuất hiện trong bảng Slots
    }

    [Fact]
    public async Task SaveFloorLayoutDraft_WhenDraftAlreadyExists_UpdatesExistingDraftWithoutCreatingDuplicate()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var saveDraftUseCase = new SaveFloorLayoutDraftUseCase(floorRepo, layoutRepo, NullLogger<SaveFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng B1", Level = -1, GridColumns = 20, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Đã có 1 bản draft trước đó
        var oldDraft = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = false,
            LayoutJson = "{\"columns\":20,\"rows\":10,\"cells\":[]}",
            ChangeNote = "Bản nháp đợt 1",
            CreatedByUserId = 10
        }, 5);
        await layoutRepo.AddAsync(oldDraft);

        var command = new SaveFloorLayoutDraftCommand(
            FloorId: 1,
            Columns: 20,
            Rows: 10,
            Cells: [new LayoutCellInputDto(1, 1, "wall", WidthCells: 1, HeightCells: 1)],
            ChangeNote: "Bản nháp đợt 2 - Cập nhật thêm tường",
            UserId: 20);

        var result = await saveDraftUseCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.False(result.IsPublished);

        var allVersions = await layoutRepo.GetAllVersionsByFloorIdAsync(1);
        Assert.Single(allVersions); // Không sinh thêm bản ghi draft trùng lặp
        Assert.Equal("Bản nháp đợt 2 - Cập nhật thêm tường", allVersions[0].ChangeNote);
        Assert.Equal(20, allVersions[0].CreatedByUserId);
    }

    [Fact]
    public async Task SaveFloorLayoutDraft_WhenCoordinatesOutOfBounds_ThrowsValidationException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var saveDraftUseCase = new SaveFloorLayoutDraftUseCase(floorRepo, layoutRepo, NullLogger<SaveFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Cell vượt ngoài lưới: X=9, WidthCells=2 (9 + 2 = 11 > Columns 10)
        var command = new SaveFloorLayoutDraftCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells: [new LayoutCellInputDto(9, 5, "slot", "A-01", SlotType.Standard, VehicleType.Suv, WidthCells: 2, HeightCells: 1)]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => saveDraftUseCase.ExecuteAsync(command));
        Assert.Contains("vượt quá kích thước lưới", ex.Message);
    }

    [Fact]
    public async Task SaveFloorLayoutDraft_WhenDuplicateSlotCodesExist_ThrowsValidationException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var saveDraftUseCase = new SaveFloorLayoutDraftUseCase(floorRepo, layoutRepo, NullLogger<SaveFloorLayoutDraftUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // 2 ô đỗ trùng mã "A-01"
        var command = new SaveFloorLayoutDraftCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells:
            [
                new LayoutCellInputDto(0, 0, "slot", "A-01", SlotType.Standard, VehicleType.Suv, 1, 1),
                new LayoutCellInputDto(2, 2, "slot", "A-01", SlotType.Standard, VehicleType.Suv, 1, 1)
            ]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => saveDraftUseCase.ExecuteAsync(command));
        Assert.Contains("trùng lặp", ex.Message);
    }

    // ==================== US-059: PUBLISH LAYOUT TESTS ====================

    [Fact]
    public async Task PublishFloorLayout_WithValidData_SynchronizesSlotsAndCreatesPublishedVersion()
    {
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var publishUseCase = new PublishFloorLayoutUseCase(floorRepo, slotRepo, layoutRepo, NullLogger<PublishFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Đang có 2 slot: A-01 và A-02
        var slot1 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-01",
            GridX = 0,
            GridY = 0,
            WidthCells = 1,
            HeightCells = 1,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Sedan,
            State = SlotState.Available,
            IsActive = true
        }, 101);

        var slot2 = WithId(new Slot
        {
            FloorId = 1,
            Code = "A-02",
            GridX = 1,
            GridY = 0,
            WidthCells = 1,
            HeightCells = 1,
            SlotType = SlotType.Standard,
            MaxVehicleType = VehicleType.Sedan,
            State = SlotState.Available,
            IsActive = true
        }, 102);

        await slotRepo.AddAsync(slot1);
        await slotRepo.AddAsync(slot2);

        // Bản vẽ mới:
        // - A-01: đổi tọa độ sang (3, 4), nâng lên EvCharging, kích thước 2x2
        // - A-02: bị gỡ bỏ khỏi bản vẽ (người vẽ xóa đi)
        // - A-03: thêm mới ở (6, 6)
        // - Kích thước lưới mở rộng thành 25x15
        var command = new PublishFloorLayoutCommand(
            FloorId: 1,
            Columns: 25,
            Rows: 15,
            Cells:
            [
                new LayoutCellInputDto(3, 4, "slot", "A-01", SlotType.EvCharging, VehicleType.Suv, WidthCells: 2, HeightCells: 2),
                new LayoutCellInputDto(6, 6, "slot", "A-03", SlotType.Standard, VehicleType.Sedan, WidthCells: 1, HeightCells: 1)
            ],
            ChangeNote: "Tái cơ cấu mặt bằng tầng 1",
            UserId: 88);

        var result = await publishUseCase.ExecuteAsync(command);

        // 1. Kiểm tra DTO trả về
        Assert.NotNull(result);
        Assert.True(result.IsPublished);
        Assert.NotNull(result.PublishedAtUtc);
        Assert.Equal(1, result.VersionNo);
        Assert.Equal(25, result.Columns);
        Assert.Equal(15, result.Rows);

        // 2. Kiểm tra Floor entity được cập nhật kích thước
        var updatedFloor = await floorRepo.GetByIdAsync(1);
        Assert.NotNull(updatedFloor);
        Assert.Equal(25, updatedFloor.GridColumns);
        Assert.Equal(15, updatedFloor.GridRows);

        // 3. Kiểm tra đồng bộ hóa bảng Slots
        var allSlots = await slotRepo.GetByFloorIdAsync(1);

        // A-01: được cập nhật tọa độ & thông số
        var updatedSlot1 = allSlots.First(s => s.Code == "A-01");
        Assert.Equal(3, updatedSlot1.GridX);
        Assert.Equal(4, updatedSlot1.GridY);
        Assert.Equal(2, updatedSlot1.WidthCells);
        Assert.Equal(2, updatedSlot1.HeightCells);
        Assert.Equal(SlotType.EvCharging, updatedSlot1.SlotType);
        Assert.Equal(VehicleType.Suv, updatedSlot1.MaxVehicleType);
        Assert.True(updatedSlot1.IsActive);

        // A-02: bị xóa khỏi bản vẽ -> soft delete (IsActive = false)
        var softDeletedSlot2 = allSlots.First(s => s.Code == "A-02");
        Assert.False(softDeletedSlot2.IsActive);

        // A-03: slot mới được thêm vào DB
        var newSlot3 = allSlots.First(s => s.Code == "A-03");
        Assert.Equal(6, newSlot3.GridX);
        Assert.Equal(6, newSlot3.GridY);
        Assert.Equal(SlotState.Available, newSlot3.State);
        Assert.True(newSlot3.IsActive);

        // 4. Kiểm tra LayoutVersion trong DB
        var publishedLayout = await layoutRepo.GetLatestPublishedByFloorIdAsync(1);
        Assert.NotNull(publishedLayout);
        Assert.True(publishedLayout.IsPublished);
        Assert.Equal(1, publishedLayout.VersionNo);
        Assert.Equal("Tái cơ cấu mặt bằng tầng 1", publishedLayout.ChangeNote);
        Assert.Equal(88, publishedLayout.CreatedByUserId);
    }

    [Fact]
    public async Task PublishFloorLayout_WhenOccupiedSlotIsRemoved_ThrowsConflictException()
    {
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var publishUseCase = new PublishFloorLayoutUseCase(floorRepo, slotRepo, layoutRepo, NullLogger<PublishFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Slot B-01 đang CÓ XE ĐỖ (Occupied)
        var occupiedSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "B-01",
            GridX = 0,
            GridY = 0,
            State = SlotState.Occupied,
            IsActive = true
        }, 201);
        await slotRepo.AddAsync(occupiedSlot);

        // Publish bản vẽ mà KHÔNG có B-01 (cố tình xóa slot đang có xe)
        var command = new PublishFloorLayoutCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells:
            [
                new LayoutCellInputDto(5, 5, "slot", "B-02", SlotType.Standard, VehicleType.Suv, 1, 1)
            ],
            ChangeNote: "Cố tình xóa slot có xe");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => publishUseCase.ExecuteAsync(command));
        Assert.Contains("B-01", ex.Message);
        Assert.Contains("đang có phương tiện đỗ", ex.Message);

        // Đảm bảo không có layout version nào được tạo khi lỗi
        var publishedVersion = await layoutRepo.GetLatestPublishedByFloorIdAsync(1);
        Assert.Null(publishedVersion);
    }

    [Fact]
    public async Task PublishFloorLayout_WhenReservedSlotIsRemoved_ThrowsConflictException()
    {
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var publishUseCase = new PublishFloorLayoutUseCase(floorRepo, slotRepo, layoutRepo, NullLogger<PublishFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Slot C-01 đang ĐƯỢC GIỮ CHỖ (Reserved)
        var reservedSlot = WithId(new Slot
        {
            FloorId = 1,
            Code = "C-01",
            GridX = 0,
            GridY = 0,
            State = SlotState.Reserved,
            IsActive = true
        }, 301);
        await slotRepo.AddAsync(reservedSlot);

        // Publish bản vẽ không chứa C-01
        var command = new PublishFloorLayoutCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells: [new LayoutCellInputDto(1, 1, "road", WidthCells: 1, HeightCells: 1)],
            ChangeNote: "Xóa slot giữ chỗ");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => publishUseCase.ExecuteAsync(command));
        Assert.Contains("C-01", ex.Message);
        Assert.Contains("đang được giữ chỗ", ex.Message);
    }

    [Fact]
    public async Task PublishFloorLayout_WhenExistingDraftIsPresent_DeletesDraftAndPublishesNewVersion()
    {
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var publishUseCase = new PublishFloorLayoutUseCase(floorRepo, slotRepo, layoutRepo, NullLogger<PublishFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        // Đã có 1 bản draft trước đó
        var draft = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = false,
            LayoutJson = "{}"
        }, 5);
        await layoutRepo.AddAsync(draft);

        var command = new PublishFloorLayoutCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells: [new LayoutCellInputDto(1, 1, "slot", "A-01", SlotType.Standard, VehicleType.Suv, 1, 1)],
            ChangeNote: "Chính thức xuất bản từ nháp");

        var result = await publishUseCase.ExecuteAsync(command);

        Assert.True(result.IsPublished);

        // Draft cũ đã bị dọn dẹp
        var remainingDraft = await layoutRepo.GetLatestDraftByFloorIdAsync(1);
        Assert.Null(remainingDraft);
    }

    [Fact]
    public async Task PublishFloorLayout_WhenSlotCellMissingCode_ThrowsValidationException()
    {
        var floorRepo = new FakeFloorRepo();
        var slotRepo = new FakeSlotRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var publishUseCase = new PublishFloorLayoutUseCase(floorRepo, slotRepo, layoutRepo, NullLogger<PublishFloorLayoutUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        var command = new PublishFloorLayoutCommand(
            FloorId: 1,
            Columns: 10,
            Rows: 10,
            Cells: [new LayoutCellInputDto(0, 0, "slot", Code: null)]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => publishUseCase.ExecuteAsync(command));
        Assert.Contains("thiếu mã ô đỗ (Code)", ex.Message);
    }

    // ==================== US-059: VERSION HISTORY TESTS ====================

    [Fact]
    public async Task GetFloorLayoutVersions_ReturnsDescendingVersionSummaries()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var useCase = new GetFloorLayoutVersionsUseCase(floorRepo, layoutRepo, NullLogger<GetFloorLayoutVersionsUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        var v1 = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-2),
            ChangeNote = "Phiên bản khởi tạo",
            CreatedByUserId = 1,
            LayoutJson = "{}"
        }, 1);

        var v2 = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 2,
            IsPublished = false,
            ChangeNote = "Bản nháp đang sửa",
            CreatedByUserId = 2,
            LayoutJson = "{}"
        }, 2);

        await layoutRepo.AddAsync(v1);
        await layoutRepo.AddAsync(v2);

        var summaries = await useCase.ExecuteAsync(1);

        Assert.NotNull(summaries);
        Assert.Equal(2, summaries.Count);
        Assert.Equal(2, summaries[0].VersionNo);
        Assert.Equal("Bản nháp đang sửa", summaries[0].ChangeNote);
        Assert.Equal(1, summaries[1].VersionNo);
        Assert.Equal("Phiên bản khởi tạo", summaries[1].ChangeNote);
    }

    [Fact]
    public async Task GetFloorLayoutByVersion_WhenVersionExists_ReturnsDetailedLayout()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutByVersionUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutByVersionUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 12, GridRows = 8 }, 1);
        await floorRepo.AddAsync(floor);

        var json = JsonSerializer.Serialize(new
        {
            columns = 12,
            rows = 8,
            cells = new object[]
            {
                new { x = 3, y = 3, type = "pillar", widthCells = 1, heightCells = 1 }
            }
        });

        var version = WithId(new LayoutVersion
        {
            FloorId = 1,
            VersionNo = 3,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            ChangeNote = "Bổ sung cột trụ P-01",
            CreatedByUserId = 5,
            LayoutJson = json
        }, 10);
        await layoutRepo.AddAsync(version);

        var result = await useCase.ExecuteAsync(1, 3);

        Assert.NotNull(result);
        Assert.Equal(1, result.FloorId);
        Assert.Equal(3, result.VersionNo);
        Assert.Equal("Bổ sung cột trụ P-01", result.ChangeNote);
        Assert.Equal(5, result.CreatedByUserId);
        Assert.Single(result.Cells);
        Assert.Equal("pillar", result.Cells[0].Type);
    }

    [Fact]
    public async Task GetFloorLayoutByVersion_WhenVersionNotFound_ThrowsNotFoundException()
    {
        var floorRepo = new FakeFloorRepo();
        var layoutRepo = new FakeLayoutVersionRepo();
        var slotRepo = new FakeSlotRepo();
        var useCase = new GetFloorLayoutByVersionUseCase(floorRepo, layoutRepo, slotRepo, NullLogger<GetFloorLayoutByVersionUseCase>.Instance);

        var floor = WithId(new Floor { ZoneId = 1, Name = "Tầng 1", Level = 1, GridColumns = 10, GridRows = 10 }, 1);
        await floorRepo.AddAsync(floor);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(1, 99));
    }

    // ==================== FAKE REPOSITORIES FOR TESTING ====================

    private sealed class FakeLayoutVersionRepo : ILayoutVersionRepository
    {
        private readonly List<LayoutVersion> _versions = [];
        private int _nextId = 1;

        public Task<LayoutVersion?> GetLatestPublishedByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions
                .Where(v => v.FloorId == floorId && v.IsPublished)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefault());

        public Task<LayoutVersion?> GetLatestDraftByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions
                .Where(v => v.FloorId == floorId && !v.IsPublished)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefault());

        public Task<LayoutVersion?> GetByFloorIdAndVersionAsync(int floorId, int versionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.FirstOrDefault(v => v.FloorId == floorId && v.VersionNo == versionNo));

        public Task<IReadOnlyList<LayoutVersion>> GetAllVersionsByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LayoutVersion>>(_versions
                .Where(v => v.FloorId == floorId)
                .OrderByDescending(v => v.VersionNo)
                .ToList());

        public Task<int> GetNextVersionNoAsync(int floorId, CancellationToken cancellationToken = default)
        {
            var max = _versions.Where(v => v.FloorId == floorId).Select(v => v.VersionNo).DefaultIfEmpty(0).Max();
            return Task.FromResult(max + 1);
        }

        public Task AddAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
        {
            if (layoutVersion.Id == 0) WithId(layoutVersion, _nextId++);
            _versions.Add(layoutVersion);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
        {
            _versions.Remove(layoutVersion);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFloorRepo : IFloorRepository
    {
        private readonly List<Floor> _floors = [];
        private int _nextId = 1;

        public Task<Floor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.FirstOrDefault(f => f.Id == id));

        public Task<Floor?> GetWithSlotsAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.FirstOrDefault(f => f.Id == id));

        public Task<IReadOnlyList<Floor>> GetByZoneIdAsync(int zoneId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Floor>>(_floors.Where(f => f.ZoneId == zoneId).ToList());

        public Task<bool> ExistsNameAsync(int zoneId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_floors.Any(f => f.ZoneId == zoneId && f.Name == name && (!excludeId.HasValue || f.Id != excludeId.Value)));

        public Task AddAsync(Floor floor, CancellationToken cancellationToken = default)
        {
            if (floor.Id == 0) WithId(floor, _nextId++);
            _floors.Add(floor);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Floor floor, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Floor floor, CancellationToken cancellationToken = default)
        {
            _floors.Remove(floor);
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSlotRepo : ISlotRepository
    {
        private readonly List<Slot> _slots = [];
        private int _nextId = 1;

        public Task<Slot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<Slot>> GetByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Slot>>(_slots.Where(s => s.FloorId == floorId).ToList());

        public Task<bool> ExistsCodeAsync(int floorId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.Any(s => s.FloorId == floorId && s.Code == code && (!excludeId.HasValue || s.Id != excludeId.Value)));

        public Task<bool> IsGridCellOccupiedAsync(int floorId, int gridX, int gridY, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_slots.Any(s => s.FloorId == floorId && s.GridX == gridX && s.GridY == gridY && (!excludeId.HasValue || s.Id != excludeId.Value)));

        public Task AddAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            if (slot.Id == 0) WithId(slot, _nextId++);
            _slots.Add(slot);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Slot> slots, CancellationToken cancellationToken = default)
        {
            foreach (var s in slots)
            {
                if (s.Id == 0) WithId(s, _nextId++);
                _slots.Add(s);
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Slot slot, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            _slots.Remove(slot);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
