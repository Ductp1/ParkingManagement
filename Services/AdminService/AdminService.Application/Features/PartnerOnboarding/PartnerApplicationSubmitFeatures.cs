using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== REQUEST =====

/// <summary>
/// T1 (US-116 / US-053): form "Đăng ký đối tác" 4 phần – Liên hệ · Doanh nghiệp · Bãi đỗ · Đồng ý xử lý dữ liệu (Nghị định 13/2023).
/// Mọi trường là chuỗi thô từ form; chuẩn hóa + kiểm tra ở <see cref="PartnerApplicationInput"/>.
/// </summary>
public sealed record SubmitPartnerApplicationRequest(
    string? FullName, string? Email, string? Phone, string? Position,
    PartnerBusinessType? BusinessType, string? BusinessName, string? TaxCode, string? BusinessAddress,
    string? LegalRepresentativeName, string? LegalRepresentativePosition,
    int LotCount, string? PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours,
    bool ConsentAccepted);

/// <summary>
/// Admin nhập hồ sơ thay đối tác (nhận qua điện thoại / hotline). ConsentAccepted = Admin xác nhận đối tác đã đồng ý xử lý dữ liệu
/// qua điện thoại; IntakeNote (tùy chọn) ghi nguồn cuộc gọi, lưu vào lý do của audit log.
/// </summary>
public sealed record AdminCreatePartnerApplicationRequest(
    string? FullName, string? Email, string? Phone, string? Position,
    PartnerBusinessType? BusinessType, string? BusinessName, string? TaxCode, string? BusinessAddress,
    string? LegalRepresentativeName, string? LegalRepresentativePosition,
    int LotCount, string? PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours,
    bool ConsentAccepted, string? IntakeNote)
{
    public SubmitPartnerApplicationRequest ToSubmit() => new(FullName, Email, Phone, Position, BusinessType, BusinessName, TaxCode,
        BusinessAddress, LegalRepresentativeName, LegalRepresentativePosition, LotCount, PrimaryLotAddress, EstimatedCarSlots,
        EstimatedMotorbikeSlots, OperatingHours, ConsentAccepted);
}

/// <summary>Phản hồi của form public: chỉ mã hồ sơ + trạng thái, không trả lại dữ liệu cá nhân.</summary>
public sealed record PartnerApplicationReceiptDto(string Code, string Status, DateTime SubmittedAtUtc);

// ===== Kiểm tra + chuẩn hóa =====

/// <summary>Dữ liệu hồ sơ đã chuẩn hóa (trim, email chữ thường, SĐT dạng 0xxxxxxxxx, MST bỏ khoảng trắng).</summary>
public sealed record PartnerApplicationInput(
    string FullName, string Email, string Phone, string? Position,
    PartnerBusinessType BusinessType, string BusinessName, string TaxCode, string BusinessAddress,
    string LegalRepresentativeName, string? LegalRepresentativePosition,
    int LotCount, string PrimaryLotAddress, int? EstimatedCarSlots, int? EstimatedMotorbikeSlots, string? OperatingHours)
{
    public const int MaxLots = 100;
    public const int MaxSlots = 10_000;

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    /// <summary>Di động / cố định VN: 0 + 9–10 chữ số (nhận cả +84 / 84 ở đầu, dấu cách, chấm, gạch).</summary>
    private static readonly Regex PhonePattern = new(@"^0\d{9,10}$", RegexOptions.Compiled);
    /// <summary>MST doanh nghiệp 10 số, chi nhánh 10-3 số; hộ kinh doanh có thể dùng MST cá nhân 12 số (số CCCD).</summary>
    private static readonly Regex TaxCodePattern = new(@"^(\d{10}(-\d{3})?|\d{12})$", RegexOptions.Compiled);

    public static PartnerApplicationInput From(SubmitPartnerApplicationRequest r)
    {
        var errors = new List<string>();

        string Text(string? value, string field, int min, int max)
        {
            var text = value?.Trim() ?? string.Empty;
            if (text.Length < min || text.Length > max) errors.Add($"{field} phải dài từ {min} đến {max} ký tự.");
            return text;
        }

        string? Optional(string? value, string field, int max)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length > max) errors.Add($"{field} tối đa {max} ký tự.");
            return text;
        }

        int? Slots(int? value, string field)
        {
            if (value is < 0 or > MaxSlots) errors.Add($"{field} phải từ 0 đến {MaxSlots.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}.");
            return value;
        }

        var fullName = Text(r.FullName, "Họ và tên người liên hệ", 2, 200);
        var email = (r.Email?.Trim() ?? string.Empty).ToLowerInvariant();
        if (email.Length is 0 or > 254 || !EmailPattern.IsMatch(email)) errors.Add("Email không hợp lệ.");
        var phone = NormalizePhone(r.Phone);
        if (!PhonePattern.IsMatch(phone)) errors.Add("Số điện thoại không hợp lệ (ví dụ 0901234567).");
        var position = Optional(r.Position, "Chức vụ người liên hệ", 100);

        if (r.BusinessType is not { } businessType || !Enum.IsDefined(businessType))
        {
            errors.Add("Loại hình kinh doanh phải là Company hoặc HouseholdBusiness.");
            businessType = PartnerBusinessType.Company;
        }
        var businessName = Text(r.BusinessName, "Tên doanh nghiệp / hộ kinh doanh", 2, 300);
        var taxCode = (r.TaxCode ?? string.Empty).Replace(" ", string.Empty).Trim();
        if (!TaxCodePattern.IsMatch(taxCode)) errors.Add("Mã số thuế phải gồm 10 số, 10-3 số (chi nhánh) hoặc 12 số (cá nhân).");
        var businessAddress = Text(r.BusinessAddress, "Địa chỉ kinh doanh", 5, 300);
        var legalName = Text(r.LegalRepresentativeName, "Người đại diện theo pháp luật", 2, 200);
        var legalPosition = Optional(r.LegalRepresentativePosition, "Chức vụ người đại diện", 100);

        if (r.LotCount is < 1 or > MaxLots) errors.Add($"Số bãi đỗ phải từ 1 đến {MaxLots}.");
        var lotAddress = Text(r.PrimaryLotAddress, "Địa chỉ bãi chính", 5, 300);
        var cars = Slots(r.EstimatedCarSlots, "Số chỗ ô tô");
        var motorbikes = Slots(r.EstimatedMotorbikeSlots, "Số chỗ xe máy");
        var hours = Optional(r.OperatingHours, "Giờ hoạt động", 100);

        if (!r.ConsentAccepted) errors.Add("Cần đồng ý cho ParkMaster xử lý dữ liệu đăng ký (Nghị định 13/2023).");
        if (errors.Count > 0) throw new ValidationException(string.Join(" ", errors));

        return new PartnerApplicationInput(fullName, email, phone, position, businessType, businessName, taxCode, businessAddress,
            legalName, legalPosition, r.LotCount, lotAddress, cars, motorbikes, hours);
    }

    /// <summary>"+84 901.234.567" → "0901234567".</summary>
    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84", StringComparison.Ordinal)) digits = "0" + digits[3..];
        else if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11) digits = "0" + digits[2..];
        return digits.Replace("+", string.Empty);
    }

    /// <summary>Hồ sơ mới trạng thái Submitted (T1); mã hồ sơ do repository gán khi lưu.</summary>
    public PartnerApplication ToApplication(DateTime consentAcceptedAtUtc)
    {
        var app = PartnerApplication.Submit(string.Empty, consentAcceptedAtUtc);
        app.FullName = FullName;
        app.Email = Email;
        app.Phone = Phone;
        app.Position = Position;
        app.BusinessType = BusinessType;
        app.BusinessName = BusinessName;
        app.TaxCode = TaxCode;
        app.BusinessAddress = BusinessAddress;
        app.LegalRepresentativeName = LegalRepresentativeName;
        app.LegalRepresentativePosition = LegalRepresentativePosition;
        app.LotCount = LotCount;
        app.PrimaryLotAddress = PrimaryLotAddress;
        app.EstimatedCarSlots = EstimatedCarSlots;
        app.EstimatedMotorbikeSlots = EstimatedMotorbikeSlots;
        app.OperatingHours = OperatingHours;
        return app;
    }
}

/// <summary>Mã hồ sơ hiển thị PA-{năm}-{số thứ tự 4 chữ số trong năm}, ví dụ PA-2026-0012.</summary>
public static class PartnerApplicationCode
{
    public static string Prefix(int year) => $"PA-{year}-";

    /// <summary>Mã kế tiếp sau mã lớn nhất của năm (null = hồ sơ đầu tiên của năm).</summary>
    public static string Next(int year, string? lastCodeOfYear)
    {
        var prefix = Prefix(year);
        var last = lastCodeOfYear is not null && lastCodeOfYear.StartsWith(prefix, StringComparison.Ordinal)
                   && int.TryParse(lastCodeOfYear[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : 0;
        return $"{prefix}{last + 1:0000}";
    }
}

// ===== PORT =====

/// <summary>Trường bị trùng với một hồ sơ đang xử lý (chưa Activated / Rejected / Cancelled).</summary>
public sealed record OpenApplicationMatch(string Code, bool Email, bool Phone, bool TaxCode);

public interface IPartnerApplicationIntakeRepository
{
    /// <summary>Hồ sơ đang xử lý trùng email, SĐT hoặc MST (null = không trùng).</summary>
    Task<OpenApplicationMatch?> FindOpenDuplicateAsync(string email, string phone, string taxCode, CancellationToken cancellationToken);

    /// <summary>
    /// Gán mã PA-{năm}-NNNN, lưu hồ sơ rồi audit log (cần Id) trong CÙNG 1 transaction. Trùng mã do 2 request cùng lúc → thử mã kế tiếp.
    /// </summary>
    Task AddSubmittedAsync(PartnerApplication application, int year, Func<PartnerApplication, AuditLog> buildAuditLog,
        CancellationToken cancellationToken);
}

// ===== USE CASE =====

internal static class PartnerIntake
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<PartnerApplication> CreateAsync(IPartnerApplicationIntakeRepository repository, PartnerApplicationInput input,
        DateTime nowUtc, int? adminUserId, string action, string? reason, Func<OpenApplicationMatch, string> duplicateMessage,
        CancellationToken ct)
    {
        if (await repository.FindOpenDuplicateAsync(input.Email, input.Phone, input.TaxCode, ct) is { } match)
            throw new ConflictException(duplicateMessage(match));

        var app = input.ToApplication(nowUtc);
        await repository.AddSubmittedAsync(app, nowUtc.Year, saved => new AuditLog
        {
            SourceService = "AdminService",
            UserId = adminUserId,
            Action = $"{PartnerApplicationAudit.EntityName}.{action}",
            EntityName = PartnerApplicationAudit.EntityName,
            EntityId = saved.Id.ToString(CultureInfo.InvariantCulture),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                code = saved.Code,
                status = saved.Status.ToString(),
                businessName = saved.BusinessName,
                taxCode = saved.TaxCode,
                lotCount = saved.LotCount,
            }, Json),
            Reason = reason,
        }, ct);
        return app;
    }

    /// <summary>Tên trường trùng, không lộ mã hồ sơ của người khác (dùng cho form public).</summary>
    public static string Fields(OpenApplicationMatch m)
        => string.Join(", ", new[] { m.Email ? "email" : null, m.Phone ? "số điện thoại" : null, m.TaxCode ? "mã số thuế" : null }
            .Where(f => f is not null));
}

public interface ISubmitPartnerApplicationUseCase
{
    Task<PartnerApplicationReceiptDto> ExecuteAsync(SubmitPartnerApplicationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 T1 (P1, form public – không đăng nhập): tạo hồ sơ Submitted để Admin thẩm định ở Partner Onboarding.
/// Trùng email / SĐT / MST với hồ sơ đang xử lý → 409 (không nêu mã hồ sơ). Chưa kiểm tra trùng tài khoản UserService (U1 chưa có).
/// </summary>
public sealed class SubmitPartnerApplicationUseCase(IPartnerApplicationIntakeRepository repository, TimeProvider clock)
    : ISubmitPartnerApplicationUseCase
{
    public async Task<PartnerApplicationReceiptDto> ExecuteAsync(SubmitPartnerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = PartnerApplicationInput.From(request ?? throw new ValidationException("Thiếu dữ liệu hồ sơ."));
        var now = clock.GetUtcNow().UtcDateTime;
        var app = await PartnerIntake.CreateAsync(repository, input, now, adminUserId: null, "Submitted", "Đối tác tự nộp qua form đăng ký",
            m => $"Đã có hồ sơ đối tác đang được xử lý với {PartnerIntake.Fields(m)} này. Vui lòng liên hệ hotline 1900 6868 để được hỗ trợ.",
            cancellationToken);
        return new PartnerApplicationReceiptDto(app.Code, app.Status.ToString(), now);
    }
}

public interface ICreatePartnerApplicationUseCase
{
    Task<PartnerApplicationDetailDto> ExecuteAsync(AdminCreatePartnerApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 T1 do Admin nhập thay đối tác (nhận qua điện thoại): cùng kiểm tra như form public; trùng → 409 kèm mã hồ sơ đang xử lý.
/// Hồ sơ vào trạng thái Submitted như hồ sơ tự nộp, audit log ghi Admin + ghi chú cuộc gọi.
/// </summary>
public sealed class CreatePartnerApplicationUseCase(IPartnerApplicationIntakeRepository repository, IPartnerApplicationQueries queries,
    TimeProvider clock) : ICreatePartnerApplicationUseCase
{
    public const int MaxIntakeNoteLength = 1000;

    public async Task<PartnerApplicationDetailDto> ExecuteAsync(AdminCreatePartnerApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (request is null) throw new ValidationException("Thiếu dữ liệu hồ sơ.");
        var note = request.IntakeNote?.Trim();
        if (note?.Length > MaxIntakeNoteLength) throw new ValidationException($"Ghi chú tiếp nhận tối đa {MaxIntakeNoteLength} ký tự.");
        var input = PartnerApplicationInput.From(request.ToSubmit());

        var app = await PartnerIntake.CreateAsync(repository, input, clock.GetUtcNow().UtcDateTime, performedByUserId, "CreatedByAdmin",
            string.IsNullOrEmpty(note) ? "Admin nhập hồ sơ thay đối tác" : $"Admin nhập hồ sơ thay đối tác: {note}",
            m => $"Hồ sơ {m.Code} đang được xử lý với cùng {PartnerIntake.Fields(m)}. Mở hồ sơ đó thay vì tạo mới.",
            cancellationToken);
        return await PartnerApplicationSteps.ReloadAsync(queries, app.Id, cancellationToken);
    }
}
