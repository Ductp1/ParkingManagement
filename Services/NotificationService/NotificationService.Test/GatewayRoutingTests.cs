using System.Text.Json;

namespace NotificationService.Test;

/// <summary>
/// T-601 – KIỂM TRA CẤU HÌNH ROUTE GATEWAY CHO SIGNALR WEBSOCKET (/hubs/notify, /hubs/parking).
/// Yêu cầu T-601: client kết nối hub QUA Gateway :5000 → Gateway phải có route khớp "/hubs/{**rest}"
/// trỏ tới cluster "notification" (NotificationService :5106); YARP tự cho phép WebSocket upgrade
/// khi route khớp và destination hỗ trợ.
/// TRẠNG THÁI HIỆN TẠI: appsettings.json của Gateway CHƯA có route /hubs/* – frontend đang nối
/// thẳng :5106 (xem src/realtime/useNotificationHub.ts). Test này chủ đích FAIL làm bằng chứng
/// tự động cho GAP – KHÔNG sửa test cho khớp gap (theo yêu cầu QA).
/// File đặt ở NotificationService.Test (chỉ đọc file cấu hình) vì IntegrationTests project
/// không chạy được trong môi trường hiện tại do Application Control policy chặn DLL mới build.
/// </summary>
public class GatewayRoutingTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParkingManagement.slnx")))
            dir = dir.Parent!;
        return dir!.FullName;
    }

    [Fact]
    public void Gateway_must_have_signalR_hub_route_to_notification_service()
    {
        var configPath = Path.Combine(RepoRoot(), "Gateway", "ParkingManagement.Gateway", "appsettings.json");
        Assert.True(File.Exists(configPath), $"Không tìm thấy cấu hình Gateway tại {configPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var proxy = doc.RootElement.GetProperty("ReverseProxy");

        // 1) Phải tồn tại route khớp /hubs/* → cluster "notification".
        string? hubRouteName = null;
        string? hubCluster = null;
        foreach (var route in proxy.GetProperty("Routes").EnumerateObject())
        {
            var matchPath = route.Value.TryGetProperty("Match", out var match)
                            && match.TryGetProperty("Path", out var pathProp)
                ? pathProp.GetString() : null;
            if (matchPath is null || !matchPath.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
                continue;
            hubRouteName = route.Name;
            hubCluster = route.Value.TryGetProperty("ClusterId", out var cluster) ? cluster.GetString() : null;
            break;
        }

        Assert.True(hubRouteName is not null,
            "T-601 GAP: Gateway chưa có route '/hubs/{**rest}' cho SignalR WebSocket → client phải nối thẳng :5106, " +
            "vi phạm kiến trúc 'mọi traffic đi qua Gateway :5000'. Bổ sung route vào ReverseProxy:Routes.");
        Assert.True(hubCluster == "notification",
            $"T-601: route {hubRouteName} phải trỏ tới cluster 'notification'.");
        Assert.True(hubRouteName is not null && !string.IsNullOrWhiteSpace(hubCluster),
            $"T-601: route {hubRouteName} thiếu ClusterId.");

        // 2) Cluster "notification" phải trỏ tới NotificationService :5106.
        var destination = proxy.GetProperty("Clusters").GetProperty("notification")
            .GetProperty("Destinations").GetProperty("primary").GetProperty("Address").GetString();
        Assert.Contains("5106", destination);
    }
}