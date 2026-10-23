import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

export const DEFAULT_PAGE_SIZE = 20;

/**
 * Bộ lọc + phân trang của màn danh sách lưu trên URL (?page=&status=&q=...) để F5/Back giữ nguyên.
 * Đổi bộ lọc bất kỳ → quay về trang 1.
 */
export const useListParams = () => {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(1, Number(searchParams.get('page')) || 1);

  const get = useCallback((key: string): string => searchParams.get(key) ?? '', [searchParams]);

  const set = useCallback(
    (updates: Record<string, string | number | null>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          Object.entries(updates).forEach(([key, value]) => {
            if (value === null || value === '') next.delete(key);
            else next.set(key, String(value));
          });
          if (!('page' in updates)) next.delete('page');
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  const setPage = useCallback((value: number) => set({ page: value <= 1 ? null : value }), [set]);

  return useMemo(() => ({ page, get, set, setPage }), [page, get, set, setPage]);
};
