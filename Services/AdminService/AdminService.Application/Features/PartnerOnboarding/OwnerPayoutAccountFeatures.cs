using System.Globalization;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.PartnerOnboarding;

// ===== DTO =====

/// <summary>
/// US-080: tài khoản nhận tiền quyết toán của một chủ bãi – lấy từ hồ sơ đối tác đã cấp tài khoản (AccountIssued / Activated),
/// nơi Admin nhập tài khoản nhận thanh toán mặc định ở T6 (hợp đồng từng bãi chỉ chép lại). Số tài khoản chỉ còn 4 số cuối.
/// OwnerName = tên doanh nghiệp trên hồ sơ đối tác.
/// </summary>
public sealed record OwnerPayoutAccountDto(int OwnerProfileId, string OwnerName, int PartnerApplicationId, string PartnerApplicationCode,
    string BankBin, string BankName, string AccountName, string AccountNumberMasked, string Source);

/// <summary>Kết quả tra nhiều chủ bãi: MissingOwnerProfileIds = chủ bãi chưa có hồ sơ đối tác / tài khoản nhận tiền.</summary>
public sealed record OwnerPayoutAccountListDto(IReadOnlyList<OwnerPayoutAccountDto> Items, IReadOnlyList<int> MissingOwnerProfileIds);

// ===== PORT (đọc) =====

/// <summary>Dòng đọc từ pm_admin (số tài khoản còn nguyên – chỉ Application che trước khi trả ra API).</summary>
public sealed record OwnerPayoutAccountRow(int OwnerProfileId, int PartnerApplicationId, string PartnerApplicationCode, string BusinessName,
    string BankBin, string BankName, string AccountNumber, string AccountName);

public interface IOwnerPayoutAccountQueries
{
    /// <summary>
    /// Hồ sơ đối tác AccountIssued / Activated có đủ 4 trường tài khoản nhận tiền của các chủ bãi đã cho.
    /// Một chủ bãi có thể trả nhiều dòng (hồ sơ cũ + mới) – Application chọn hồ sơ mới nhất (Id lớn nhất).
    /// </summary>
    Task<IReadOnlyList<OwnerPayoutAccountRow>> ListAsync(IReadOnlyCollection<int> ownerProfileIds, CancellationToken cancellationToken);
}

// ===== Quy tắc =====

public static class OwnerPayoutAccountRules
{
    public const string Source = "PartnerApplication";
    public const int MaxOwnersPerRequest = 100;
    private const string MaskPrefix = "****";

    /// <summary>"****" + 4 ký tự cuối (không lộ độ dài, như AuditValueMasker); ≤ 4 ký tự → che hết.</summary>
    public static string MaskAccountNumber(string accountNumber)
    {
        var value = accountNumber.Trim();
        return value.Length <= 4 ? MaskPrefix : MaskPrefix + value[^4..];
    }

    /// <summary>Chọn hồ sơ mới nhất của mỗi chủ bãi và che số tài khoản.</summary>
    public static IReadOnlyDictionary<int, OwnerPayoutAccountDto> Latest(IEnumerable<OwnerPayoutAccountRow> rows)
        => rows.GroupBy(r => r.OwnerProfileId)
            .Select(g => g.MaxBy(r => r.PartnerApplicationId)!)
            .ToDictionary(r => r.OwnerProfileId, r => new OwnerPayoutAccountDto(r.OwnerProfileId, r.BusinessName, r.PartnerApplicationId,
                r.PartnerApplicationCode, r.BankBin, r.BankName, r.AccountName, MaskAccountNumber(r.AccountNumber), Source));

    /// <summary>"1,2, 3" → [1, 2, 3] (bỏ trùng, giữ thứ tự); rỗng / không phải số nguyên dương / quá 100 chủ bãi → 400.</summary>
    public static IReadOnlyList<int> ParseOwnerIds(string? ownerProfileIds)
    {
        if (string.IsNullOrWhiteSpace(ownerProfileIds)) throw new ValidationException("ownerProfileIds không được để trống.");
        var ids = new List<int>();
        foreach (var part in ownerProfileIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new ValidationException($"ownerProfileIds chứa '{part}' không phải số nguyên dương.");
            if (!ids.Contains(id)) ids.Add(id);
        }
        if (ids.Count == 0) throw new ValidationException("ownerProfileIds không được để trống.");
        if (ids.Count > MaxOwnersPerRequest) throw new ValidationException($"Tối đa {MaxOwnersPerRequest} chủ bãi mỗi lần.");
        return ids;
    }
}

// ===== USE CASE =====

public interface IGetOwnerPayoutAccountUseCase
{
    Task<OwnerPayoutAccountDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>US-080: Admin xem tài khoản nhận tiền (đã che) của một chủ bãi trước khi chuyển khoản quyết toán. Chưa có → 404.</summary>
public sealed class GetOwnerPayoutAccountUseCase(IOwnerPayoutAccountQueries queries) : IGetOwnerPayoutAccountUseCase
{
    public async Task<OwnerPayoutAccountDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        var rows = await queries.ListAsync([ownerProfileId], cancellationToken);
        return OwnerPayoutAccountRules.Latest(rows).GetValueOrDefault(ownerProfileId)
            ?? throw new NotFoundException("Tài khoản nhận tiền của chủ bãi", ownerProfileId);
    }
}

public interface IListOwnerPayoutAccountsUseCase
{
    Task<OwnerPayoutAccountListDto> ExecuteAsync(string? ownerProfileIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080: tra tên + tài khoản nhận tiền (đã che) của nhiều chủ bãi một lần cho các trang Quyết toán / Chi trả,
/// thay vì gọi từng chủ bãi. Kết quả theo đúng thứ tự ownerProfileIds; chủ bãi không có dữ liệu nằm ở MissingOwnerProfileIds.
/// </summary>
public sealed class ListOwnerPayoutAccountsUseCase(IOwnerPayoutAccountQueries queries) : IListOwnerPayoutAccountsUseCase
{
    public async Task<OwnerPayoutAccountListDto> ExecuteAsync(string? ownerProfileIds, CancellationToken cancellationToken = default)
    {
        var ids = OwnerPayoutAccountRules.ParseOwnerIds(ownerProfileIds);
        var byOwner = OwnerPayoutAccountRules.Latest(await queries.ListAsync(ids, cancellationToken));
        return new OwnerPayoutAccountListDto(
            [.. ids.Where(byOwner.ContainsKey).Select(id => byOwner[id])],
            [.. ids.Where(id => !byOwner.ContainsKey(id))]);
    }
}
