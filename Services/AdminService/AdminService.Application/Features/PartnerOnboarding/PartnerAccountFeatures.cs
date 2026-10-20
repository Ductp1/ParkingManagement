using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST =====

/// <summary>A9 issue-account: tài khoản nhận thanh toán mặc định của đối tác (in vào Phụ lục 02 của hợp đồng từng bãi).</summary>
public sealed record IssueOwnerAccountRequest(PartnerBankAccountRequest? Bank);

// ===== USE CASE =====

public interface IIssueOwnerAccountUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, IssueOwnerAccountRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A9, T6): UnderReview → AccountIssued. Không ký hợp đồng ở bước này – hợp đồng lập theo từng bãi sau KYB.
/// 1. Kiểm tra trạng thái, rồi tài khoản ngân hàng (chưa gọi ra ngoài).
/// 2. Gọi UserService U2 (Idempotency-Key theo hồ sơ): lỗi → hồ sơ giữ nguyên, Admin thử lại an toàn (503 / 409).
/// 3. Hồ sơ (AccountIssued + ngân hàng + Id tài khoản) + AuditLog trong 1 SaveChanges; lỗi lưu → thử lại cùng Idempotency-Key.
/// </summary>
public sealed class IssueOwnerAccountUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient ownerAccounts) : IIssueOwnerAccountUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, IssueOwnerAccountRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        app.EnsureCanIssueAccount();
        var bank = BankAccount.Create(request.Bank?.Bin, request.Bank?.Name, request.Bank?.AccountNumber, request.Bank?.AccountName);

        var account = await ownerAccounts.IssueOwnerAccountAsync(new IssueOwnerAccountCommand(
                app.Id, app.FullName, app.Email, app.Phone, app.BusinessName, app.TaxCode, app.BusinessAddress,
                bank.Bin, bank.BankName, bank.AccountNumber, bank.AccountName),
            IdempotencyKey(app.Id), cancellationToken);

        app.IssueAccount(performedByUserId, bank, account.UserId, account.OwnerProfileId);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "AccountIssued", performedByUserId, before, null),
            cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }

    /// <summary>Một hồ sơ chỉ tạo một tài khoản: gọi lại U2 với cùng khóa trả về tài khoản đã tạo.</summary>
    public static string IdempotencyKey(int applicationId) => $"partner-application-{applicationId}";
}

public interface IResendOwnerActivationUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A10, T7): AccountIssued giữ nguyên; gọi UserService U3 (thu hồi link cũ, gửi link kích hoạt mới) rồi ghi AuditLog.
/// UserService lỗi → không ghi gì, Admin thử lại.
/// </summary>
public sealed class ResendOwnerActivationUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient ownerAccounts) : IResendOwnerActivationUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        var ownerProfileId = app.EnsureCanResendActivation();

        await ownerAccounts.ResendActivationAsync(ownerProfileId, cancellationToken);
        await repository.SaveWithAuditAsync(PartnerApplicationAudit.Build(app, "ActivationResent", performedByUserId, before, null),
            cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, id, cancellationToken);
    }
}
