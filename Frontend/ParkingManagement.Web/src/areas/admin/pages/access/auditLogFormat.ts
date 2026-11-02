/** Dùng chung cho màn Audit Log (US-100): danh sách, slide-over xem nhanh và trang chi tiết. */
export const AUDIT_LOG_BASE = '/admin/access/audit-log';

/** Audit của AdminService ghi trực tiếp; service khác gửi qua sự kiện AuditRecorded (T-802). */
export const OWN_SERVICE = 'AdminService';

/** "U#12" hoặc "System" khi thao tác không gắn người dùng. */
export const auditUserText = (userId: number | null) => (userId === null ? 'System' : `U#${userId}`);

export const auditUserLink = (userId: number | null) => (userId === null ? null : `/admin/operations/users/${userId}`);

/**
 * Màn quản trị của bản ghi bị tác động (EntityName + EntityId). Chỉ các entity có màn chi tiết trong portal;
 * còn lại (OwnerProfile, RateCard, Review, LotContract…) trả null.
 */
export const auditEntityLink = (entityName: string, entityId: string | null): string | null => {
  if (!entityId) return null;
  const id = encodeURIComponent(entityId);
  switch (entityName) {
    case 'KybApplication':
      return `/admin/operations/kyb-review/${id}`;
    case 'PartnerApplication':
      return `/admin/operations/partner-onboarding/${id}`;
    case 'ContractTemplate':
      return `/admin/operations/contract-templates/${id}`;
    case 'SystemConfig':
      return `/admin/access/system-settings/${id}`;
    default:
      return null;
  }
};

type AuditFilterLink = { entity?: string; entityId?: string | null; userId?: number | null; ownerProfileId?: number | null };

/** Link sang danh sách Audit Log đã lọc sẵn (dùng ở chi tiết audit và ở các màn khác: user, KYB, đối tác, tham số…). */
export const auditLogListLink = (filter: AuditFilterLink) => {
  const params = new URLSearchParams();
  if (filter.entity) params.set('entity', filter.entity);
  if (filter.entityId) params.set('entityId', filter.entityId);
  if (filter.userId) params.set('userId', String(filter.userId));
  if (filter.ownerProfileId) params.set('ownerProfileId', String(filter.ownerProfileId));
  const query = params.toString();
  return query ? `${AUDIT_LOG_BASE}?${query}` : AUDIT_LOG_BASE;
};

export type AuditChangeTone = 'added' | 'removed' | 'modified';

export const auditChangeTone = (kind: string): AuditChangeTone =>
  kind === 'Added' ? 'added' : kind === 'Removed' ? 'removed' : 'modified';

/** Giá trị JSON (đã che) → chuỗi một dòng; undefined = trường không có ở phía đó. */
export const auditValueText = (value: unknown): string =>
  value === undefined ? '—' : typeof value === 'string' ? value : JSON.stringify(value);
