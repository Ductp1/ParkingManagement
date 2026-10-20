using System.Text.RegularExpressions;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.ValueObjects;

/// <summary>
/// [AdminService] Điều khoản Admin nhập khi gửi / sửa hợp đồng đối tác (US-116, T5 / T5b). Tạo qua <see cref="Create"/> để luôn hợp lệ.
/// CommissionRate tính theo % (8.5 = 8,5%); EffectiveTo = ngày cuối của kỳ hạn (EffectiveFrom + TermMonths − 1 ngày).
/// </summary>
public sealed partial record ContractTerms(decimal CommissionRate, DateOnly EffectiveFrom, int TermMonths, string BankBin,
    string BankName, string BankAccountNumber, string BankAccountName)
{
    public static readonly IReadOnlyList<int> AllowedTermMonths = [12, 24, 36];

    public DateOnly EffectiveTo => EffectiveFrom.AddMonths(TermMonths).AddDays(-1);

    /// <param name="today">Ngày hiện tại theo giờ Việt Nam – ngày hiệu lực không được trước ngày này.</param>
    public static ContractTerms Create(decimal? commissionRate, DateOnly? effectiveFrom, int? termMonths, string? bankBin,
        string? bankName, string? bankAccountNumber, string? bankAccountName, DateOnly today)
    {
        if (commissionRate is not { } rate || rate <= 0 || rate > 100 || decimal.Round(rate, 2) != rate)
            throw new ValidationException("Tỷ lệ hoa hồng phải lớn hơn 0 và không quá 100 (%), tối đa 2 chữ số thập phân.");
        if (effectiveFrom is not { } from) throw new ValidationException("Ngày hiệu lực không được để trống.");
        if (from < today) throw new ValidationException("Ngày hiệu lực không được trước hôm nay.");
        if (termMonths is not { } months || !AllowedTermMonths.Contains(months))
            throw new ValidationException("Thời hạn hợp đồng phải là 12, 24 hoặc 36 tháng.");

        var bin = bankBin?.Trim() ?? string.Empty;
        if (!BinPattern().IsMatch(bin)) throw new ValidationException("BIN ngân hàng phải gồm 6 chữ số.");
        var account = bankAccountNumber?.Trim() ?? string.Empty;
        if (!AccountNumberPattern().IsMatch(account)) throw new ValidationException("Số tài khoản phải gồm 6–30 chữ số.");

        return new ContractTerms(rate, from, months, bin, Text(bankName, "Tên ngân hàng", 100), account,
            Text(bankAccountName, "Tên chủ tài khoản", 200));
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
