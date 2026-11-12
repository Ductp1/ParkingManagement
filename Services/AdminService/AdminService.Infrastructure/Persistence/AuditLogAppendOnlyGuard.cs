using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdminService.Infrastructure.Persistence;

/// <summary>
/// US-100 AC2: audit log chỉ được thêm, không được sửa hoặc xóa. Chặn ở SaveChanges để mọi đường ghi qua EF đều tuân thủ,
/// kể cả code viết sau này. Lỗi lập trình → InvalidOperationException (API trả 500), không lưu gì cả.
/// ExecuteUpdate / ExecuteDelete / SQL tay đi thẳng xuống database nên không qua đây – AdminService không dùng cho AuditLogs.
/// </summary>
public static class AuditLogAppendOnlyGuard
{
    public static void Check(ChangeTracker changeTracker)
    {
        var changed = changeTracker.Entries<AuditLog>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (changed is not null)
            throw new InvalidOperationException(
                $"Audit log #{changed.Entity.Id} không được sửa hoặc xóa (trạng thái {changed.State}). Audit log chỉ được thêm mới.");
    }
}
