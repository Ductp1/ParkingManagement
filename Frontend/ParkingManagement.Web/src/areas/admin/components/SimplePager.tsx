import React from 'react';
import { AdminButton } from './AdminButton';

/**
 * Phân trang cho API chỉ trả mảng (không có tổng số dòng): Previous / Page N / Next.
 * Còn trang sau khi trang hiện tại đầy (rowCount === pageSize).
 */
export const SimplePager: React.FC<{ page: number; pageSize: number; rowCount: number; onPageChange: (page: number) => void }> = ({
  page,
  pageSize,
  rowCount,
  onPageChange,
}) => (
  <div className="flex h-12 items-center justify-between gap-3 border-t border-(--admin-border-default) px-3">
    <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">
      Page {page}
      {rowCount > 0 && ` · rows ${(page - 1) * pageSize + 1}–${(page - 1) * pageSize + rowCount}`}
    </span>
    <div className="flex items-center gap-2">
      <AdminButton variant="secondary" icon="chevronLeft" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </AdminButton>
      <AdminButton variant="secondary" disabled={rowCount < pageSize} onClick={() => onPageChange(page + 1)}>
        Next
      </AdminButton>
    </div>
  </div>
);
