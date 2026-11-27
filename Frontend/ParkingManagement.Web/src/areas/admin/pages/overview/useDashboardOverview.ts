import { useCallback, useEffect, useRef, useState } from 'react';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminQueryStatus } from '@/types/adminCommon';
import { toQueryStatus } from '../../hooks/useAdminQuery';
import { useVisibleInterval } from '../../hooks/useVisibleInterval';
import { adminDashboardApi } from './adminDashboardApi';
import type { DashboardOverviewDto } from './dashboardTypes';

/** US-081: tự làm mới khi tab đang hiển thị; Admin vẫn có nút làm mới thủ công. */
export const REFRESH_INTERVAL_MS = 60_000;

export interface DashboardOverviewQuery {
  data: DashboardOverviewDto | null;
  /** Lỗi của lần tải gần nhất (lần đầu → cả trang lỗi; lần làm mới → giữ số cũ + báo lỗi nhỏ). */
  error: AdminApiError | null;
  /** loading = lần tải đầu; ready = đã có dữ liệu; error / forbidden / unavailable = lần đầu đã lỗi. */
  status: AdminQueryStatus;
  isFetching: boolean;
  /** Tăng mỗi lần tải thành công – dùng thông báo aria-live "đã cập nhật". */
  completedCount: number;
  refresh: () => void;
}

const toAdminError = (err: unknown): AdminApiError =>
  err instanceof AdminApiError ? err : new AdminApiError(0, err instanceof Error ? err.message : String(err));

/**
 * Tải overview: mỗi lần một request (đang chờ thì lần làm mới tiếp theo bị bỏ qua – không gửi chồng), giữ dữ liệu cũ trong lúc
 * tải lại (không nháy skeleton), rời trang thì AbortController hủy request đang chạy và bỏ kết quả về muộn.
 */
export const useDashboardOverview = (): DashboardOverviewQuery => {
  const [data, setData] = useState<DashboardOverviewDto | null>(null);
  const [error, setError] = useState<AdminApiError | null>(null);
  const [isFetching, setIsFetching] = useState(true);
  const [completedCount, setCompletedCount] = useState(0);
  const inFlight = useRef<AbortController | null>(null);

  const refresh = useCallback(() => {
    if (inFlight.current) return;
    const controller = new AbortController();
    inFlight.current = controller;
    setIsFetching(true);
    adminDashboardApi
      .overview(controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setData(result);
        setError(null);
        setCompletedCount((n) => n + 1);
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) setError(toAdminError(err));
      })
      .finally(() => {
        if (inFlight.current !== controller) return;
        inFlight.current = null;
        setIsFetching(false);
      });
  }, []);

  useEffect(() => {
    refresh();
    return () => {
      inFlight.current?.abort();
      inFlight.current = null;
    };
  }, [refresh]);

  useVisibleInterval(refresh, REFRESH_INTERVAL_MS);

  let status: AdminQueryStatus = 'ready';
  if (data === null) status = error ? toQueryStatus(error) : 'loading';

  return { data, error, status, isFetching, completedCount, refresh };
};
