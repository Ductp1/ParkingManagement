using System.Globalization;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Commission;

/// <summary>Nguồn của tỷ lệ hoa hồng áp dụng cho một giao dịch (US-079).</summary>
public static class CommissionRateSources
{
    /// <summary>Phí nền tảng của hợp đồng bãi đã ký, đang trong thời gian hiệu lực.</summary>
    public const string LotContract = "LotContract";
    /// <summary>Tham số hệ thống DEFAULT_COMMISSION_RATE (giá trị hiệu lực tại thời điểm giao dịch).</summary>
    public const string DefaultConfig = "DefaultConfig";

    public static readonly IReadOnlyList<string> All = [LotContract, DefaultConfig];
}

/// <summary>Lý do dùng tỷ lệ mặc định thay vì hợp đồng (null khi dùng hợp đồng).</summary>
public static class CommissionRateReasons
{
    /// <summary>Hợp đồng đã ký nhưng ngày giao dịch trước ngày hiệu lực.</summary>
    public const string BeforeEffective = "BeforeEffective";
    /// <summary>Hợp đồng đã ký nhưng ngày giao dịch sau ngày hết hạn (chưa có luồng gia hạn).</summary>
    public const string AfterExpiry = "AfterExpiry";
    /// <summary>Bãi chưa có hợp đồng đã ký (bãi seed, hợp đồng còn chờ ký hoặc đã hủy).</summary>
    public const string NoSignedContract = "NoSignedContract";
}

/// <summary>
/// Phiên bản đã ký của một hợp đồng bãi, đọc từ pm_admin. CommissionRatePercent theo % (7.5 = 7,5%) như LotContractVersion;
/// EffectiveFrom / EffectiveTo null với hợp đồng theo file DOCX gốc.
/// </summary>
public sealed record SignedLotContractRate(int ContractId, string ContractNumber, int ParkingLotId, int OwnerProfileId, string BusinessName,
    string LotName, string? LotDistrict, int Version, decimal CommissionRatePercent, DateOnly? EffectiveFrom, DateOnly? EffectiveTo,
    DateTime SignedAtUtc);

/// <summary>Kết quả giải tỷ lệ. Rate là PHÂN SỐ 0–1. Contract = hợp đồng đã ký của bãi (kể cả khi ngoài hiệu lực) để hiển thị / liên kết.</summary>
public sealed record ResolvedCommissionRate(decimal Rate, string Source, string? Reason, SignedLotContractRate? Contract,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool OverlappingContracts);

/// <summary>
/// US-079: chọn tỷ lệ hoa hồng cho một bãi tại một thời điểm. Hàm thuần – không đọc database, không đọc đồng hồ.
/// <list type="number">
/// <item>Ngày xét = ngày theo giờ Việt Nam của <c>atUtc</c>.</item>
/// <item>Hợp đồng đã ký có EffectiveFrom ≤ ngày ≤ EffectiveTo (cả hai đầu) → tỷ lệ hợp đồng / 100.
///   Hợp đồng DOCX gốc (ngày null) hiệu lực từ ngày ký (giờ Việt Nam), không có ngày kết thúc.</item>
/// <item>Nhiều hợp đồng cùng hiệu lực (database hiện không cho phép – phòng xa) → hợp đồng ký sau cùng, cờ OverlappingContracts.</item>
/// <item>Không có → tỷ lệ mặc định đã giải tại atUtc, kèm lý do (BeforeEffective / AfterExpiry / NoSignedContract).</item>
/// </list>
/// </summary>
public static class CommissionRateResolver
{
    public const string DefaultRateKey = "DEFAULT_COMMISSION_RATE";

    /// <summary>Việt Nam dùng UTC+7 quanh năm.</summary>
    public static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    public static DateOnly IctDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + IctOffset);

    /// <summary>Ngày bắt đầu hiệu lực thực tế: EffectiveFrom, hoặc ngày ký (giờ Việt Nam) với hợp đồng DOCX gốc.</summary>
    public static DateOnly EffectiveFromOf(SignedLotContractRate contract) => contract.EffectiveFrom ?? IctDate(contract.SignedAtUtc);

    public static ResolvedCommissionRate Resolve(IEnumerable<SignedLotContractRate> signedContractsOfLot, decimal defaultRate, DateTime atUtc)
    {
        var day = IctDate(atUtc);
        var contracts = signedContractsOfLot
            .OrderByDescending(c => c.SignedAtUtc).ThenByDescending(c => c.ContractId)
            .ToList();

        var active = contracts.Where(c => EffectiveFromOf(c) <= day && (c.EffectiveTo is not { } to || day <= to)).ToList();
        if (active.Count > 0)
        {
            var contract = active[0];
            return new ResolvedCommissionRate(contract.CommissionRatePercent / 100m, CommissionRateSources.LotContract, null, contract,
                EffectiveFromOf(contract), contract.EffectiveTo, active.Count > 1);
        }

        if (contracts.Count == 0)
            return new ResolvedCommissionRate(defaultRate, CommissionRateSources.DefaultConfig, CommissionRateReasons.NoSignedContract, null,
                null, null, false);

        var latest = contracts[0];
        var reason = day < EffectiveFromOf(latest) ? CommissionRateReasons.BeforeEffective : CommissionRateReasons.AfterExpiry;
        return new ResolvedCommissionRate(defaultRate, CommissionRateSources.DefaultConfig, reason, latest, EffectiveFromOf(latest),
            latest.EffectiveTo, false);
    }

    /// <summary>
    /// Giá trị DEFAULT_COMMISSION_RATE (phân số 0–1, SystemConfigRules). Sai định dạng / ngoài khoảng → 503: không đoán tỷ lệ,
    /// phía gọi (job của PaymentService) để giao dịch ở trạng thái chờ tính và thử lại sau.
    /// </summary>
    public static decimal ParseDefaultRate(string? value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) || rate < 0 || rate > 1)
            throw new DependencyUnavailableException($"Tham số {DefaultRateKey} không hợp lệ ('{value}'), chưa thể tính hoa hồng.");
        return rate;
    }
}
