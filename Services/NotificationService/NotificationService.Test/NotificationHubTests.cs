using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.API.Hubs;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>
/// T-601 – Unit test NotificationHub (route /hubs/notify) THẬT: join/leave group "user-{userId}"
/// theo claim "sub" hoặc query ?userId=, routing event chỉ tới group đúng. Context/Clients/Groups
/// được NSubstitute thay thế (không cần server SignalR thật).
/// LƯU Ý (gap có chủ đích, xem báo cáo): hub KHÔNG từ chối connection thiếu token – chỉ bỏ qua việc
/// join group, nên client ẩn danh vẫn connect được nhưng sẽ KHÔNG nhận event nào.
/// </summary>
public class NotificationHubTests
{
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    // Client(string) trên IHubCallerClients trả ISingleClientProxy (kế thừa IClientProxy) → dùng đúng kiểu.
    private readonly ISingleClientProxy _proxy = Substitute.For<ISingleClientProxy>();

    private NotificationHub CreateHub(int? userIdClaim = null, int? userIdQuery = null, string connectionId = "conn-1")
    {
        var identity = userIdClaim is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim("sub", userIdClaim.Value.ToString())]);
        var http = new DefaultHttpContext();
        if (userIdQuery is not null)
            http.Request.QueryString = new QueryString($"?userId={userIdQuery}");

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.User.Returns(new ClaimsPrincipal(identity));
        context.GetHttpContext().Returns(http);

        // Dùng overload Func của .Returns để NSubstitute không hiểu nhầm là
        // "mySub.Method().Returns(otherSub.Method())" (guard Potentially problematic use).
        _clients.Group(Arg.Any<string>()).Returns(_ => _proxy);
        _clients.All.Returns(_ => _proxy);
        _clients.Client(Arg.Any<string>()).Returns(_ => _proxy);

        return new NotificationHub(NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Clients = _clients,
            Groups = _groups,
        };
    }

    [Theory]
    [InlineData(5)]
    [InlineData(42)]
    public async Task OnConnectedAsync_with_userId_claim_joins_user_group(int userId)
    {
        var hub = CreateHub(userIdClaim: userId);
        await hub.OnConnectedAsync();
        await _groups.Received(1).AddToGroupAsync("conn-1", $"user-{userId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_with_userId_query_joins_user_group()
    {
        var hub = CreateHub(userIdQuery: 7);
        await hub.OnConnectedAsync();
        await _groups.Received(1).AddToGroupAsync("conn-1", "user-7", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_without_identity_joins_no_group()
    {
        // Hành vi thật: connection ẩn danh được chấp nhận nhưng KHÔNG join group nào
        // → không bao giờ nhận event (requirement "từ chối connection không token" chưa cài – xem báo cáo).
        var hub = CreateHub();
        await hub.OnConnectedAsync();
        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_leaves_user_group()
    {
        var hub = CreateHub(userIdClaim: 5);
        await hub.OnDisconnectedAsync(null);
        await _groups.Received(1).RemoveFromGroupAsync("conn-1", "user-5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendNotificationToUser_only_target_user_group_receives_event()
    {
        var hub = CreateHub(userIdClaim: 5);
        var message = new NotificationMessage(99, 5, NotificationChannel.InApp, "Đặt chỗ thành công",
            "Body", null, DateTime.UtcNow);

        await hub.SendNotificationToUserAsync(5, message);

        _clients.Received(1).Group("user-5");
        _clients.DidNotReceive().Group("user-7");
        await _proxy.Received(1).SendCoreAsync("ReceiveNotification",
            Arg.Is<object[]>(args => args.Length == 1 && ((NotificationMessage)args[0]).NotificationId == 99),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastNotification_reaches_all_clients()
    {
        var hub = CreateHub(userIdClaim: 5);
        var message = new NotificationMessage(1, 5, NotificationChannel.Email, "T", "B", null, DateTime.UtcNow);

        await hub.BroadcastNotificationAsync(message);

        await _proxy.Received(1).SendCoreAsync("ReceiveNotification",
            Arg.Is<object[]>(args => args.Length == 1 && ReferenceEquals(args[0], message)),
            Arg.Any<CancellationToken>());
    }
}