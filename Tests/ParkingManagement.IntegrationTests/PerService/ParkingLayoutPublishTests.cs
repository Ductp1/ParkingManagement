using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.API.Controllers;
using ParkingService.Application.Features.Layouts;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: ParkingService chạy host thật trên database pm_it_parking_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Parking layout")]
public sealed class ParkingLayoutCollection : ICollectionFixture<ServiceHostFixture<ParkingLotsController, ParkingDbContext>>;

/// <summary>
/// US-059 / US-060 trên PostgreSQL thật: xuất bản / khôi phục sơ đồ có ô đổi chỗ cho nhau không được đụng chỉ mục unique
/// (FloorId, GridX, GridY) – chỉ mục kiểm từng dòng nên fake repository không phát hiện được.
/// </summary>
[Collection("Parking layout")]
public class ParkingLayoutPublishTests(ServiceHostFixture<ParkingLotsController, ParkingDbContext> fixture)
{
    private const int FloorId = 1;

    [Fact]
    public async Task Publish_and_rollback_can_move_slots_into_places_held_by_other_slots()
    {
        var floor = await Query(db => db.Floors.AsNoTracking().SingleAsync(f => f.Id == FloorId));
        var (columns, rows) = (floor.GridColumns, floor.GridRows);
        var slots = await ActiveSlots();
        Assert.True(slots.Count > 2);

        // 1. Xoay vòng: mỗi ô nhận vị trí của ô kế tiếp.
        var rotated = slots.Select((s, i) => At(s, slots[(i + 1) % slots.Count].GridX, slots[(i + 1) % slots.Count].GridY)).ToList();
        var first = await Publish(columns, rows, rotated);
        await AssertActiveSlotsAre(rotated);

        // 2. Gỡ một ô trống: ô bị tắt vẫn nằm ở vị trí cũ trong bảng Slots.
        var removed = rotated.First(c => slots.Single(s => s.Code == c.Code).State == SlotState.Available);
        var without = rotated.Where(c => c.Code != removed.Code).ToList();
        await Publish(columns, rows, without);
        await AssertActiveSlotsAre(without);

        // 3. Ô mới đặt đúng chỗ của ô đã tắt.
        var replaced = without.Append(new LayoutCellInputDto(removed.X, removed.Y, "slot", "ZZ-01")).ToList();
        await Publish(columns, rows, replaced);
        await AssertActiveSlotsAre(replaced);

        // 4. Khôi phục bản đầu: ô cũ lấy lại chỗ đang do ô mới giữ.
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRollbackFloorLayoutUseCase>()
                .ExecuteAsync(new RollbackFloorLayoutCommand(FloorId, first.VersionNo, "rollback"));
        }
        await AssertActiveSlotsAre(rotated);
    }

    private static LayoutCellInputDto At(Slot s, int x, int y) => new(x, y, "slot", s.Code, s.SlotType, s.MaxVehicleType);

    private async Task<FloorLayoutDto> Publish(int columns, int rows, IReadOnlyList<LayoutCellInputDto> cells)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPublishFloorLayoutUseCase>()
            .ExecuteAsync(new PublishFloorLayoutCommand(FloorId, columns, rows, cells, "test"));
    }

    private async Task<T> Query<T>(Func<ParkingDbContext, Task<T>> query)
    {
        using var scope = fixture.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ParkingDbContext>());
    }

    private Task<List<Slot>> ActiveSlots() => Query(db => db.Slots.AsNoTracking()
        .Where(s => s.FloorId == FloorId && s.IsActive).OrderBy(s => s.Code).ToListAsync());

    private async Task AssertActiveSlotsAre(IEnumerable<LayoutCellInputDto> cells)
        => Assert.Equal(
            cells.Select(c => $"{c.Code}@{c.X},{c.Y}").Order(StringComparer.Ordinal),
            (await ActiveSlots()).Select(s => $"{s.Code}@{s.GridX},{s.GridY}").Order(StringComparer.Ordinal));
}
