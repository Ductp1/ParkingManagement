using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;

namespace AdminService.Test;

// US-100: giá trị cho bộ lọc màn Audit Log (unit test với fake IAuditLogQueries chạy đúng GROUP BY của AuditLogQueries, không cần database).
public class AuditLogFilterOptionsTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    private static readonly AuditLog[] Logs =
    [
        AuditLogs.Create(1, Now, "AdminService", 1, 2, "Owner.Locked", "OwnerProfile", "12", "Gian lận"),
        AuditLogs.Create(2, Now, "AdminService", 1, 2, "Owner.Unlocked", "OwnerProfile", "12", "Đã khắc phục"),
        AuditLogs.Create(3, Now, "AdminService", 1, null, "SystemConfig.ChangeScheduled", "SystemConfig", "BOOKING_HOLD_MINUTES", "Thử"),
        AuditLogs.Create(4, Now, "BookingService", 7, null, "Booking.Cancelled", "Booking", "BK-1", "Bãi đầy"),
        AuditLogs.Create(5, Now, "ParkingService", 1, null, "KybApplication.Approved", "KybApplication", "9", null),
        AuditLogs.Create(6, Now, "BookingService", 8, null, "Booking.Cancelled", "Booking", "BK-2", null),
    ];

    [Fact]
    public async Task Filter_options_count_each_value_in_database_sorted_by_name()
    {
        var options = await new GetAuditLogFilterOptionsUseCase(new InMemoryAuditLogQueries(Logs)).ExecuteAsync();

        Assert.Equal(6, options.TotalCount);
        Assert.Equal([new("AdminService", 3), new("BookingService", 2), new AuditLogFacetDto("ParkingService", 1)], options.SourceServices);
        Assert.Equal(["Booking.Cancelled", "KybApplication.Approved", "Owner.Locked", "Owner.Unlocked", "SystemConfig.ChangeScheduled"],
            options.Actions.Select(a => a.Value));
        Assert.Equal(2, options.Actions.Single(a => a.Value == "Booking.Cancelled").Count);
        Assert.Equal([new("Booking", 2), new("KybApplication", 1), new("OwnerProfile", 2), new AuditLogFacetDto("SystemConfig", 1)],
            options.EntityNames);
    }

    [Fact]
    public async Task Filter_options_include_services_that_appear_later_without_hard_coding()
    {
        // Log từ service mới gửi về qua AuditRecorded (T-802) tự xuất hiện trong dropdown.
        AuditLog[] logs = [.. Logs, AuditLogs.Create(7, Now, "GateService", 20, null, "Gate.ManualOpen", "GateDevice", "3", "Khẩn cấp")];

        var options = await new GetAuditLogFilterOptionsUseCase(new InMemoryAuditLogQueries(logs)).ExecuteAsync();

        Assert.Contains(new AuditLogFacetDto("GateService", 1), options.SourceServices);
        Assert.Equal(7, options.TotalCount);
    }

    [Fact]
    public async Task Filter_options_of_empty_table_are_empty()
    {
        var options = await new GetAuditLogFilterOptionsUseCase(new InMemoryAuditLogQueries([])).ExecuteAsync();

        Assert.Equal(0, options.TotalCount);
        Assert.Empty(options.SourceServices);
        Assert.Empty(options.Actions);
        Assert.Empty(options.EntityNames);
    }

    [Fact]
    public async Task Sort_is_ordinal_so_uppercase_comes_before_lowercase()
    {
        AuditLog[] logs =
        [
            AuditLogs.Create(1, Now, "paymentService", 1, null, "a.b", "x", null, null),
            AuditLogs.Create(2, Now, "PaymentService", 1, null, "A.b", "X", null, null),
        ];

        var options = await new GetAuditLogFilterOptionsUseCase(new InMemoryAuditLogQueries(logs)).ExecuteAsync();

        Assert.Equal(["PaymentService", "paymentService"], options.SourceServices.Select(s => s.Value));
    }
}
