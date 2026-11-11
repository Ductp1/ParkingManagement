using System.Text.Json;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-100: chi tiết audit log – giá trị cũ/mới đã che + trường thay đổi (unit test với fake IAuditLogQueries, không cần database).
public class AuditLogDetailTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    private static Task<AuditLogDetailDto> Get(AuditLog log, int? id = null)
        => new GetAuditLogUseCase(new InMemoryAuditLogQueries([log])).ExecuteAsync(id ?? log.Id);

    private static string? Raw(JsonElement? element) => element?.GetRawText();

    [Fact]
    public async Task Detail_returns_all_fields_of_owner_lock_audit_written_by_us096()
    {
        // Đúng dạng OwnerLockFeatures.BuildAuditLog ghi (US-096).
        var log = AuditLogs.Create(18, Now, "AdminService", 1, 2, "Owner.Locked", "OwnerProfile", "12", "Gian lận doanh thu",
            "{\"isLocked\":false}", "{\"isLocked\":true,\"sanctionId\":7,\"level\":\"TemporarySuspension\"}", "113.161.4.20");

        var detail = await Get(log);

        Assert.Equal((18, Now, "AdminService", 1, 2), (detail.Id, detail.CreatedAtUtc, detail.SourceService, detail.UserId, detail.OwnerProfileId));
        Assert.Equal(("Owner.Locked", "OwnerProfile", "12"), (detail.Action, detail.EntityName, detail.EntityId));
        Assert.Equal(("Gian lận doanh thu", "113.161.4.20"), (detail.Reason, detail.IpAddress));
        Assert.Equal("{\"isLocked\":false}", Raw(detail.OldValues));
        Assert.Equal(["isLocked:Modified", "sanctionId:Added", "level:Added"], detail.Changes.Select(c => $"{c.Field}:{c.Kind}"));
        var isLocked = detail.Changes[0];
        Assert.Equal(("false", "true"), (Raw(isLocked.OldValue), Raw(isLocked.NewValue)));
        Assert.Null(detail.Changes[1].OldValue);
    }

    [Fact]
    public async Task Detail_of_system_config_change_from_us098_lists_only_changed_fields()
    {
        var log = AuditLogs.Create(19, Now, "AdminService", 1, null, "SystemConfig.ChangeCancelled", "SystemConfig", "BOOKING_HOLD_MINUTES",
            "Nhập nhầm", "{\"changeId\":9,\"cancelled\":false,\"value\":\"25\"}", "{\"changeId\":9,\"cancelled\":true,\"value\":\"15\"}");

        var detail = await Get(log);

        Assert.Equal(["cancelled", "value"], detail.Changes.Select(c => c.Field));
        Assert.All(detail.Changes, c => Assert.Equal("Modified", c.Kind));
    }

    [Fact]
    public async Task Detail_masks_bank_account_of_partner_application_but_still_reports_the_change()
    {
        // PartnerApplicationAudit.Snapshot (US-116) lưu số tài khoản nhận tiền đầy đủ.
        var log = AuditLogs.Create(20, Now, "AdminService", 1, 5, "PartnerApplication.AccountIssued", "PartnerApplication", "41", null,
            "{\"status\":\"ContractSigned\",\"payoutBankAccount\":\"0011002233445\"}",
            "{\"status\":\"AccountIssued\",\"payoutBankAccount\":\"0099002233445\"}");

        var detail = await Get(log);

        Assert.Equal("{\"status\":\"ContractSigned\",\"payoutBankAccount\":\"****3445\"}", Raw(detail.OldValues));
        Assert.Equal("{\"status\":\"AccountIssued\",\"payoutBankAccount\":\"****3445\"}", Raw(detail.NewValues));
        var bank = Assert.Single(detail.Changes, c => c.Field == "payoutBankAccount");      // so trên giá trị gốc
        Assert.Equal(("\"****3445\"", "\"****3445\""), (Raw(bank.OldValue), Raw(bank.NewValue)));
        Assert.DoesNotContain("0011002233445", JsonSerializer.Serialize(detail));
        Assert.DoesNotContain("0099002233445", JsonSerializer.Serialize(detail));
    }

    [Fact]
    public async Task Detail_of_log_from_other_service_flattens_nested_fields_and_hides_credentials()
    {
        // Log service khác gửi về qua AuditRecorded (T-802): OwnerProfileId, IpAddress không có trong hợp đồng sự kiện.
        var log = AuditLogs.Create(21, Now, "UserService", 3, null, "User.PasswordChanged", "User", "3", null,
            "{\"profile\":{\"fullName\":\"An\",\"cccd\":\"079203001234\"},\"passwordHash\":\"old-hash\"}",
            "{\"profile\":{\"fullName\":\"Quốc An\",\"cccd\":\"079203001234\"},\"passwordHash\":\"new-hash\",\"sessions\":[1]}");

        var detail = await Get(log);

        Assert.Equal(("UserService", null, null), (detail.SourceService, detail.OwnerProfileId, detail.IpAddress));
        Assert.Equal(["profile.fullName:Modified", "passwordHash:Modified", "sessions:Added"],
            detail.Changes.Select(c => $"{c.Field}:{c.Kind}"));
        Assert.Equal(("\"***\"", "\"***\""), (Raw(detail.Changes[1].OldValue), Raw(detail.Changes[1].NewValue)));
        Assert.Contains("\"cccd\":\"****1234\"", Raw(detail.NewValues));
        Assert.DoesNotContain("old-hash", JsonSerializer.Serialize(detail));
        Assert.DoesNotContain("new-hash", JsonSerializer.Serialize(detail));
    }

    [Fact]
    public async Task Detail_reports_removed_fields_and_create_or_delete_logs()
    {
        var created = AuditLogs.Create(22, Now, "AdminService", 1, null, "ContractTemplate.Created", "ContractTemplate", "3", null,
            null, "{\"status\":\"Draft\"}");
        var removed = AuditLogs.Create(23, Now, "AdminService", 1, null, "ContractTemplate.Updated", "ContractTemplate", "3", null,
            "{\"status\":\"Draft\",\"note\":\"x\"}", "{\"status\":\"Draft\"}");

        var createdDetail = await Get(created);
        var removedDetail = await Get(removed);

        Assert.Null(createdDetail.OldValues);
        var status = Assert.Single(createdDetail.Changes);
        Assert.Equal(("status", "Added"), (status.Field, status.Kind));
        var note = Assert.Single(removedDetail.Changes);
        Assert.Equal(("note", "Removed", "\"x\""), (note.Field, note.Kind, Raw(note.OldValue)));
        Assert.Null(note.NewValue);
    }

    [Fact]
    public async Task Detail_without_json_values_has_no_changes()
    {
        var log = AuditLogs.Create(24, Now, "BookingService", 7, null, "Booking.Cancelled", "Booking", "BK-2026-00142", "Bãi báo đầy");

        var detail = await Get(log);

        Assert.Null(detail.OldValues);
        Assert.Null(detail.NewValues);
        Assert.Empty(detail.Changes);
    }

    [Fact]
    public async Task Detail_compares_non_object_roots_as_a_whole()
    {
        var log = AuditLogs.Create(25, Now, "ParkingService", 1, null, "Slot.Reordered", "Floor", "2", null, "[1,2]", "[2,1]");

        var change = Assert.Single((await Get(log)).Changes);

        Assert.Equal(("$", "Modified", "[1,2]", "[2,1]"), (change.Field, change.Kind, Raw(change.OldValue), Raw(change.NewValue)));
    }

    [Fact]
    public async Task Same_values_in_different_key_order_are_not_a_change()
    {
        var log = AuditLogs.Create(26, Now, "AdminService", 1, null, "X.Y", "X", "1", null,
            "{\"a\":1,\"b\":{\"c\":2,\"d\":3}}", "{\"b\":{\"d\":3,\"c\":2},\"a\":1}");

        Assert.Empty((await Get(log)).Changes);
    }

    [Fact]
    public async Task Missing_id_returns_not_found()
    {
        var log = AuditLogs.Create(1, Now, "AdminService", 1, null, "X.Y", "X", "1", null);

        await Assert.ThrowsAsync<NotFoundException>(() => Get(log, id: 99));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_id_is_rejected_before_querying(int id)
    {
        var queries = new InMemoryAuditLogQueries([]);

        await Assert.ThrowsAsync<ValidationException>(() => new GetAuditLogUseCase(queries).ExecuteAsync(id));
        Assert.Equal(0, queries.Calls);
    }
}
