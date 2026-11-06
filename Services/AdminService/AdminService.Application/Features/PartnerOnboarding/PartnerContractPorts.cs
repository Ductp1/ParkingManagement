using System.Security.Cryptography;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== PORT: sinh file hợp đồng =====

/// <summary>File hợp đồng đã sinh (chưa lưu).</summary>
public sealed record RenderedContract(byte[] Content, string ContentType, string FileExtension);

/// <summary>
/// US-116: sinh file hợp đồng từ nội dung mẫu + giá trị placeholder ({{ContractNumber}}, {{LotName}}…).
/// MVP sinh HTML (không thêm package PDF); đổi sang PDF chỉ cần thay cài đặt ở Infrastructure.
/// </summary>
public interface IContractRenderer
{
    /// <param name="previewWatermark">true = bản xem trước: đóng dấu "BẢN XEM TRƯỚC – KHÔNG CÓ GIÁ TRỊ PHÁP LÝ".</param>
    RenderedContract Render(string templateBody, int templateVersion, IReadOnlyDictionary<string, string> values, bool previewWatermark);
}

// ===== PORT: lưu file =====

/// <summary>
/// US-116: lưu file hợp đồng đã sinh và bản scan đã ký. Khóa dạng "lot-contracts/12/contract-v1.html".
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

/// <summary>Body U2 POST api/v1/users/owner-profiles: thông tin pháp nhân + tài khoản nhận thanh toán mặc định (không còn hoa hồng).</summary>
public sealed record IssueOwnerAccountCommand(int PartnerApplicationId, string FullName, string Email, string Phone,
    string BusinessName, string TaxCode, string BusinessAddress, string BankBin, string BankName, string BankAccountNumber,
    string BankAccountName);

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

// ===== Dùng chung =====

/// <summary>File trả về cho client (xem trước / tải hợp đồng).</summary>
public sealed record ContractFileResult(byte[] Content, string ContentType, string FileName);

/// <summary>File Admin tải lên. Length = kích thước khai báo; Content chỉ được đọc khi không vượt giới hạn (rỗng nếu quá lớn).</summary>
public sealed record UploadedFile(string FileName, long Length, byte[] Content);

/// <summary>Tài khoản nhận thanh toán trong body (T6 issue-account).</summary>
public sealed record PartnerBankAccountRequest(string? Bin, string? Name, string? AccountNumber, string? AccountName);

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

/// <summary>Tiện ích dùng chung cho hồ sơ đối tác và hợp đồng bãi.</summary>
public static class ContractSupport
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly TodayInVietnam(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc + VietnamOffset);

    public static string Sha256Hex(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
