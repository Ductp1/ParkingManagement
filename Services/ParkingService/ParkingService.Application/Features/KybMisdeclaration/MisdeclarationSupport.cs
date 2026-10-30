using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybMisdeclaration;

/// <summary>Bước dùng chung của các use case xử lý khai báo sai: kiểm tra tham số, nạp bãi / case, trả màn xử lý sau khi lưu.</summary>
internal static class MisdeclarationSupport
{
    public static void EnsureIds(int lotId, int adminUserId, int? caseId = null)
    {
        if (lotId <= 0) throw new ValidationException("lotId phải là số nguyên dương.");
        if (caseId is <= 0) throw new ValidationException("caseId phải là số nguyên dương.");
        if (adminUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
    }

    public static async Task<LotMisdeclarationCase> LoadCaseAsync(ILotMisdeclarationRepository repository, int lotId, int caseId,
        CancellationToken cancellationToken)
        => await repository.GetCaseAsync(lotId, caseId, cancellationToken)
           ?? throw new NotFoundException("Case khai báo sai", caseId);

    public static async Task<LotMisdeclarationDto> ReloadAsync(ILotMisdeclarationQueries queries, KybMisdeclarationSettings settings,
        int lotId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var lot = await queries.GetLotAsync(lotId, cancellationToken) ?? throw new NotFoundException("Bãi đỗ", lotId);
        var cases = await queries.ListCasesAsync(lotId, cancellationToken);
        return MisdeclarationMapper.ToDto(lot, cases, settings, nowUtc);
    }

    /// <summary>Parse enum từ chuỗi (không phân biệt hoa thường, không nhận số) → 400 kèm danh sách giá trị hợp lệ.</summary>
    public static TEnum ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} không được để trống.");
        var text = value.Trim();
        if (!int.TryParse(text, out _) && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        throw new ValidationException($"{field} không hợp lệ (chấp nhận: {string.Join(", ", Enum.GetNames<TEnum>())}).");
    }
}
