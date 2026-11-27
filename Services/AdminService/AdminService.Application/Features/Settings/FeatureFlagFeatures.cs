using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

// ===== DTO =====
/// <summary>Bật/tắt một feature flag (US-099). Admin thao tác không nằm trong body: controller lấy từ JWT.</summary>
public sealed record UpdateFeatureFlagRequest(bool? IsEnabled, string? Reason);

/// <summary>Trạng thái flag cho service khác / Frontend đọc (Remote Configuration): chỉ khóa + bật/tắt, không có người sửa hay mô tả.</summary>
public sealed record FeatureFlagStateDto(string Key, bool IsEnabled);

// ===== PORT (ghi) =====
public interface IFeatureFlagRepository
{
    Task<FeatureFlagDto?> FindByKeyAsync(string key, CancellationToken cancellationToken);
    /// <summary>
    /// Đổi flag sang <paramref name="isEnabled"/> + ghi audit log trong CÙNG 1 transaction. Chỉ đổi khi flag đang ở giá trị khác,
    /// nên 2 Admin bấm cùng lúc thì người đến sau nhận false và không sinh dòng audit thứ hai.
    /// </summary>
    Task<bool> TryChangeAsync(string key, bool isEnabled, int performedByUserId, DateTime nowUtc, AuditLog auditLog, CancellationToken cancellationToken);
}

// ===== USE CASE =====
public interface IUpdateFeatureFlagUseCase
{
    Task<FeatureFlagDto> ExecuteAsync(string key, UpdateFeatureFlagRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-099 AC3: Admin bật/tắt feature flag, bắt buộc có lý do, hiệu lực ngay và ghi audit log.</summary>
public sealed class UpdateFeatureFlagUseCase(IFeatureFlagRepository repository, TimeProvider timeProvider) : IUpdateFeatureFlagUseCase
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public async Task<FeatureFlagDto> ExecuteAsync(string key, UpdateFeatureFlagRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (request.IsEnabled is not { } isEnabled) throw new ValidationException("isEnabled không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do thay đổi không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > 1000) throw new ValidationException("Lý do thay đổi tối đa 1000 ký tự.");

        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        var flag = await repository.FindByKeyAsync(normalizedKey, cancellationToken)
            ?? throw new NotFoundException("Feature flag", normalizedKey);
        if (flag.IsEnabled == isEnabled)
            throw new ConflictException($"Feature flag {flag.Key} đang ở trạng thái {(isEnabled ? "bật" : "tắt")}.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var changed = await repository.TryChangeAsync(flag.Key, isEnabled, performedByUserId, now, BuildAuditLog(flag.Key, isEnabled, reason, performedByUserId), cancellationToken);
        if (!changed) throw new ConflictException($"Feature flag {flag.Key} vừa được đổi từ một yêu cầu khác. Vui lòng tải lại.");

        return flag with { IsEnabled = isEnabled, UpdatedByUserId = performedByUserId, UpdatedAtUtc = now };
    }

    private static AuditLog BuildAuditLog(string key, bool isEnabled, string reason, int performedByUserId) => new()
    {
        SourceService = "AdminService",
        UserId = performedByUserId,
        Action = "FeatureFlag.Updated",
        EntityName = "FeatureFlag",
        EntityId = key,
        OldValuesJson = JsonSerializer.Serialize(new { isEnabled = !isEnabled }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(new { isEnabled }, AuditJson),
        Reason = reason,
    };
}

public interface IGetFeatureFlagStatesUseCase
{
    Task<IReadOnlyList<FeatureFlagStateDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-099 AC2: nguồn Remote Configuration – service khác và Frontend đọc flag ở đây thay vì hard-code.</summary>
public sealed class GetFeatureFlagStatesUseCase(ISettingsQueries queries) : IGetFeatureFlagStatesUseCase
{
    public async Task<IReadOnlyList<FeatureFlagStateDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        => [.. (await queries.ListFeatureFlagsAsync(cancellationToken)).Select(f => new FeatureFlagStateDto(f.Key, f.IsEnabled))];
}
