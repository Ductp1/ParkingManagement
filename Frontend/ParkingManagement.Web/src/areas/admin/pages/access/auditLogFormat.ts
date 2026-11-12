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
    // T-803: quyền dữ liệu cá nhân (UserService) – mở người dùng / yêu cầu tương ứng.
    case 'User':
      return `/admin/operations/users/${id}`;
    case 'DataSubjectRequest':
      return `/admin/access/security-privacy?request=${id}`;
    default:
      return null;
  }
};

/**
 * T-803: EntityName mà UserService sẽ dùng khi phát AuditRecorded (contract đề xuất trong Security Handoff – Lê Quang Trường):
 * duyệt / từ chối KYC → "User" + EntityId = userId; xử lý yêu cầu dữ liệu cá nhân → "DataSubjectRequest" + EntityId = id yêu cầu.
 */
export const KYC_AUDIT_ENTITY = 'User';
export const DSR_AUDIT_ENTITY = 'DataSubjectRequest';

type AuditFilterLink = {
  /** Service gửi sự kiện (?service=), ví dụ "UserService" cho KYC / yêu cầu dữ liệu cá nhân (T-803). */
  service?: string;
  entity?: string;
  entityId?: string | null;
  /** Người THỰC HIỆN thao tác (AuditLogs.UserId), không phải người bị tác động. */
  userId?: number | null;
  ownerProfileId?: number | null;
};

/** Link sang danh sách Audit Log đã lọc sẵn (dùng ở chi tiết audit và ở các màn khác: user, KYB, đối tác, tham số, quyền dữ liệu…). */
export const auditLogListLink = (filter: AuditFilterLink) => {
  const params = new URLSearchParams();
  if (filter.service) params.set('service', filter.service);
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
