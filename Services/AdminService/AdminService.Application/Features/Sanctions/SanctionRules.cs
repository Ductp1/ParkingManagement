using System.Globalization;
using System.Text.Json;
using AdminService.Application.Features.Dashboard;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

/// <summary>
/// US-097: quy tắc 4 cấp theo Điều 7 hợp đồng mẫu (AC tạm – chờ PO xác nhận). Dùng chung cho use case và test.
/// </summary>
public static class SanctionRules
{
    public const int MaxLotsPerRequest = 50;
    public const int EvidenceMaxLength = 20_000;
    public const int SearchMaxLength = 100;
    /// <summary>numeric(18,2): tối đa 16 chữ số phần nguyên.</summary>
    public const decimal MaxPenaltyAmount = 9_999_999_999_999_999.99m;

    public static string LevelLabel(SanctionLevel level) => level switch
    {
        SanctionLevel.Warning => "Cảnh cáo",
        SanctionLevel.RankingPenalty => "Hạ thứ hạng hiển thị",
        SanctionLevel.TemporarySuspension => "Tạm ngừng",
        SanctionLevel.PermanentBan => "Chấm dứt hợp tác",
        _ => level.ToString(),
    };

    /// <summary>
    /// Điều 7.3: hạn khiếu nại = hết ngày làm việc thứ N (23:59:59 giờ Việt Nam) tính từ ngày sau ngày áp; bỏ Thứ 7, Chủ nhật.
    /// Ngày lễ chưa tính (chưa có lịch nghỉ dùng chung – vấn đề mở).
    /// </summary>
    public static DateTime AppealDeadlineUtc(DateTime issuedAtUtc, int businessDays)
    {
        if (businessDays < 1) throw new ArgumentOutOfRangeException(nameof(businessDays));
        var day = (DateTime.SpecifyKind(issuedAtUtc, DateTimeKind.Utc) + IctDay.Offset).Date;
        var counted = 0;
        while (counted < businessDays)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) counted++;
        }
        return DateTime.SpecifyKind(day.AddDays(1) - IctDay.Offset - TimeSpan.FromSeconds(1), DateTimeKind.Utc);
    }

    /// <summary>Lý do bắt buộc, trim, tối đa 1000 ký tự.</summary>
    public static string RequireReason(string? reason, string what)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException($"Lý do {what} không được để trống.");
        var trimmed = reason.Trim();
        if (trimmed.Length > Sanction.ReasonMaxLength) throw new ValidationException($"Lý do {what} tối đa 1000 ký tự.");
        return trimmed;
    }

    /// <summary>EvidenceJson tùy chọn: phải là JSON hợp lệ, tối đa 20.000 ký tự. Rỗng → null.</summary>
    public static string? NormalizeEvidence(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson)) return null;
        var trimmed = evidenceJson.Trim();
        if (trimmed.Length > EvidenceMaxLength) throw new ValidationException("evidenceJson tối đa 20.000 ký tự.");
        try
        {
            using var _ = JsonDocument.Parse(trimmed);
        }
        catch (JsonException)
        {
            throw new ValidationException("evidenceJson phải là JSON hợp lệ.");
        }
        return trimmed;
    }

    /// <summary>Tiền phạt tùy chọn: ≥ 0, tối đa 2 chữ số thập phân (VND), trong giới hạn numeric(18,2).</summary>
    public static decimal? NormalizePenalty(decimal? penaltyAmount)
    {
        if (penaltyAmount is not { } amount) return null;
        if (amount < 0) throw new ValidationException("penaltyAmount phải ≥ 0.");
        if (amount > MaxPenaltyAmount) throw new ValidationException("penaltyAmount vượt giới hạn cho phép.");
        if (decimal.Round(amount, 2) != amount) throw new ValidationException("penaltyAmount tối đa 2 chữ số thập phân.");
        return amount;
    }

    /// <summary>Danh sách bãi: bỏ trùng, mỗi Id &gt; 0, tối đa 50 bãi.</summary>
    public static IReadOnlyList<int> NormalizeLotIds(IReadOnlyList<int>? parkingLotIds)
    {
        if (parkingLotIds is null || parkingLotIds.Count == 0) return [];
        if (parkingLotIds.Any(id => id <= 0)) throw new ValidationException("parkingLotIds chỉ chứa số nguyên dương.");
        var distinct = parkingLotIds.Distinct().ToList();
        if (distinct.Count > MaxLotsPerRequest) throw new ValidationException($"Tối đa {MaxLotsPerRequest} bãi cho mỗi lần áp chế tài.");
        return distinct;
    }

    /// <summary>
    /// Thời hạn theo cấp (Điều 7.1): cảnh cáo – bắt buộc hạn khắc phục, không có EndsAt; hạ hạng – EndsAt bắt buộc, ≤ 30 ngày;
    /// tạm ngừng – EndsAt bắt buộc; chấm dứt hợp tác – không có EndsAt, chỉ áp lên toàn chủ bãi. Mốc phải sau thời điểm áp.
    /// </summary>
    public static void ValidatePeriod(SanctionLevel level, bool hasLots, DateTime nowUtc, DateTime? endsAtUtc, DateTime? correctionDueAtUtc)
    {
        if (level != SanctionLevel.Warning && correctionDueAtUtc is not null)
            throw new ValidationException("correctionDueAtUtc chỉ dùng cho cấp Cảnh cáo.");

        switch (level)
        {
            case SanctionLevel.Warning:
                if (endsAtUtc is not null)
                    throw new ValidationException("Cảnh cáo không có endsAtUtc – dùng correctionDueAtUtc (hạn khắc phục).");
                if (correctionDueAtUtc is null) throw new ValidationException("Cảnh cáo bắt buộc có hạn khắc phục (correctionDueAtUtc).");
                if (correctionDueAtUtc <= nowUtc) throw new ValidationException("Hạn khắc phục phải sau thời điểm hiện tại.");
                break;
            case SanctionLevel.RankingPenalty:
                if (endsAtUtc is null) throw new ValidationException("Hạ thứ hạng bắt buộc có thời điểm kết thúc (endsAtUtc).");
                if (endsAtUtc <= nowUtc) throw new ValidationException("endsAtUtc phải sau thời điểm hiện tại.");
                if (endsAtUtc > nowUtc.AddDays(Sanction.RankingPenaltyMaxDays))
                    throw new ValidationException($"Hạ thứ hạng tối đa {Sanction.RankingPenaltyMaxDays} ngày kể từ lúc áp.");
                break;
            case SanctionLevel.TemporarySuspension:
                if (endsAtUtc is null) throw new ValidationException("Tạm ngừng bắt buộc có thời điểm kết thúc (endsAtUtc).");
                if (endsAtUtc <= nowUtc) throw new ValidationException("endsAtUtc phải sau thời điểm hiện tại.");
                break;
            case SanctionLevel.PermanentBan:
                if (hasLots) throw new ValidationException("Chấm dứt hợp tác chỉ áp lên toàn bộ chủ bãi (không chọn bãi).");
                if (endsAtUtc is not null) throw new ValidationException("Chấm dứt hợp tác là vĩnh viễn – không có endsAtUtc.");
                break;
            default:
                throw new ValidationException("Cấp chế tài không hợp lệ.");
        }
    }

    /// <summary>Kiểm tra tham số lọc danh sách.</summary>
    public static SanctionFilter ToFilter(SanctionListQuery query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is { Length: > SearchMaxLength }) throw new ValidationException("Từ khóa tìm kiếm tối đa 100 ký tự.");
        if (query.OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (query.ParkingLotId is <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        var from = UtcDateTime.ToUtc(query.From);
        var to = UtcDateTime.ToUtc(query.To);
        if (from is not null && to is not null && from >= to) throw new ValidationException("from phải trước to.");
        return new SanctionFilter(query.Status, query.Level, query.OwnerProfileId, query.ParkingLotId, search, from, to,
            query.SyncFailed == true);
    }

    /// <summary>Danh sách Id dạng "3, 7, 12" cho thông điệp lỗi.</summary>
    public static string JoinIds(IEnumerable<int> ids) => string.Join(", ", ids.Order().Select(i => i.ToString(CultureInfo.InvariantCulture)));
}
