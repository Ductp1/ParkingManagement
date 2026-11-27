import type { AdminQueryIssue, AdminQueryStatus } from '@/types/adminCommon';

const QUERY_ISSUES: ReadonlySet<AdminQueryStatus> = new Set<AdminQueryStatus>(['notFound', 'offline', 'sessionExpired']);

/** notFound / offline / sessionExpired – hiển thị bằng QueryIssueState. */
export const isQueryIssue = (status: AdminQueryStatus): status is AdminQueryIssue => QUERY_ISSUES.has(status);

/** Mọi trạng thái lỗi (không có dữ liệu để hiển thị): error, forbidden, unavailable, notFound, offline, sessionExpired. */
export const isQueryFailure = (status: AdminQueryStatus): boolean =>
  status === 'error' || status === 'forbidden' || status === 'unavailable' || isQueryIssue(status);
