using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.API.Hubs;
using NSubstitute;

namespace NotificationService.Test;

/// <summary>
/// T-601/US-065/US-066 – Unit test ParkingHub (route /hubs/parking) THẬT:
/// subscribe/unsubscribe group "lot-{parkingLotId}", broadcast SlotStateChanged/CapacityChanged
/// CHỈ tới group của bãi đó (client bãi khác không nhận), payload đầy đủ field có trong hợp đồng.
/// </summary>
public class ParkingHubTests
{
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    // Client(string) trên IHubCallerClients trả ISingleClientProxy (kế thừa IClientProxy) → dùng đúng kiểu.
    private readonly ISingleClientProxy _proxy = Substitute.For<ISingleClientProxy>();

    private ParkingHub CreateHub(string connectionId = "conn-1")
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.GetHttpContext().Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());

        // Dùng overload Func của .Returns để NSubstitute không hiểu nhầm là
        // "mySub.Method().Returns(otherSub.Method())" (guard Potentially problematic use).
        _clients.Group(Arg.Any<string>()).Returns(_ => _proxy);
        _clients.Client(Arg.Any<string>()).Returns(_ => _proxy);

        return new ParkingHub(NullLogger<ParkingHub>.Instance)
        {
            Context = context,
            Clients = _clients,
            Groups = _groups,
        };
    }

    [Fact]
    public async Task SubscribeToLot_joins_lot_group_and_acks_caller()
    {
        var hub = CreateHub();

        await hub.SubscribeToLotAsync(3);

        await _groups.Received(1).AddToGroupAsync("conn-1", "lot-3", Arg.Any<CancellationToken>());
        await _proxy.Received(1).SendCoreAsync("SubscribedToLot",
            Arg.Is<object[]>(args => args.Length == 1 && (int)args[0] == 3), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SubscribeToLot_with_invalid_lot_id_is_ignored(int parkingLotId)
    {
        var hub = CreateHub();
        await hub.SubscribeToLotAsync(parkingLotId);
        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeFromLot_leaves_lot_group_and_acks()
    {
        var hub = CreateHub();
        await hub.UnsubscribeFromLotAsync(3);
        await _groups.Received(1).RemoveFromGroupAsync("conn-1", "lot-3", Arg.Any<CancellationToken>());
        await _proxy.Received(1).SendCoreAsync("UnsubscribedFromLot",
            Arg.Is<object[]>(args => args.Length == 1 && (int)args[0] == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SlotStateChange_is_broadcast_only_to_that_lot_group_with_full_payload()
    {
        var hub = CreateHub();
        var changedAt = new DateTime(2026, 1, 15, 8, 30, 0, DateTimeKind.Utc);
        var message = new SlotStateChangeMessage(SlotId: 12, ParkingLotId: 3, NewState: "Occupied",
            PreviousState: "Available", Reason: "CheckedIn", ChangedAtUtc: changedAt);

        await hub.BroadcastSlotStateChangeAsync(3, message);

        _clients.Received(1).Group("lot-3");
        _clients.DidNotReceive().Group("lot-4");
        await _proxy.Received(1).SendCoreAsync("SlotStateChanged",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is SlotStateChangeMessage
                && ((SlotStateChangeMessage)args[0]).SlotId == 12
                && ((SlotStateChangeMessage)args[0]).ParkingLotId == 3
                && ((SlotStateChangeMessage)args[0]).NewState == "Occupied"
                && ((SlotStateChangeMessage)args[0]).PreviousState == "Available"
                && ((SlotStateChangeMessage)args[0]).Reason == "CheckedIn"
                && ((SlotStateChangeMessage)args[0]).ChangedAtUtc == changedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CapacityChange_is_broadcast_only_to_that_lot_group()
    {
        // US-066: đổi cấu hình sức chứa → phát event cập nhật số chỗ (CapacityChanged) qua hub US-065.
        var hub = CreateHub();
        var message = new CapacityChangeMessage(ParkingLotId: 3, AvailableSlots: 7, TotalSlots: 20,
            OccupiedSlots: 10, ReservedSlots: 3, IsEmergencyMode: false, UpdatedAtUtc: DateTime.UtcNow);

        await hub.BroadcastCapacityChangeAsync(3, message);

        _clients.Received(1).Group("lot-3");
        _clients.DidNotReceive().Group("lot-9");
        await _proxy.Received(1).SendCoreAsync("CapacityChanged",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is CapacityChangeMessage
                && ((CapacityChangeMessage)args[0]).AvailableSlots == 7
                && ((CapacityChangeMessage)args[0]).TotalSlots == 20
                && !((CapacityChangeMessage)args[0]).IsEmergencyMode),
            Arg.Any<CancellationToken>());
    }
}