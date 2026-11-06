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
 * - unavailable: 404/405/501/lỗi mạng tới endpoint chưa có → EmptyState thật, không hiển thị dữ liệu giả
 * - error: lỗi khác → ErrorState + Retry
 */
export type AdminQueryStatus = 'loading' | 'ready' | 'empty' | 'error' | 'forbidden' | 'unavailable';
