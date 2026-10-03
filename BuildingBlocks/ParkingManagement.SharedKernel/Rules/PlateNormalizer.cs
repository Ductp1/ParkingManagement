using System.Text;
using System.Text.RegularExpressions;

namespace ParkingManagement.SharedKernel.Rules;

/// <summary>
/// Chuẩn hoá, sửa lỗi OCR và định dạng biển số xe Việt Nam (Thông tư 01/2021/TT-BCA).
/// Dùng chung cho VehicleService (kiểm tra khi thêm xe) và GateService (chuẩn hoá kết quả OCR).
/// Dạng lưu trong DB: chỉ chữ + số, ví dụ "51F12345", "59X112345", "30LD12345".
/// </summary>
public static partial class PlateNormalizer
{
    // 2 số mã tỉnh + seri (1 chữ, có thể thêm 1 chữ/số) + 4 hoặc 5 số thứ tự.
    [GeneratedRegex(@"^\d{2}[A-Z][A-Z0-9]?\d{4,5}$")]
    private static partial Regex VietnamPlateRegex();

    private static readonly Dictionary<char, char> ToDigit = new()
    {
        ['O'] = '0', ['Q'] = '0', ['D'] = '0', ['U'] = '0',
        ['I'] = '1', ['L'] = '1', ['J'] = '1', ['T'] = '1',
        ['Z'] = '2', ['S'] = '5', ['B'] = '8', ['G'] = '6', ['A'] = '4',
    };

    private static readonly Dictionary<char, char> ToLetter = new()
    {
        ['0'] = 'D', ['1'] = 'T', ['2'] = 'Z', ['4'] = 'A',
        ['5'] = 'S', ['6'] = 'G', ['8'] = 'B',
    };

    /// <summary>Bỏ dấu, khoảng trắng, gạch ngang, chấm... và viết hoa.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        foreach (var c in input.ToUpperInvariant())
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') sb.Append(c);
        return sb.ToString();
    }

    public static bool IsValid(string? plate) => VietnamPlateRegex().IsMatch(Normalize(plate));

    /// <summary>
    /// Sửa các lỗi nhầm lẫn thường gặp của OCR dựa vào vị trí ký tự
    /// (vd "S1F-l23.45" → "51F12345"). Trả về null nếu không thể thành biển hợp lệ.
    /// </summary>
    public static string? TryCorrect(string? input)
    {
        var s = Normalize(input);
        if (s.Length is < 7 or > 9) return null;

        var chars = s.ToCharArray();
        // 2 ký tự đầu: mã tỉnh (số)
        for (var i = 0; i < 2; i++) chars[i] = AsDigit(chars[i]);
        // Ký tự thứ 3: chữ cái seri
        chars[2] = AsLetter(chars[2]);

        // Phần còn lại: [ký tự seri thứ 2] + 4-5 số.
        var rest = chars[3..];
        // Ký tự thứ 4 là chữ → seri 2 ký tự, trừ khi nó là chữ hay bị OCR nhầm với số (I, L, S, O...)
        // và phần còn lại vừa đủ 5 số (vd "51F-I23.45" → "51F12345").
        var hasSecondSeries = rest.Length == 6 ||
            (char.IsLetter(rest[0]) && !(rest.Length == 5 && ToDigit.ContainsKey(rest[0])));
        var start = hasSecondSeries ? 1 : 0;
        for (var i = start; i < rest.Length; i++) rest[i] = AsDigit(rest[i]);

        var result = new string(chars[..3]) + new string(rest);
        return IsValid(result) ? result : null;
    }

    /// <summary>"51F12345" → "51F-123.45"; "30A1234" → "30A-1234"; "59X112345" → "59X1-123.45".</summary>
    public static string Format(string? plate)
    {
        var s = Normalize(plate);
        if (!IsValid(s)) return s;

        var digitsCount = s.Length - 3 >= 6 || char.IsLetter(s[3]) ? s.Length - 4 : s.Length - 3;
        var prefix = s[..^digitsCount];
        var number = s[^digitsCount..];
        if (number.Length == 5) number = $"{number[..3]}.{number[3..]}";
        return $"{prefix}-{number}";
    }

    private static char AsDigit(char c) => char.IsDigit(c) ? c : ToDigit.GetValueOrDefault(c, c);
    private static char AsLetter(char c) => char.IsLetter(c) ? c : ToLetter.GetValueOrDefault(c, c);
}
