/**
 * DTO AdminService – Audit Log (US-100). Dòng audit đến từ AdminService (ghi trực tiếp) và từ service khác (AuditRecorded, T-802).
 * Nguồn: Services/AdminService/AdminService.Application/Features/AuditLogs/*
 */
export interface AuditLogSummaryDto {
  id: number;
  createdAtUtc: string;
  sourceService: string;
  userId: number | null;
  ownerProfileId: number | null;
  action: string;
  entityName: string;
  entityId: string | null;
  reason: string | null;
}

/** Kind = Added | Removed | Modified; field = đường dẫn có dấu chấm ("$" khi JSON gốc không phải object). Giá trị đã che. */
export interface AuditFieldChangeDto {
  field: string;
  kind: string;
  oldValue: unknown;
  newValue: unknown;
}

export interface AuditLogDetailDto extends AuditLogSummaryDto {
  ipAddress: string | null;
  oldValues: unknown;
  newValues: unknown;
  changes: AuditFieldChangeDto[];
}

export interface AuditLogFacetDto {
  value: string;
  count: number;
}

export interface AuditLogFilterOptionsDto {
  totalCount: number;
  sourceServices: AuditLogFacetDto[];
  actions: AuditLogFacetDto[];
  entityNames: AuditLogFacetDto[];
}

/** Tham số lọc của GET /audit-logs và /audit-logs/export (from / to là ISO UTC, to không bao gồm). */
export interface AuditLogListQuery {
  from?: string | null;
  to?: string | null;
  sourceService?: string;
  action?: string;
  entityName?: string;
  entityId?: string;
  userId?: number | null;
  ownerProfileId?: number | null;
  q?: string;
}
