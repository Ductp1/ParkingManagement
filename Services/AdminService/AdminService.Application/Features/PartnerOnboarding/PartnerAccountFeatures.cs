using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST =====

/// <summary>A9 issue-account (multipart): bản scan hợp đồng đã ký + phiên bản Admin xác nhận đã ký.</summary>
public sealed record IssueOwnerAccountRequest(int? ConfirmedDraftVersion, UploadedFile? SignedFile);

/// <summary>Kiểm tra bản scan đã ký: PDF / JPG / PNG nhận diện theo nội dung file (không tin tên hay Content-Type từ client).</summary>
public static class SignedScanRules
{
    public const int MaxBytes = 10 * 1024 * 1024;

    public static (string ContentType, string Extension) Detect(UploadedFile? file)
    {
        if (file is null || file.Length == 0) throw new ValidationException("Vui lòng tải lên bản scan hợp đồng đã ký.");
        if (file.Length > MaxBytes || file.Content.Length > MaxBytes) throw new ValidationException("Bản scan hợp đồng tối đa 10 MB.");
        var bytes = file.Content;
        if (StartsWith(bytes, "%PDF-"u8)) return ("application/pdf", ".pdf");
        if (StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ("image/png", ".png");
        if (StartsWith(bytes, [0xFF, 0xD8, 0xFF])) return ("image/jpeg", ".jpg");
        throw new ValidationException("Bản scan hợp đồng phải là file PDF, JPG hoặc PNG.");
    }

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> signature) => content.AsSpan().StartsWith(signature);
}

// ===== USE CASE =====

public interface IIssueOwnerAccountUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(int id, IssueOwnerAccountRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (A9, T6): ContractPending → AccountIssued.
/// 1. Kiểm tra trạng thái, phiên bản Admin xác nhận == phiên bản Current, bản scan hợp lệ (chưa gọi ra ngoài).
/// 2. Gọi UserService U2 (Idempotency-Key theo hồ sơ): lỗi → hồ sơ giữ nguyên, Admin thử lại an toàn (503 / 409).
/// 3. Lưu bản scan + SHA-256, rồi hồ sơ + phiên bản Signed + OwnerContract + AuditLog trong 1 SaveChanges.
///    Lưu database lỗi (vd. Admin khác vừa thao tác) → xóa bản scan; thử lại dùng cùng Idempotency-Key nên không tạo trùng tài khoản.
/// </summary>
public sealed class IssueOwnerAccountUseCase(IPartnerApplicationRepository repository, IPartnerApplicationQueries queries,
    IOwnerAccountClient ownerAccounts, IFileStorage storage) : IIssueOwnerAccountUseCase
{
    public async Task<PartnerApplicationDetailDto> ExecuteAsync(int id, IssueOwnerAccountRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        PartnerApplicationSteps.EnsureIds(id, performedByUserId);
        var app = await PartnerApplicationSteps.LoadAsync(repository, id, cancellationToken);
        var before = PartnerApplicationAudit.Snapshot(app);
        var draft = app.EnsureCanIssueAccount(request.ConfirmedDraftVersion);
        var (contentType, extension) = SignedScanRules.Detect(request.SignedFile);
        var scan = request.SignedFile!.Content;

        var account = await ownerAccounts.IssueOwnerAccountAsync(new IssueOwnerAccountCommand(
                app.Id, draft.ContractNumber, app.FullName, app.Email, app.Phone, app.BusinessName, app.TaxCode, app.BusinessAddress,
                draft.BankBin, draft.BankName, draft.BankAccountNumber, draft.BankAccountName, draft.CommissionRate / 100m),
            IdempotencyKey(app.Id), cancellationToken);

        var key = PartnerContractSupport.SignedScanKey(app.Id, draft.Version, extension);
        await storage.SaveAsync(key, scan, cancellationToken);
        var contract = app.IssueAccount(performedByUserId, draft.Version, account.UserId, account.OwnerProfileId,
            new Domain.ValueObjects.StoredFile(key, contentType, PartnerContractSupport.Sha256Hex(scan)));
        repository.AddOwnerContract(contract);

        await PartnerContractFiles.SaveOrDeleteAsync(repository, storage, key,
            PartnerApplicationAudit.Build(app, "AccountIssued", performedByUserId, before, null), cancellationToken);
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
