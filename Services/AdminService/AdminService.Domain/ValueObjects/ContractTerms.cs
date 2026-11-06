using System.Text.RegularExpressions;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.ValueObjects;

/// <summary>
/// [AdminService] Điều khoản thương mại Admin nhập khi lập / sửa hợp đồng bãi (US-116, L1 / L2). Tạo qua <see cref="Create"/> để luôn hợp lệ.
/// CommissionRate tính theo % (7.5 = 7,5%); EffectiveTo = ngày cuối của kỳ hạn (EffectiveFrom + TermMonths − 1 ngày).
/// SpecialTerms = thỏa thuận riêng của bãi (mỗi dòng một khoản), null = không có.
/// Hợp đồng theo file DOCX gốc (mẫu loại Document) chỉ có phí nền tảng – ngày hiệu lực và thời hạn nằm trong file (null).
/// </summary>
public sealed record ContractTerms(decimal CommissionRate, DateOnly? EffectiveFrom, int? TermMonths, string? SpecialTerms = null)
{
    public static readonly IReadOnlyList<int> AllowedTermMonths = [12, 24, 36];
    public const int MaxSpecialTermsLength = 4000;
    public const int MaxSpecialTermsLines = 40;

    public DateOnly? EffectiveTo => EffectiveFrom is { } from && TermMonths is { } months ? from.AddMonths(months).AddDays(-1) : null;

    /// <param name="today">Ngày hiện tại theo giờ Việt Nam – ngày hiệu lực không được trước ngày này.</param>
    public static ContractTerms Create(decimal? commissionRate, DateOnly? effectiveFrom, int? termMonths, DateOnly today,
        string? specialTerms = null)
    {
        var rate = Rate(commissionRate);
        if (effectiveFrom is not { } from) throw new ValidationException("Ngày hiệu lực không được để trống.");
        if (from < today) throw new ValidationException("Ngày hiệu lực không được trước hôm nay.");
        if (termMonths is not { } months || !AllowedTermMonths.Contains(months))
            throw new ValidationException("Thời hạn hợp đồng phải là 12, 24 hoặc 36 tháng.");
        return new ContractTerms(rate, from, months, NormalizeSpecialTerms(specialTerms));
    }

    /// <summary>Mẫu loại Document (file DOCX gốc): chỉ phí nền tảng; ngày hiệu lực, thời hạn và thỏa thuận riêng nằm trong file.</summary>
    public static ContractTerms FeeOnly(decimal? commissionRate) => new(Rate(commissionRate), null, null);

    private static decimal Rate(decimal? commissionRate)
    {
        if (commissionRate is not { } rate || rate <= 0 || rate > 100 || decimal.Round(rate, 2) != rate)
            throw new ValidationException("Tỷ lệ hoa hồng phải lớn hơn 0 và không quá 100 (%), tối đa 2 chữ số thập phân.");
        return rate;
    }

    /// <summary>Chuẩn hóa xuống dòng, bỏ khoảng trắng thừa và dòng trống ở đầu / cuối; rỗng → null.</summary>
    public static string? NormalizeSpecialTerms(string? specialTerms)
    {
        if (string.IsNullOrWhiteSpace(specialTerms)) return null;
        var lines = specialTerms.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.Trim()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        var text = string.Join('\n', lines);
        if (text.Length > MaxSpecialTermsLength)
            throw new ValidationException($"Thỏa thuận riêng tối đa {MaxSpecialTermsLength} ký tự.");
        if (lines.Count > MaxSpecialTermsLines)
            throw new ValidationException($"Thỏa thuận riêng tối đa {MaxSpecialTermsLines} dòng.");
        return text;
    }
}

/// <summary>[AdminService] Tài khoản nhận thanh toán của đối tác (nhập khi cấp tài khoản T6, in vào Phụ lục 02 của hợp đồng bãi).</summary>
public sealed partial record BankAccount(string Bin, string BankName, string AccountNumber, string AccountName)
{
    public static BankAccount Create(string? bin, string? bankName, string? accountNumber, string? accountName)
    {
        var b = bin?.Trim() ?? string.Empty;
        if (!BinPattern().IsMatch(b)) throw new ValidationException("BIN ngân hàng phải gồm 6 chữ số.");
        var number = accountNumber?.Trim() ?? string.Empty;
        if (!AccountNumberPattern().IsMatch(number)) throw new ValidationException("Số tài khoản phải gồm 6–30 chữ số.");
        return new BankAccount(b, Text(bankName, "Tên ngân hàng", 100), number, Text(accountName, "Tên chủ tài khoản", 200));
    }

    private static string Text(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} không được để trống.");
        var text = value.Trim();
        if (text.Length > maxLength) throw new ValidationException($"{field} tối đa {maxLength} ký tự.");
        return text;
    }

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex BinPattern();

    [GeneratedRegex(@"^\d{6,30}$")]
    private static partial Regex AccountNumberPattern();
}

/// <summary>[AdminService] File đã lưu qua IFileStorage: khóa lưu trữ, kiểu nội dung và SHA-256 (hex thường) để kiểm tra toàn vẹn.</summary>
public sealed record StoredFile(string Key, string ContentType, string Sha256);
