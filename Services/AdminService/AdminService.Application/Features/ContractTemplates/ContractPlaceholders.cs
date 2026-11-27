using System.Text.RegularExpressions;
using AdminService.Application.Features.LotContracts;
using AdminService.Domain.Entities;
using AdminService.Domain.ValueObjects;

namespace AdminService.Application.Features.ContractTemplates;

/// <summary>
/// US-116: danh mục placeholder của mẫu hợp đồng bãi – nguồn duy nhất cho: kiểm tra mẫu (placeholder lạ, placeholder bắt buộc),
/// dữ liệu mẫu khi xem trước bản nháp, và danh sách chèn nhanh trên màn hình sửa mẫu. Giá trị thật lấy từ LotContractSupport.Values.
/// </summary>
public static partial class ContractPlaceholders
{
    public const string SpecialTermsKey = "SpecialTerms";
    public const string NoSpecialTerms = "Không có thỏa thuận riêng.";

    private sealed record Definition(string Key, string Group, string Description, bool Required = false, bool Block = false);

    private static readonly Definition[] Definitions =
    [
        new("ContractNumber", "Contract", "Contract number, same for every version", Required: true),
        new("Version", "Contract", "Version number of this contract file"),
        new("VersionNote", "Contract", "\"Phiên bản 2 – thay thế phiên bản 1 ngày …\""),
        new("IssuedDate", "Contract", "Issue date dd/MM/yyyy"),
        new("IssuedDateLong", "Contract", "Issue date in words: \"ngày 21 tháng 10 năm 2026\""),
        new("PlatformName", "Party A", "Platform company name (template field)"),
        new("PlatformAddress", "Party A", "Platform head-office address"),
        new("PlatformTaxCode", "Party A", "Platform enterprise code"),
        new("PlatformPhone", "Party A", "Platform phone"),
        new("PlatformEmail", "Party A", "Platform email"),
        new("PlatformRepresentativeName", "Party A", "Platform representative"),
        new("PlatformRepresentativePosition", "Party A", "Platform representative's position"),
        new("ApplicationCode", "Party B", "Partner application code"),
        new("BusinessName", "Party B", "Partner business name", Required: true),
        new("TaxCode", "Party B", "Partner enterprise / tax code"),
        new("BusinessAddress", "Party B", "Partner head-office address"),
        new("LegalRepresentativeName", "Party B", "Partner legal representative"),
        new("LegalRepresentativePosition", "Party B", "Partner representative's position"),
        new("Email", "Party B", "Partner email"),
        new("Phone", "Party B", "Partner phone"),
        new("LotName", "Parking lot", "Lot name", Required: true),
        new("LotAddress", "Parking lot", "Lot address, district, city"),
        new("LotCapacity", "Parking lot", "Total slots, e.g. \"120 chỗ\""),
        new("LotOpeningHours", "Parking lot", "Opening hours, e.g. \"05:00 – 23:00\" or \"24/7\""),
        new("CommissionRate", "Terms", "Platform fee %, e.g. \"7,5\"", Required: true),
        new("TermMonths", "Terms", "Term in months, e.g. \"12\""),
        new("TermMonthsText", "Terms", "Term with words, e.g. \"12 (mười hai)\""),
        new("EffectiveFrom", "Terms", "Effective from dd/MM/yyyy", Required: true),
        new("EffectiveTo", "Terms", "Last effective day dd/MM/yyyy", Required: true),
        new(SpecialTermsKey, "Terms", "Special terms of this lot, one clause per line. Must stand alone on its own line.",
            Required: true, Block: true),
        new("BankBin", "Payout account", "Bank BIN"),
        new("BankName", "Payout account", "Bank name"),
        new("BankAccountNumber", "Payout account", "Account number"),
        new("BankAccountName", "Payout account", "Account holder"),
    ];

    private static readonly HashSet<string> Known = Definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Bên A dùng cho danh mục (cột Sample) khi chưa có mẫu cụ thể.</summary>
    public static readonly PlatformParty SamplePlatform = new("CÔNG TY CỔ PHẦN CÔNG NGHỆ SMART PARKING", "Số 01 đường Mẫu, TP. Hồ Chí Minh",
        "0123456789", "1900 0000", "partner@smartparking.example", "NGUYỄN VĂN MẪU", "Tổng Giám đốc");

    public static IReadOnlyList<string> Keys { get; } = Definitions.Select(d => d.Key).ToList();

    public static IReadOnlyList<ContractPlaceholderDto> Catalog(DateTime nowUtc)
    {
        var samples = SampleValues(SamplePlatform, nowUtc);
        return Definitions
            .Select(d => new ContractPlaceholderDto(d.Key, d.Group, d.Description, samples.GetValueOrDefault(d.Key, string.Empty), d.Required,
                d.Block))
            .ToList();
    }

    /// <summary>Placeholder không có trong danh mục (theo thứ tự xuất hiện, không lặp).</summary>
    public static IReadOnlyList<string> Unknown(string body)
        => Placeholder().Matches(body).Select(m => m.Groups[1].Value).Where(k => !Known.Contains(k)).Distinct().ToList();

    /// <summary>Placeholder bắt buộc còn thiếu; khối (SpecialTerms) phải đứng riêng một dòng mới tính là có.</summary>
    public static IReadOnlyList<string> MissingRequired(string body)
    {
        var inline = Placeholder().Matches(body).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var standalone = body.Replace("\r\n", "\n").Split('\n')
            .Select(l => StandalonePlaceholder().Match(l.Trim()))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        return Definitions
            .Where(d => d.Required && !(d.Block ? standalone : inline).Contains(d.Key))
            .Select(d => d.Key)
            .ToList();
    }

    /// <summary>Giá trị mẫu để xem trước bản nháp: một hợp đồng giả định, Bên A lấy từ bản nháp.</summary>
    public static IReadOnlyDictionary<string, string> SampleValues(PlatformParty platform, DateTime nowUtc)
    {
        var contract = new LotContract
        {
            ContractNumber = "HD-SP-2026-0000",
            ParkingLotId = 0,
            LotName = "Bãi xe Mẫu",
            LotAddress = "1 đường Mẫu, Phường Mẫu",
            LotCity = "TP. Hồ Chí Minh",
            LotTotalSlots = 120,
            LotOpeningHours = "05:00 – 23:00",
            ApplicationCode = "PA-0000",
            BusinessName = "CÔNG TY TNHH BÃI XE MẪU",
            TaxCode = "0300000000",
            BusinessAddress = "2 đường Mẫu, TP. Hồ Chí Minh",
            LegalRepresentativeName = "Trần Thị Mẫu",
            LegalRepresentativePosition = "Giám đốc",
            Email = "owner@example.com",
            Phone = "0900000000",
            BankBin = "970436",
            BankName = "Vietcombank",
            BankAccountNumber = "0011000000000",
            BankAccountName = "CONG TY TNHH BAI XE MAU",
        };
        var terms = new ContractTerms(7.5m, PartnerOnboarding.ContractSupport.TodayInVietnam(nowUtc), 12,
            "12.1. Bên B được miễn phí nền tảng trong 01 (một) tháng đầu kể từ ngày Hợp đồng có hiệu lực.\n"
            + "12.2. Bên A hỗ trợ lắp đặt 01 (một) camera nhận diện biển số tại cổng vào.");
        return LotContractSupport.Values(contract, 1, terms, platform, nowUtc);
    }

    [GeneratedRegex(@"\{\{([A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^\{\{([A-Za-z]+)\}\}$")]
    private static partial Regex StandalonePlaceholder();
}
