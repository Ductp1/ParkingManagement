using System.Net.Mail;
using System.Text.RegularExpressions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST / DTO =====

/// <summary>Form đăng ký đối tác chủ bãi (US-053, public, không cần đăng nhập). Giấy tờ đi riêng trong <see cref="PartnerDocumentUpload"/>.</summary>
public sealed record SubmitPartnerApplicationRequest(
    string? FullName, string? Email, string? Phone, string? Position,
    PartnerBusinessType? BusinessType, string? BusinessName, string? TaxCode, string? BusinessAddress,
    string? LegalRepresentativeName, string? LegalRepresentativePosition,
    int? LotCount, string? PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours,
    bool ConsentAccepted);

/// <summary>Một file giấy tờ kèm loại giấy tờ.</summary>
public sealed record PartnerDocumentUpload(PartnerDocumentType Type, UploadedFile File);

/// <summary>Biên nhận trả cho người nộp: chỉ mã hồ sơ và trạng thái, không trả lại giấy tờ hay id nội bộ.</summary>
public sealed record PartnerApplicationReceiptDto(string Code, string Status, DateTime SubmittedAtUtc);

/// <summary>Giấy tờ của hồ sơ (chỉ Admin xem).</summary>
public sealed record PartnerDocumentDto(int Id, string DocumentType, string OriginalFileName, string ContentType, long SizeBytes,
    DateTime UploadedAtUtc);

/// <summary>Dòng giấy tờ đọc từ database (kèm khóa file để đọc nội dung).</summary>
public sealed record PartnerDocumentRecord(int Id, int PartnerApplicationId, PartnerDocumentType DocumentType, string FileKey,
    string ContentType, string OriginalFileName, long SizeBytes, DateTime CreatedAtUtc);

// ===== PORT =====

public interface IPartnerRegistrationRepository
{
    /// <summary>Đã có hồ sơ chưa kết thúc (không phải Activated / Rejected / Cancelled) cùng mã số thuế hoặc email.</summary>
    Task<bool> HasOpenApplicationAsync(string taxCode, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Sinh mã PA-{năm}-{số thứ tự} rồi lưu hồ sơ + giấy tờ trong một SaveChanges.
    /// Trùng mã (hai người nộp cùng lúc) thì lấy số kế tiếp và thử lại. Trả mã đã lưu.
    /// </summary>
    Task<string> AddWithNewCodeAsync(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents, int year,
        CancellationToken cancellationToken);
}

public interface IPartnerDocumentQueries
{
    Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerDocumentRecord>> ListAsync(int applicationId, CancellationToken cancellationToken);

    Task<PartnerDocumentRecord?> FindAsync(int applicationId, int documentId, CancellationToken cancellationToken);
}

// ===== QUY TẮC GIẤY TỜ =====

/// <summary>
/// Giấy tờ pháp lý: PDF / JPG / PNG nhận diện theo nội dung file, mỗi file ≤ 10 MB, tối đa 6 file và 25 MB mỗi hồ sơ
/// (Gateway giới hạn body ~30 MB). Bắt buộc có giấy đăng ký kinh doanh và giấy tờ tùy thân của người đại diện.
/// </summary>
public static class PartnerDocumentRules
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxTotalBytes = 25 * 1024 * 1024;
    public const int MaxFiles = 6;

    public static readonly PartnerDocumentType[] Required = [PartnerDocumentType.BusinessLicense, PartnerDocumentType.RepresentativeId];

    public static string Label(PartnerDocumentType type) => type switch
    {
        PartnerDocumentType.BusinessLicense => "Giấy đăng ký kinh doanh",
        PartnerDocumentType.RepresentativeId => "Giấy tờ tùy thân của người đại diện",
        PartnerDocumentType.LotLegalProof => "Giấy tờ quyền sử dụng mặt bằng",
        _ => "Giấy tờ khác",
    };

    public static void EnsureSet(IReadOnlyList<PartnerDocumentUpload> documents)
    {
        if (documents.Count > MaxFiles) throw new ValidationException($"Tối đa {MaxFiles} file giấy tờ cho mỗi hồ sơ.");
        foreach (var type in Required)
        {
            if (!documents.Any(d => d.Type == type))
                throw new ValidationException($"Vui lòng tải lên {Label(type).ToLowerInvariant()}.");
        }
        if (documents.Any(d => !Enum.IsDefined(d.Type))) throw new ValidationException("Loại giấy tờ không hợp lệ.");
        if (documents.Sum(d => d.File.Length) > MaxTotalBytes) throw new ValidationException("Tổng dung lượng giấy tờ tối đa 25 MB.");
    }

    /// <summary>Kiểm tra một file, trả (MIME, phần mở rộng).</summary>
    public static (string ContentType, string Extension) Detect(PartnerDocumentUpload document)
    {
        var file = document.File;
        var label = Label(document.Type);
        // Kiểm tra kích thước trước: file quá lớn không được đọc vào bộ nhớ (Content rỗng).
        if (file.Length > MaxFileBytes || file.Content.Length > MaxFileBytes) throw new ValidationException($"{label}: mỗi file tối đa 10 MB.");
        if (file.Length == 0 || file.Content.Length == 0) throw new ValidationException($"{label}: file rỗng.");
        var bytes = file.Content.AsSpan();
        if (bytes.StartsWith("%PDF-"u8)) return ("application/pdf", ".pdf");
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ("image/png", ".png");
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return ("image/jpeg", ".jpg");
        throw new ValidationException($"{label}: chỉ nhận file PDF, JPG hoặc PNG.");
    }

    /// <summary>Tên file hiển thị: bỏ đường dẫn, ký tự điều khiển; tối đa 200 ký tự.</summary>
    public static string SafeFileName(string? name, string fallback)
    {
        var baseName = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last());
        var cleaned = new string(baseName.Where(c => !char.IsControl(c) && c is not '"').ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = fallback;
        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    public static string Slug(PartnerDocumentType type) => type switch
    {
        PartnerDocumentType.BusinessLicense => "business-license",
        PartnerDocumentType.RepresentativeId => "representative-id",
        PartnerDocumentType.LotLegalProof => "lot-legal-proof",
        _ => "other",
    };
}

// ===== USE CASE: nộp hồ sơ (public) =====

public interface ISubmitPartnerApplicationUseCase
{
    Task<PartnerApplicationReceiptDto> ExecuteAsync(SubmitPartnerApplicationRequest request, IReadOnlyList<PartnerDocumentUpload> documents,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-053 (T1): người muốn làm chủ bãi nộp hồ sơ + giấy tờ pháp lý. Hồ sơ ở trạng thái Submitted và chờ Admin
/// bắt đầu thẩm định (T2) rồi mới được cấp tài khoản (T6). File lưu trước, lưu database lỗi thì xóa file đã ghi.
/// </summary>
public sealed partial class SubmitPartnerApplicationUseCase(
    IPartnerRegistrationRepository repository,
    IFileStorage storage,
    TimeProvider timeProvider) : ISubmitPartnerApplicationUseCase
{
    public async Task<PartnerApplicationReceiptDto> ExecuteAsync(SubmitPartnerApplicationRequest request,
        IReadOnlyList<PartnerDocumentUpload> documents, CancellationToken cancellationToken = default)
    {
        if (!request.ConsentAccepted) throw new ValidationException("Vui lòng đồng ý với điều khoản xử lý dữ liệu để gửi hồ sơ.");

        var fullName = Text(request.FullName, "Họ tên người đăng ký", 200);
        var email = Email(request.Email);
        var phone = Phone(request.Phone);
        var businessType = request.BusinessType is { } bt && Enum.IsDefined(bt)
            ? bt
            : throw new ValidationException("Vui lòng chọn loại hình doanh nghiệp.");
        var businessName = Text(request.BusinessName, "Tên doanh nghiệp / hộ kinh doanh", 300);
        var taxCode = TaxCode(request.TaxCode);
        var businessAddress = Text(request.BusinessAddress, "Địa chỉ trụ sở", 500);
        var representative = Text(request.LegalRepresentativeName, "Người đại diện pháp luật", 200);
        var lotCount = request.LotCount is >= 1 and <= 100
            ? request.LotCount.Value
            : throw new ValidationException("Số bãi dự kiến phải từ 1 đến 100.");
        var primaryLotAddress = Text(request.PrimaryLotAddress, "Địa chỉ bãi chính", 500);
        var carSlots = Slots(request.EstimatedCarSlots, "Số chỗ ô tô");
        var motorbikeSlots = Slots(request.EstimatedMotorbikeSlots, "Số chỗ xe máy");

        PartnerDocumentRules.EnsureSet(documents);
        var detected = documents.Select(d => (Upload: d, Kind: PartnerDocumentRules.Detect(d))).ToList();

        if (await repository.HasOpenApplicationAsync(taxCode, email, cancellationToken))
            throw new ConflictException("Mã số thuế hoặc email này đã có hồ sơ đang chờ xử lý. Vui lòng chờ Admin phản hồi.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = PartnerApplication.Submit(string.Empty, now);
        app.FullName = fullName;
        app.Email = email;
        app.Phone = phone;
        app.Position = Optional(request.Position, "Chức vụ", 100);
        app.BusinessType = businessType;
        app.BusinessName = businessName;
        app.TaxCode = taxCode;
        app.BusinessAddress = businessAddress;
        app.LegalRepresentativeName = representative;
        app.LegalRepresentativePosition = Optional(request.LegalRepresentativePosition, "Chức vụ người đại diện", 100);
        app.LotCount = lotCount;
        app.PrimaryLotAddress = primaryLotAddress;
        app.EstimatedCarSlots = carSlots;
        app.EstimatedMotorbikeSlots = motorbikeSlots;
        app.OperatingHours = Optional(request.OperatingHours, "Giờ hoạt động", 100);

        // Khóa file do server sinh (thư mục GUID vì hồ sơ chưa có id), không dùng tên file của client.
        var folder = $"partner-applications/{Guid.NewGuid():N}";
        var saved = new List<string>();
        var entities = new List<PartnerApplicationDocument>();
        try
        {
            var index = 0;
            foreach (var (upload, kind) in detected)
            {
                index++;
                var key = $"{folder}/{PartnerDocumentRules.Slug(upload.Type)}-{index}{kind.Extension}";
                await storage.SaveAsync(key, upload.File.Content, cancellationToken);
                saved.Add(key);
                var fileName = PartnerDocumentRules.SafeFileName(upload.File.FileName, $"{PartnerDocumentRules.Slug(upload.Type)}{kind.Extension}");
                entities.Add(PartnerApplicationDocument.Create(app, upload.Type, key, kind.ContentType, fileName,
                    upload.File.Content.Length, ContractSupport.Sha256Hex(upload.File.Content)));
            }

            var code = await repository.AddWithNewCodeAsync(app, entities, now.Year, cancellationToken);
            return new PartnerApplicationReceiptDto(code, app.Status.ToString(), now);
        }
        catch
        {
            foreach (var key in saved)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch (IOException) { /* dọn file thất bại không che lỗi gốc */ }
            }
            throw;
        }
    }

    private static string Text(string? value, string field, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new ValidationException($"{field} không được để trống.");
        if (text.Length > max) throw new ValidationException($"{field} tối đa {max} ký tự.");
        return text;
    }

    private static string? Optional(string? value, string field, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > max) throw new ValidationException($"{field} tối đa {max} ký tự.");
        return text;
    }

    private static string Email(string? value)
    {
        var text = Text(value, "Email", 254).ToLowerInvariant();
        if (!MailAddress.TryCreate(text, out var parsed) || parsed.Address != text || !text.Contains('.'))
            throw new ValidationException("Email không hợp lệ.");
        return text;
    }

    private static string Phone(string? value)
    {
        var text = Regex.Replace(Text(value, "Số điện thoại", 20), @"[\s.\-]", string.Empty);
        if (!PhonePattern().IsMatch(text)) throw new ValidationException("Số điện thoại không hợp lệ (ví dụ 0901234567).");
        return text;
    }

    private static string TaxCode(string? value)
    {
        var text = Text(value, "Mã số thuế", 20).Replace(" ", string.Empty);
        if (!TaxCodePattern().IsMatch(text)) throw new ValidationException("Mã số thuế phải gồm 10 số hoặc 10 số kèm -3 số chi nhánh.");
        return text;
    }

    private static int? Slots(int? value, string field) => value switch
    {
        null => null,
        < 0 or > 10_000 => throw new ValidationException($"{field} phải từ 0 đến 10.000."),
        _ => value,
    };

    [GeneratedRegex(@"^(\+84|0)\d{9,10}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex TaxCodePattern();
}

// ===== USE CASE: Admin xem giấy tờ =====

public interface IListPartnerDocumentsUseCase
{
    Task<IReadOnlyList<PartnerDocumentDto>> ExecuteAsync(int applicationId, CancellationToken cancellationToken = default);
}

/// <summary>US-053 / US-116: Admin xem danh sách giấy tờ của hồ sơ đối tác.</summary>
public sealed class ListPartnerDocumentsUseCase(IPartnerDocumentQueries queries) : IListPartnerDocumentsUseCase
{
    public async Task<IReadOnlyList<PartnerDocumentDto>> ExecuteAsync(int applicationId, CancellationToken cancellationToken = default)
    {
        if (applicationId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        if (!await queries.ApplicationExistsAsync(applicationId, cancellationToken)) throw new NotFoundException("Hồ sơ đối tác", applicationId);
        var rows = await queries.ListAsync(applicationId, cancellationToken);
        return rows.Select(r => new PartnerDocumentDto(r.Id, r.DocumentType.ToString(), r.OriginalFileName, r.ContentType, r.SizeBytes,
            r.CreatedAtUtc)).ToList();
    }
}

public interface IGetPartnerDocumentFileUseCase
{
    Task<ContractFileResult> ExecuteAsync(int applicationId, int documentId, CancellationToken cancellationToken = default);
}

/// <summary>US-053 / US-116: Admin mở một giấy tờ (nội dung file). File mất trong kho → 404.</summary>
public sealed class GetPartnerDocumentFileUseCase(IPartnerDocumentQueries queries, IFileStorage storage) : IGetPartnerDocumentFileUseCase
{
    public async Task<ContractFileResult> ExecuteAsync(int applicationId, int documentId, CancellationToken cancellationToken = default)
    {
        if (applicationId <= 0 || documentId <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var doc = await queries.FindAsync(applicationId, documentId, cancellationToken)
                  ?? throw new NotFoundException("Giấy tờ", documentId);
        var content = await storage.ReadAsync(doc.FileKey, cancellationToken) ?? throw new NotFoundException("File giấy tờ", documentId);
        return new ContractFileResult(content, doc.ContentType, doc.OriginalFileName);
    }
}
