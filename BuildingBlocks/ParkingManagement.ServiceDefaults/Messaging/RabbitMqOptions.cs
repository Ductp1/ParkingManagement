using System.Net;
using Microsoft.Extensions.Hosting;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingManagement.ServiceDefaults.Messaging;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "parking";
    public string UserName { get; set; } = "parking";
    public string Password { get; set; } = "";
    public string Exchange { get; set; } = "parking.events";
    public bool UseTls { get; set; }
    public int OperationTimeoutSeconds { get; set; } = 10;

    public void Validate(IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(HostName) || Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(VirtualHost)
            || string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password)
            || string.IsNullOrWhiteSpace(Exchange) || Exchange.StartsWith("amq.", StringComparison.Ordinal)
            || OperationTimeoutSeconds is < 1 or > 60)
            throw new DependencyUnavailableException("Cấu hình EventBus:RabbitMq chưa hợp lệ; cần host, vhost, tài khoản, mật khẩu và exchange.");
        var loopback = HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(HostName, out var ip) && IPAddress.IsLoopback(ip));
        if (!UseTls && !loopback && !environment.IsDevelopment())
            throw new DependencyUnavailableException("Kết nối RabbitMQ ngoài localhost phải bật TLS ở môi trường production.");
    }
}

public sealed class RabbitMqSubscription
{
    public string Queue { get; set; } = "";
    public string[] RoutingKeys { get; set; } = [];
    public ushort PrefetchCount { get; set; } = 10;
}
