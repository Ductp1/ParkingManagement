using System.Globalization;
using System.Security.Cryptography;
using AdminService.Domain.Entities;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== PORT: sinh file hợp đồng =====

/// <summary>File hợp đồng đã sinh (chưa lưu).</summary>
public sealed record RenderedContract(byte[] Content, string ContentType, string FileExtension);

/// <summary>
/// US-116: sinh file hợp đồng từ nội dung mẫu + giá trị placeholder ({{ContractNumber}}, {{BusinessName}}…).
/// MVP sinh HTML (không thêm package PDF); đổi sang PDF chỉ cần thay cài đặt ở Infrastructure.
/// </summary>
public interface IContractRenderer
{
    RenderedContract Render(string templateBody, int templateVersion, IReadOnlyDictionary<string, string> values);
}

// ===== PORT: lưu file =====

/// <summary>
/// US-116: lưu file hợp đồng đã sinh và bản scan đã ký. Khóa dạng "partner-applications/12/contract-v1.html".
/// Dev: thư mục local (App_Data, đã ignore); môi trường thật thay bằng object storage.
/// </summary>
public interface IFileStorage
{
    /// <summary>Ghi đè nếu khóa đã tồn tại (gọi lại sau lỗi vẫn an toàn).</summary>
    Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken);

    /// <summary>Trả null khi không có file.</summary>
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Xóa file (không có thì bỏ qua) – dùng dọn file khi lưu database thất bại.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

// ===== PORT: tạo tài khoản chủ bãi bên UserService (U2 / U3, Trường làm phía UserService) =====

/// <summary>
/// Body U2 POST api/v1/users/owner-profiles. CommissionRateOverride theo tỷ lệ thập phân như UserService (8% = 0.08).
/// </summary>
public sealed record IssueOwnerAccountCommand(int PartnerApplicationId, string ContractNumber, string FullName, string Email,
    string Phone, string BusinessName, string TaxCode, string BusinessAddress, string BankBin, string BankName,
    string BankAccountNumber, string BankAccountName, decimal CommissionRateOverride);

public sealed record IssuedOwnerAccountDto(int UserId, int OwnerProfileId);

/// <summary>
/// US-116: cổng tạo / kích hoạt tài khoản chủ bãi bên UserService (gọi qua Gateway, chuyển tiếp JWT Admin).
/// Lỗi: 401 → AuthenticationException, 403 → ForbiddenException, trùng email / SĐT / MST → ConflictException,
/// không gọi được hoặc UserService chưa có endpoint → DependencyUnavailableException (503).
/// </summary>
public interface IOwnerAccountClient
{
    /// <summary>U2 (T6). Cùng idempotencyKey gọi lại trả đúng tài khoản đã tạo, không tạo trùng.</summary>
    Task<IssuedOwnerAccountDto> IssueOwnerAccountAsync(IssueOwnerAccountCommand command, string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>U3 (T7): thu hồi link cũ, tạo link kích hoạt mới và gửi email. Chủ bãi không tồn tại → NotFoundException.</summary>
    Task ResendActivationAsync(int ownerProfileId, CancellationToken cancellationToken);
}

// ===== Dùng chung cho các use case hợp đồng =====

/// <summary>File trả về cho client (xem trước / tải hợp đồng).</summary>
public sealed record ContractFileResult(byte[] Content, string ContentType, string FileName);

/// <summary>File Admin tải lên. Length = kích thước khai báo; Content chỉ được đọc khi không vượt giới hạn (rỗng nếu quá lớn).</summary>
public sealed record UploadedFile(string FileName, long Length, byte[] Content);

/// <summary>Khung điều khoản hợp đồng trong body A5 / A6 / A7.</summary>
public sealed record PartnerBankAccountRequest(string? Bin, string? Name, string? AccountNumber, string? AccountName);

internal static class PartnerContractSupport
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly TodayInVietnam(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc + VietnamOffset);

    public static string Sha256Hex(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    public static string ContractKey(int applicationId, int version, string extension)
        => $"partner-applications/{applicationId}/contract-v{version}{extension}";

    public static string SignedScanKey(int applicationId, int version, string extension)
        => $"partner-applications/{applicationId}/signed-v{version}{extension}";

    public static ContractTerms Terms(decimal? commissionRate, DateOnly? effectiveFrom, int? termMonths, PartnerBankAccountRequest? bank,
        DateTime nowUtc)
        => ContractTerms.Create(commissionRate, effectiveFrom, termMonths, bank?.Bin, bank?.Name, bank?.AccountNumber,
            bank?.AccountName, TodayInVietnam(nowUtc));

    /// <summary>Giá trị placeholder của mẫu hợp đồng (đã định dạng kiểu Việt Nam).</summary>
    public static IReadOnlyDictionary<string, string> Values(PartnerApplication app, string contractNumber, int version,
        ContractTerms terms, DateTime nowUtc)
    {
        static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["ContractNumber"] = contractNumber,
            ["Version"] = version.ToString(CultureInfo.InvariantCulture),
            ["IssuedDate"] = Date(TodayInVietnam(nowUtc)),
            ["ApplicationCode"] = app.Code,
            ["BusinessName"] = app.BusinessName,
            ["TaxCode"] = app.TaxCode,
            ["BusinessAddress"] = app.BusinessAddress,
            ["LegalRepresentativeName"] = app.LegalRepresentativeName,
            ["LegalRepresentativePosition"] = app.LegalRepresentativePosition ?? string.Empty,
            ["ContactName"] = app.FullName,
            ["Email"] = app.Email,
            ["Phone"] = app.Phone,
            ["LotCount"] = app.LotCount.ToString(CultureInfo.InvariantCulture),
            ["PrimaryLotAddress"] = app.PrimaryLotAddress,
            ["CommissionRate"] = terms.CommissionRate.ToString("0.##", CultureInfo.InvariantCulture),
            ["EffectiveFrom"] = Date(terms.EffectiveFrom),
            ["EffectiveTo"] = Date(terms.EffectiveTo),
            ["TermMonths"] = terms.TermMonths.ToString(CultureInfo.InvariantCulture),
            ["BankBin"] = terms.BankBin,
            ["BankName"] = terms.BankName,
            ["BankAccountNumber"] = terms.BankAccountNumber,
            ["BankAccountName"] = terms.BankAccountName,
        };
    }

    public static async Task<ContractTemplate> ActiveTemplateAsync(IPartnerApplicationRepository repository, CancellationToken ct)
        => await repository.GetActiveTemplateAsync(ct)
           ?? throw new ConflictException("Chưa có mẫu hợp đồng đang áp dụng, chưa thể sinh hợp đồng.");
}
