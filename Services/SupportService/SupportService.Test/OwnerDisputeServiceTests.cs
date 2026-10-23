using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Services;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Test;

public class OwnerDisputeServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
    private const int Owner = 1;
    private const int OwnerUser = 2;

    private static Complaint Ticket(string code = "CP-1", ComplaintStatus status = ComplaintStatus.AwaitingOwner, int? owner = Owner,
        DateTime? dueAt = null, bool payoutHeld = true, decimal? amount = 10000)
        => new()
        {
            Code = code, UserId = 5, BookingCode = "BK-1", ParkingLotId = 1, OwnerProfileId = owner,
            Category = ComplaintCategory.Overcharge, Description = "Bị tính thừa block cuối.", Status = status,
            OwnerResponseDueAtUtc = dueAt ?? Now.AddHours(10), PayoutHeld = payoutHeld, RequestedAmount = amount,
        };

    private static OwnerResponseRequestDto Response(string text = "Log cổng cho thấy xe ra lúc 11:52, đúng giờ tính phí.",
        IReadOnlyList<string>? evidence = null)
        => new(text, evidence ?? ["https://cdn.example.com/gate-log.pdf", "https://cdn.example.com/camera-1152.jpg"]);

    private static (OwnerDisputeService Service, InMemoryComplaintRepository Repo) Create(params Complaint[] tickets)
    {
        var repo = new InMemoryComplaintRepository();
        repo.Saved.AddRange(tickets);
        return (new OwnerDisputeService(repo, new FixedClock(Now)), repo);
    }

    // ===== AC1: phản hồi trong 48 giờ kèm bằng chứng =====

    [Fact]
    public async Task Owner_response_moves_ticket_to_owner_responded_with_evidence_message()
    {
        var ticket = Ticket();
        var (service, repo) = Create(ticket);

        var result = await service.RespondAsync(Owner, OwnerUser, "CP-1", Response());

        Assert.Equal("OwnerResponded", result.Status);
        Assert.Equal(ComplaintStatus.OwnerResponded, ticket.Status);
        Assert.Equal("Log cổng cho thấy xe ra lúc 11:52, đúng giờ tính phí.", ticket.OwnerResponse);
        Assert.Equal(Now, ticket.OwnerRespondedAtUtc);
        var message = Assert.Single(ticket.Messages);
        Assert.Equal(ComplaintParty.LotOwner, message.SenderParty);
        Assert.Equal(OwnerUser, message.SenderUserId);
        Assert.Contains("camera-1152.jpg", message.AttachmentUrlsJson);
        var evt = Assert.IsType<ComplaintOwnerResponded>(Assert.Single(repo.Events));
        Assert.Equal(5, evt.UserId);                      // báo cho tài xế
        Assert.True(ticket.PayoutHeld);                   // AC3: vẫn giữ tiền đến khi có phán quyết
    }

    [Fact]
    public async Task Response_exactly_at_deadline_is_accepted()
        => Assert.Equal("OwnerResponded", (await Create(Ticket(dueAt: Now)).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response())).Status);

    [Fact]
    public async Task Response_after_48_hours_is_rejected()
    {
        var (service, repo) = Create(Ticket(dueAt: Now.AddMinutes(-1)));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.RespondAsync(Owner, OwnerUser, "CP-1", Response()));
        Assert.Contains("quá hạn", ex.Message);
        Assert.Empty(repo.Events);
    }

    [Theory]
    [InlineData(ComplaintStatus.OwnerResponded, "đã phản hồi")]
    [InlineData(ComplaintStatus.Escalated, "nền tảng xử lý")]
    [InlineData(ComplaintStatus.Resolved, "kết luận")]
    [InlineData(ComplaintStatus.Rejected, "kết luận")]
    [InlineData(ComplaintStatus.Open, "không chờ phản hồi")]
    public async Task Only_awaiting_owner_tickets_can_be_answered(ComplaintStatus status, string reason)
    {
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Create(Ticket(status: status)).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response()));
        Assert.Contains(reason, ex.Message);
    }

    [Fact]
    public async Task Ticket_of_another_owner_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            Create(Ticket(owner: 99)).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response()));

    [Theory]
    [InlineData("Ngắn")]
    [InlineData("   ")]
    public async Task Too_short_response_is_rejected(string text)
        => await Assert.ThrowsAsync<ValidationException>(() =>
            Create(Ticket()).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response(text)));

    [Fact]
    public async Task More_than_10_evidence_files_is_rejected()
    {
        var urls = Enumerable.Range(1, 11).Select(i => $"https://cdn.example.com/{i}.jpg").ToList();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Create(Ticket()).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response(evidence: urls)));
    }

    [Fact]
    public async Task Invalid_evidence_url_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() =>
            Create(Ticket()).Service.RespondAsync(Owner, OwnerUser, "CP-1", Response(evidence: ["ftp://x/log.txt"])));

    [Theory]
    [InlineData(0, "CP-1")]
    [InlineData(1, " ")]
    public async Task Owner_and_code_are_required(int owner, string code)
        => await Assert.ThrowsAsync<ValidationException>(() => Create(Ticket()).Service.RespondAsync(owner, OwnerUser, code, Response()));

    [Fact]
    public async Task Owner_can_view_own_ticket_but_not_others()
    {
        var (service, _) = Create(Ticket("CP-1"), Ticket("CP-2", owner: 99));
        Assert.Equal("CP-1", (await service.GetAsync(Owner, "CP-1")).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Owner, "CP-2"));
    }

    // ===== AC3: khoản tiền bị phong tỏa =====

    [Fact]
    public async Task Payout_holds_only_include_open_disputes_of_the_owner()
    {
        var (service, _) = Create(
            Ticket("CP-1"),
            Ticket("CP-2", status: ComplaintStatus.Resolved),          // đã có phán quyết → không giữ nữa
            Ticket("CP-3", payoutHeld: false, amount: null),
            Ticket("CP-4", owner: 99));

        var holds = await service.ListPayoutHoldsAsync(Owner);

        var hold = Assert.Single(holds);
        Assert.Equal("CP-1", hold.ComplaintCode);
        Assert.Equal(10000m, hold.Amount);
        Assert.Equal(2, (await service.ListPayoutHoldsAsync(null)).Count);   // Admin: mọi chủ bãi
    }

    // ===== AC2: quá 48 giờ → nền tảng xử lý =====

    [Fact]
    public async Task Overdue_tickets_are_escalated_to_supervisor_with_system_message()
    {
        var late = Ticket("CP-LATE", dueAt: Now.AddMinutes(-1));
        var onTime = Ticket("CP-OK", dueAt: Now.AddHours(1));
        var repo = new InMemoryComplaintRepository();
        repo.Saved.AddRange([late, onTime]);

        var count = await new ComplaintEscalationService(repo, new FixedClock(Now)).EscalateOverdueAsync();

        Assert.Equal(1, count);
        Assert.Equal(ComplaintStatus.Escalated, late.Status);
        Assert.Equal(Now, late.EscalatedAtUtc);
        Assert.Equal(SupportTeam.Supervisor, late.AssignedTeam);
        Assert.Equal(ComplaintParty.System, Assert.Single(late.Messages).SenderParty);
        Assert.True(late.PayoutHeld);
        var evt = Assert.IsType<ComplaintEscalated>(Assert.Single(repo.Events));
        Assert.Equal(ComplaintEscalationService.Reason, evt.Reason);
        Assert.Equal(ComplaintStatus.AwaitingOwner, onTime.Status);
    }

    [Fact]
    public async Task Escalation_with_nothing_overdue_does_nothing()
    {
        var repo = new InMemoryComplaintRepository();
        repo.Saved.Add(Ticket(dueAt: Now.AddHours(1)));
        Assert.Equal(0, await new ComplaintEscalationService(repo, new FixedClock(Now)).EscalateOverdueAsync());
        Assert.Equal(0, repo.UpdateCount);
    }

    [Theory]
    [InlineData(ComplaintCategory.Overcharge, 10000, true)]
    [InlineData(ComplaintCategory.LotFull, 1, true)]
    [InlineData(ComplaintCategory.Overcharge, 0, false)]
    [InlineData(ComplaintCategory.AppError, 10000, false)]   // lỗi app do nền tảng xử lý → không giữ tiền chủ bãi
    [InlineData(ComplaintCategory.Other, 10000, false)]
    public void Payout_is_held_only_for_lot_disputes_with_money(ComplaintCategory category, int amount, bool held)
        => Assert.Equal(held, ComplaintRules.ShouldHoldPayout(category, amount, Owner));

    [Fact]
    public void No_requested_amount_means_no_hold()
        => Assert.False(ComplaintRules.ShouldHoldPayout(ComplaintCategory.Overcharge, null, Owner));

    [Fact] // Review: chưa biết chủ bãi thì khoản giữ không gắn với ai → không giữ.
    public void Unknown_owner_means_no_hold()
        => Assert.False(ComplaintRules.ShouldHoldPayout(ComplaintCategory.Overcharge, 10000, null));

    // ===== Review: race giữa worker escalate và chủ bãi phản hồi =====

    [Fact]
    public async Task Escalation_skips_ticket_answered_after_the_overdue_list_was_read()
    {
        // Worker đọc danh sách quá hạn, nhưng chủ bãi phản hồi xong trước khi worker xử lý ticket.
        var ticket = Ticket("CP-RACE", dueAt: Now.AddMinutes(-1));
        var repo = new InMemoryComplaintRepository { BeforeFind = c => c.Status = ComplaintStatus.OwnerResponded };
        repo.Saved.Add(ticket);

        var count = await new ComplaintEscalationService(repo, new FixedClock(Now)).EscalateOverdueAsync();

        Assert.Equal(0, count);
        Assert.Equal(ComplaintStatus.OwnerResponded, ticket.Status);   // không bị ghi đè thành Escalated
        Assert.Empty(repo.Events);
    }

    [Fact]
    public async Task Escalation_conflict_is_skipped_without_duplicate_event_and_continues_with_others()
    {
        // RowVersion đổi (instance khác vừa escalate / chủ bãi vừa phản hồi) → bỏ qua ticket đó, xử lý tiếp ticket khác.
        var repo = new InMemoryComplaintRepository();
        repo.Saved.AddRange([Ticket("CP-A", dueAt: Now.AddMinutes(-2)), Ticket("CP-B", dueAt: Now.AddMinutes(-1))]);
        repo.ConflictOnUpdate.Add("CP-A");

        var count = await new ComplaintEscalationService(repo, new FixedClock(Now)).EscalateOverdueAsync();

        Assert.Equal(1, count);
        var evt = Assert.IsType<ComplaintEscalated>(Assert.Single(repo.Events));
        Assert.Equal("CP-B", evt.Code);
    }

    [Fact]
    public async Task Owner_response_losing_the_race_returns_conflict()
    {
        // Worker escalate trước → lần lưu của chủ bãi gặp xung đột RowVersion → 409, không phát sự kiện.
        var (service, repo) = Create(Ticket());
        repo.ConflictOnUpdate.Add("CP-1");

        await Assert.ThrowsAsync<ConflictException>(() => service.RespondAsync(Owner, OwnerUser, "CP-1", Response()));
        Assert.Empty(repo.Events);
    }

    // ===== Review: đóng tranh chấp phải giải phóng khoản giữ =====

    [Theory]
    [InlineData(ComplaintStatus.Resolved)]
    [InlineData(ComplaintStatus.Rejected)]
    public void Closing_a_dispute_releases_the_payout_hold(ComplaintStatus outcome)
    {
        var ticket = Ticket(status: ComplaintStatus.Escalated);

        ticket.Close(outcome, "Đối soát log cổng: tính đúng giờ.", 0, resolvedByUserId: 1, Now);

        Assert.False(ticket.PayoutHeld);
        Assert.Equal(outcome, ticket.Status);
        Assert.Equal(Now, ticket.ResolvedAtUtc);
        Assert.Equal(1, ticket.ResolvedByUserId);
    }

    [Fact]
    public async Task Closed_dispute_no_longer_appears_in_payout_holds()
    {
        var ticket = Ticket();
        var (service, _) = Create(ticket);
        ticket.Close(ComplaintStatus.Resolved, "Hoàn 10.000đ cho tài xế.", 10000, 1, Now);
        Assert.Empty(await service.ListPayoutHoldsAsync(Owner));
    }

    [Fact]
    public void Close_rejects_non_final_status_and_double_close()
    {
        Assert.Throws<ArgumentException>(() => Ticket().Close(ComplaintStatus.Escalated, "x", null, 1, Now));
        var closed = Ticket();
        closed.Close(ComplaintStatus.Rejected, "Không đủ căn cứ.", null, 1, Now);
        Assert.Throws<InvalidOperationException>(() => closed.Close(ComplaintStatus.Resolved, "x", null, 1, Now));
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
