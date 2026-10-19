import { useCallback, useEffect, useRef, useState } from 'react';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminQueryStatus } from '@/types/adminCommon';

export interface AdminQueryResult<T> {
  data: T | null;
  status: AdminQueryStatus;
  error: AdminApiError | null;
  isFetching: boolean;
  refetch: () => void;
}

/** 404/405/501 hoặc không kết nối được = endpoint chưa có/chưa chạy → hiển thị EmptyState thật. */
const UNAVAILABLE = new Set([0, 404, 405, 501]);

export const toQueryStatus = (error: AdminApiError): AdminQueryStatus => {
  if (error.status === 403) return 'forbidden';
  if (UNAVAILABLE.has(error.status)) return 'unavailable';
  return 'error';
};

const toAdminError = (err: unknown): AdminApiError =>
  err instanceof AdminApiError ? err : new AdminApiError(0, err instanceof Error ? err.message : String(err));

/**
 * Gọi API khi deps đổi, giữ status loading/ready/empty/error/forbidden/unavailable cho màn hình.
 * fetcher = null → chưa đủ điều kiện gọi (ví dụ chưa chọn bãi), status = 'empty'.
 * isEmpty quyết định khi nào dữ liệu trả về được coi là rỗng (EmptyState).
 */
export const useAdminQuery = <T>(
  fetcher: (() => Promise<T>) | null,
  deps: ReadonlyArray<unknown>,
  isEmpty?: (data: T) => boolean
): AdminQueryResult<T> => {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<AdminApiError | null>(null);
  const [isFetching, setIsFetching] = useState(fetcher !== null);
  const [reloadKey, setReloadKey] = useState(0);
  const fetcherRef = useRef(fetcher);
  const isEmptyRef = useRef(isEmpty);
  fetcherRef.current = fetcher;
  isEmptyRef.current = isEmpty;

  const enabled = fetcher !== null;

  useEffect(() => {
    const run = fetcherRef.current;
    if (!run) return;
    let cancelled = false;
    setIsFetching(true);
    setError(null);
    run()
      .then((result) => {
        if (!cancelled) setData(result);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setData(null);
          setError(toAdminError(err));
        }
      })
      .finally(() => {
        if (!cancelled) setIsFetching(false);
      });
    return () => {
      cancelled = true;
    };
    // deps do màn hình truyền vào quyết định khi nào gọi lại
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, reloadKey, ...deps]);

  const refetch = useCallback(() => setReloadKey((k) => k + 1), []);

  let status: AdminQueryStatus;
  if (!enabled) status = 'empty';
  else if (isFetching && data === null) status = 'loading';
  else if (error) status = toQueryStatus(error);
  else if (data === null) status = 'loading';
  else status = isEmptyRef.current?.(data) ? 'empty' : 'ready';

  return { data, status, error, isFetching, refetch };
};
