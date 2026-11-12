using System.Text.Json;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.RateCards;

/// <summary>
/// DTO chứa cấu hình của 1 block giá bậc thang theo loại xe (US-070).
/// </summary>
public sealed record CreateRateRuleRequest(
    VehicleType VehicleType,
    int FromMinute,
    int? ToMinute,
    int BlockMinutes,
    decimal PricePerBlock,
    int SortOrder);

/// <summary>
/// DTO gửi lên để tạo mới bảng giá kèm toàn bộ mức phụ thu và block giá (US-070, US-071, US-072).
/// </summary>
public sealed record CreateRateCardCommand(
    int ParkingLotId,
    int OwnerProfileId,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    decimal OvernightSurcharge,
    decimal WeekendMultiplier,
    decimal HolidayMultiplier,
    decimal OversizedMultiplier,
    decimal OverstayMultiplier,
    decimal? MaxDailyAmount,
    IReadOnlyList<CreateRateRuleRequest> Rules);

/// <summary>
/// DTO trả về kết quả chi tiết của bảng giá kèm cờ cảnh báo duyệt tăng giá (US-072).
/// </summary>
public sealed record RateCardDetailDto(
    int Id,
    int ParkingLotId,
    int OwnerProfileId,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    decimal OvernightSurcharge,
    decimal WeekendMultiplier,
    decimal HolidayMultiplier,
    decimal OversizedMultiplier,
    decimal OverstayMultiplier,
    decimal? MaxDailyAmount,
    IReadOnlyList<RateRuleDto> Rules,
    bool RequiresAdminApproval = false,
    decimal MaxPriceIncreasePercentage = 0m,
    string? WarningMessage = null);

/// <summary>
/// DTO mô tả một dòng quy tắc giá block giờ.
/// </summary>
public sealed record RateRuleDto(
    int Id,
    string VehicleType,
    int FromMinute,
    int? ToMinute,
    int BlockMinutes,
    decimal PricePerBlock,
    int SortOrder);

/// <summary>
/// Cổng ca sử dụng tạo mới bảng giá.
/// </summary>
public interface ICreateRateCardUseCase
{
    Task<RateCardDetailDto> ExecuteAsync(CreateRateCardCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng tạo mới bảng giá độc lập có kiểm soát thay đổi giá (US-070, US-071, US-072).
/// </summary>
public sealed class CreateRateCardUseCase : ICreateRateCardUseCase
{
    private readonly IRateCardManagementRepository repo;
    private readonly IRateCardAuditLogRepository? auditRepo;
    private readonly TimeProvider timeProvider;

    public CreateRateCardUseCase(
        IRateCardManagementRepository repo,
        IRateCardAuditLogRepository? auditRepo = null,
        TimeProvider? timeProvider = null)
    {
        this.repo = repo;
        this.auditRepo = auditRepo;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RateCardDetailDto> ExecuteAsync(CreateRateCardCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản của bãi xe và chủ bãi
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId phải là số nguyên dương.");

        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId phải là số nguyên dương.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên bảng giá không được để trống.");

        // US-072 AC1: Kiểm soát ngày hiệu lực (Effective Date) phải sau thời điểm hiện tại ít nhất 12 giờ
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (command.EffectiveFromUtc < now)
            throw new ValidationException("Ngày bắt đầu hiệu lực (EffectiveFromUtc) không thể ở trong quá khứ.");

        var minEffectiveTime = now.AddHours(12);
        if (command.EffectiveFromUtc < minEffectiveTime)
            throw new ValidationException($"Ngày bắt đầu hiệu lực phải sau thời điểm hiện tại ít nhất 12 giờ (tối thiểu từ {minEffectiveTime:dd/MM/yyyy HH:mm} UTC) để thông báo cho khách hàng.");

        if (command.EffectiveToUtc.HasValue && command.EffectiveToUtc.Value <= command.EffectiveFromUtc)
            throw new ValidationException("Ngày kết thúc hiệu lực (EffectiveToUtc) phải lớn hơn ngày bắt đầu hiệu lực (EffectiveFromUtc).");

        // 2. Kiểm tra các hệ số phụ thu (US-071)
        if (command.OvernightSurcharge < 0)
            throw new ValidationException("Phụ thu qua đêm không được là số âm.");

        if (command.WeekendMultiplier < 1.0m || command.WeekendMultiplier > 3.0m)
            throw new ValidationException("Hệ số cuối tuần phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.HolidayMultiplier < 1.0m || command.HolidayMultiplier > 3.0m)
            throw new ValidationException("Hệ số ngày lễ phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.OverstayMultiplier < 1.0m || command.OverstayMultiplier > 3.0m)
            throw new ValidationException("Hệ số phạt quá giờ phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.OversizedMultiplier < 1.0m || command.OversizedMultiplier > 3.0m)
            throw new ValidationException("Hệ số xe quá khổ phải nằm trong khoảng từ 1.0 đến 3.0.");

        // 3. Kiểm tra danh sách quy tắc block giờ (US-070)
        if (command.Rules == null || command.Rules.Count == 0)
            throw new ValidationException("Bảng giá phải có ít nhất 1 quy tắc giá theo block giờ.");

        foreach (var rule in command.Rules)
        {
            if (rule.BlockMinutes <= 0)
                throw new ValidationException("Số phút của một block (BlockMinutes) phải lớn hơn 0.");

            if (rule.PricePerBlock < 0)
                throw new ValidationException("Đơn giá mỗi block (PricePerBlock) không được là số âm.");

            if (rule.FromMinute < 0)
                throw new ValidationException("Số phút bắt đầu (FromMinute) không được là số âm.");

            if (rule.ToMinute.HasValue && rule.ToMinute.Value <= rule.FromMinute)
                throw new ValidationException($"Khoảng thời gian không hợp lệ: ToMinute ({rule.ToMinute.Value}) phải lớn hơn FromMinute ({rule.FromMinute}).");
        }

        // 4. Lấy bảng giá đang chạy hiện tại của bãi để so sánh tỷ lệ tăng giá (US-072 AC2)
        var currentActive = await repo.GetCurrentActiveAsync(command.ParkingLotId, now, cancellationToken);
        decimal maxIncreasePercentage = 0m;

        if (currentActive != null)
        {
            // So sánh đơn giá block đầu tiên của từng loại xe
            foreach (var newRule in command.Rules)
            {
                var oldRule = currentActive.Rules
                    .FirstOrDefault(r => r.VehicleType == newRule.VehicleType && r.FromMinute == newRule.FromMinute);

                if (oldRule != null && oldRule.PricePerBlock > 0)
                {
                    var blockIncrease = ((newRule.PricePerBlock - oldRule.PricePerBlock) / oldRule.PricePerBlock) * 100m;
                    if (blockIncrease > maxIncreasePercentage)
                    {
                        maxIncreasePercentage = blockIncrease;
                    }
                }
            }

            // So sánh phụ thu qua đêm
            if (currentActive.OvernightSurcharge > 0 && command.OvernightSurcharge > currentActive.OvernightSurcharge)
            {
                var overnightIncrease = ((command.OvernightSurcharge - currentActive.OvernightSurcharge) / currentActive.OvernightSurcharge) * 100m;
                if (overnightIncrease > maxIncreasePercentage)
                {
                    maxIncreasePercentage = overnightIncrease;
                }
            }

            // So sánh hệ số phạt quá giờ
            if (currentActive.OverstayMultiplier > 0 && command.OverstayMultiplier > currentActive.OverstayMultiplier)
            {
                var overstayIncrease = ((command.OverstayMultiplier - currentActive.OverstayMultiplier) / currentActive.OverstayMultiplier) * 100m;
                if (overstayIncrease > maxIncreasePercentage)
                {
                    maxIncreasePercentage = overstayIncrease;
                }
            }
        }

        // US-072 AC2: Nếu tăng > 30% thì đánh dấu cần Admin phê duyệt và tạm thời chưa kích hoạt
        var requiresApproval = maxIncreasePercentage > 30m;
        var isActive = !requiresApproval;

        // 5. Nếu bảng giá mới hợp lệ và tự động kích hoạt, đóng thời hạn bảng giá cũ tại mốc bắt đầu giá mới
        if (isActive && currentActive != null)
        {
            currentActive.EffectiveToUtc = command.EffectiveFromUtc;
            await repo.UpdateAsync(currentActive, cancellationToken);
        }

        // 6. Khởi tạo thực thể RateCard mới
        var newCard = new RateCard
        {
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            Name = command.Name.Trim(),
            EffectiveFromUtc = command.EffectiveFromUtc,
            EffectiveToUtc = command.EffectiveToUtc,
            IsActive = isActive,
            OvernightSurcharge = command.OvernightSurcharge,
            WeekendMultiplier = command.WeekendMultiplier,
            HolidayMultiplier = command.HolidayMultiplier,
            OversizedMultiplier = command.OversizedMultiplier,
            OverstayMultiplier = command.OverstayMultiplier,
            MaxDailyAmount = command.MaxDailyAmount
        };

        foreach (var r in command.Rules)
        {
            newCard.Rules.Add(new RateRule
            {
                VehicleType = r.VehicleType,
                FromMinute = r.FromMinute,
                ToMinute = r.ToMinute,
                BlockMinutes = r.BlockMinutes,
                PricePerBlock = r.PricePerBlock,
                SortOrder = r.SortOrder
            });
        }

        // Lưu bảng giá xuống cơ sở dữ liệu
        await repo.AddAsync(newCard, cancellationToken);

        // 7. US-072 AC3: Ghi vết Audit Log (ai, lúc nào, giá cũ/mới, phần trăm tăng)
        var auditLog = new RateCardAuditLog
        {
            RateCardId = newCard.Id,
            ParkingLotId = command.ParkingLotId,
            ChangedByUserId = command.OwnerProfileId,
            Action = requiresApproval ? "PriceIncreaseWarning" : "CreateRateCard",
            OldValueJson = currentActive != null ? JsonSerializer.Serialize(MapToDto(currentActive)) : null,
            NewValueJson = JsonSerializer.Serialize(MapToDto(newCard)),
            MaxPriceIncreasePercentage = Math.Round(maxIncreasePercentage, 2),
            RequiresAdminApproval = requiresApproval
        };
        if (auditRepo != null)
        {
            await auditRepo.AddAsync(auditLog, cancellationToken);
        }

        string? warningMsg = requiresApproval
            ? $"Mức giá mới tăng {maxIncreasePercentage:N1}% (> 30% so với giá hiện tại). Bảng giá đã được gửi yêu cầu phê duyệt tới Admin/Partner Ops trước khi có hiệu lực."
            : null;

        return MapToDto(newCard, requiresApproval, Math.Round(maxIncreasePercentage, 2), warningMsg);
    }

    /// <summary>
    /// Phương thức tiện ích ánh xạ từ Entity sang DTO kèm thông tin cảnh báo kiểm soát giá.
    /// </summary>
    public static RateCardDetailDto MapToDto(
        RateCard card, 
        bool requiresApproval = false, 
        decimal increasePercentage = 0m, 
        string? warningMessage = null)
    {
        return new RateCardDetailDto(
            card.Id,
            card.ParkingLotId,
            card.OwnerProfileId,
            card.Name,
            card.EffectiveFromUtc,
            card.EffectiveToUtc,
            card.IsActive,
            card.OvernightSurcharge,
            card.WeekendMultiplier,
            card.HolidayMultiplier,
            card.OversizedMultiplier,
            card.OverstayMultiplier,
            card.MaxDailyAmount,
            card.Rules.Select(r => new RateRuleDto(
                r.Id,
                r.VehicleType.ToString(),
                r.FromMinute,
                r.ToMinute,
                r.BlockMinutes,
                r.PricePerBlock,
                r.SortOrder
            )).ToList(),
            requiresApproval,
            increasePercentage,
            warningMessage
        );
    }
}
