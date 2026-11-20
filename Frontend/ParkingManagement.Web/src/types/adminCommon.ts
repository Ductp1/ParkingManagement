/**
 * Types dùng chung cho Admin Portal.
 * PagedResult khớp SharedKernel/Contracts/PagedResult.cs (JSON camelCase, có thuộc tính tính toán TotalPages).
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Tham số phân trang chuẩn của các API danh sách (page bắt đầu từ 1). */
export interface PageQuery {
  page?: number;
  pageSize?: number;
}

/**
 * Trạng thái hiển thị của một truy vấn trong admin:
 * - forbidden: 403 → màn "No permission"
 * - sessionExpired: 401 → xóa phiên, chuyển về /login (không hiện lỗi chung)
 * - notFound: 404 có ProblemDetails → bản ghi không tồn tại / đã xóa
 * - unavailable: 404 không body / 405 / 501 → chức năng chưa có, không hiển thị dữ liệu giả
 * - offline: không tới được server → báo mất kết nối + Retry
 * - error: lỗi khác → ErrorState + Retry
 */
export type AdminQueryStatus =
  | 'loading'
  | 'ready'
  | 'empty'
  | 'error'
  | 'forbidden'
  | 'unavailable'
  | 'notFound'
  | 'offline'
  | 'sessionExpired';

/** Các trạng thái lỗi có màn riêng (ngoài error/forbidden/unavailable) – dùng chung QueryIssueState. */
export type AdminQueryIssue = 'notFound' | 'offline' | 'sessionExpired';
