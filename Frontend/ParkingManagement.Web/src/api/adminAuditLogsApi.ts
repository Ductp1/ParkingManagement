import { adminFileRequest, adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type { AdminFile } from '@/types/adminPartnerOnboarding';
import type { AuditLogDetailDto, AuditLogFilterOptionsDto, AuditLogListQuery, AuditLogSummaryDto } from '@/types/adminAuditLogs';

const BASE = '/api/v1/admin/audit-logs';

const filterParams = (q: AuditLogListQuery) =>
  cleanParams({
    from: q.from,
    to: q.to,
    sourceService: q.sourceService,
    action: q.action,
    entityName: q.entityName,
    entityId: q.entityId,
    userId: q.userId,
    ownerProfileId: q.ownerProfileId,
    q: q.q,
  });

export const adminAuditLogsApi = {
  /**
   * Tra cứu audit log toàn hệ thống, mới nhất trước (US-100).
   */
  list: (query: AuditLogListQuery, page: number, pageSize: number) =>
    adminRequest<PagedResult<AuditLogSummaryDto>>({ method: 'GET', url: BASE, params: { ...filterParams(query), page, pageSize } }),

  /**
   * Chi tiết một dòng: giá trị cũ / mới đã che dữ liệu nhạy cảm + các trường thay đổi (US-100).
   */
  getById: (id: number) => adminRequest<AuditLogDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /**
   * Giá trị cho cột lọc / dropdown: SourceService, Action, EntityName kèm số dòng (US-100).
   */
  filterOptions: () => adminRequest<AuditLogFilterOptionsDto>({ method: 'GET', url: `${BASE}/filters` }),

  /**
   * Xuất CSV theo đúng bộ lọc của danh sách, tối đa 10.000 dòng (US-100).
   */
  export: (query: AuditLogListQuery): Promise<AdminFile> =>
    adminFileRequest({ method: 'GET', url: `${BASE}/export`, params: filterParams(query) }),
};
