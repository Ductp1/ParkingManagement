import { useEffect } from 'react';
import { useOutletContext } from 'react-router-dom';

export interface AdminOutletContext {
  setDetailLabel: (label: string | null) => void;
}

/** Màn chi tiết gọi hook này để hiện breadcrumb cấp 3 (Section / Tab / {label}); tự xóa khi rời màn. */
export const useAdminBreadcrumb = (label: string | null): void => {
  const { setDetailLabel } = useOutletContext<AdminOutletContext>();

  useEffect(() => {
    setDetailLabel(label);
    return () => setDetailLabel(null);
  }, [label, setDetailLabel]);
};
