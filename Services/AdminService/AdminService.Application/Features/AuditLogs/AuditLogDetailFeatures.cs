using System.Text.Json;
using System.Text.Json.Nodes;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.AuditLogs;

// ===== DTO =====
/// <summary>Một dòng AuditLogs đầy đủ, đúng như lưu trong pm_admin (JSON chưa che) – chỉ dùng giữa port đọc và use case.</summary>
public sealed record AuditLogRecord(int Id, DateTime CreatedAtUtc, string SourceService, int? UserId, int? OwnerProfileId, string Action,
    string EntityName, string? EntityId, string? OldValuesJson, string? NewValuesJson, string? Reason, string? IpAddress);

/// <summary>Một trường thay đổi. Field = đường dẫn (VD "status", "bank.accountNumber"; "$" khi JSON gốc không phải object).
/// Kind = Added | Removed | Modified. Giá trị đã che.</summary>
public sealed record AuditFieldChangeDto(string Field, string Kind, JsonElement? OldValue, JsonElement? NewValue);

/// <summary>US-100: chi tiết một audit log – giá trị cũ/mới (đã che dữ liệu nhạy cảm) + danh sách trường thay đổi.</summary>
public sealed record AuditLogDetailDto(int Id, DateTime CreatedAtUtc, string SourceService, int? UserId, int? OwnerProfileId,
    string Action, string EntityName, string? EntityId, string? Reason, string? IpAddress, JsonElement? OldValues,
    JsonElement? NewValues, IReadOnlyList<AuditFieldChangeDto> Changes);

/// <summary>
/// US-100: so giá trị cũ/mới theo từng trường lá (object lồng nhau → đường dẫn có dấu chấm; mảng so nguyên khối).
/// So trên giá trị gốc rồi mới che, nên đổi một số tài khoản vẫn hiện là thay đổi dù hai giá trị đã che trông giống nhau.
/// Hàm thuần – không đọc database.
/// </summary>
public static class AuditChangeDiff
{
    private const string RootPath = "$";

    public static IReadOnlyList<AuditFieldChangeDto> Compare(JsonNode? oldValues, JsonNode? newValues)
    {
        var before = Flatten(oldValues);
        var after = Flatten(newValues);
        var changes = new List<AuditFieldChangeDto>();
        foreach (var field in before.Keys.Concat(after.Keys.Where(k => !before.ContainsKey(k))))
        {
            var inBefore = before.TryGetValue(field, out var oldLeaf);
            var inAfter = after.TryGetValue(field, out var newLeaf);
            if (inBefore && inAfter && JsonNode.DeepEquals(oldLeaf!.Raw, newLeaf!.Raw)) continue;
            changes.Add(new AuditFieldChangeDto(field, (inBefore, inAfter) switch
            {
                (false, _) => "Added",
                (_, false) => "Removed",
                _ => "Modified",
            }, ToElement(oldLeaf?.Masked), ToElement(newLeaf?.Masked)));
        }
        return changes;
    }

    public static JsonElement? ToElement(JsonNode? node) => node is null ? null : JsonSerializer.SerializeToElement(node);

    private sealed record Leaf(JsonNode? Raw, JsonNode? Masked);

    /// <summary>Trường lá theo đường dẫn; khóa nhạy cảm dừng ở chính khóa đó (không đi sâu, giá trị che nguyên khối).</summary>
    private static Dictionary<string, Leaf> Flatten(JsonNode? root)
    {
        var leaves = new Dictionary<string, Leaf>(StringComparer.Ordinal);
        if (root is JsonObject obj) Walk(obj, null, leaves);
        else if (root is not null) leaves[RootPath] = new Leaf(root, AuditValueMasker.Mask(root));
        return leaves;
    }

    private static void Walk(JsonObject obj, string? prefix, Dictionary<string, Leaf> leaves)
    {
        foreach (var (key, value) in obj)
        {
            var path = prefix is null ? key : $"{prefix}.{key}";
            if (value is JsonObject child && !AuditValueMasker.IsSensitive(key)) Walk(child, path, leaves);
            else leaves[path] = new Leaf(value, AuditValueMasker.MaskProperty(key, value));
        }
    }
}

// ===== USE CASE (đọc) =====
public interface IGetAuditLogUseCase
{
    Task<AuditLogDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>US-100 (UC-45) AC1: xem giá trị trước/sau, lý do, người thực hiện của một thao tác. Chỉ đọc, che dữ liệu nhạy cảm khi trả về.</summary>
public sealed class GetAuditLogUseCase(IAuditLogQueries queries) : IGetAuditLogUseCase
{
    public async Task<AuditLogDetailDto> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ValidationException("id phải là số nguyên dương.");
        var log = await queries.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Audit log", id);

        var oldValues = AuditValueMasker.Parse(log.OldValuesJson);
        var newValues = AuditValueMasker.Parse(log.NewValuesJson);
        return new AuditLogDetailDto(log.Id, log.CreatedAtUtc, log.SourceService, log.UserId, log.OwnerProfileId, log.Action,
            log.EntityName, log.EntityId, log.Reason, log.IpAddress,
            AuditChangeDiff.ToElement(AuditValueMasker.Mask(oldValues)), AuditChangeDiff.ToElement(AuditValueMasker.Mask(newValues)),
            AuditChangeDiff.Compare(oldValues, newValues));
    }
}
