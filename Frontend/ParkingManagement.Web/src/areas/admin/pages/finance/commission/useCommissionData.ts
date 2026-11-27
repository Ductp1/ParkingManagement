import { useCallback, useEffect, useRef, useState } from 'react';
import { AdminApiError } from '@/api/adminClientApi';
import { adminCommissionApi } from '@/api/adminCommissionApi';
import type { AdminQueryStatus } from '@/types/adminCommon';
import type { AdminCommissionSummaryDto, CommissionRateListDto, CommissionRateSource } from '@/types/adminCommission';
import { toQueryStatus } from '../../../hooks/useAdminQuery';

export interface SourceQuery<T> {
  data: T | null;
  error: AdminApiError | null;
  /** loading = chưa có dữ liệu; ready; error / forbidden / unavailable; idle = không gọi (kỳ tùy chọn sai). */
  status: AdminQueryStatus | 'idle';
  /** Đang tải cho bộ lọc hiện tại (dữ liệu cũ vẫn hiện, bảng mờ đi). */
  isFetching: boolean;
  retry: () => void;
}

interface Settled<T> {
  requestId: string;
  data: T | null;
  error: AdminApiError | null;
}

const toAdminError = (err: unknown): AdminApiError =>
  err instanceof AdminApiError ? err : new AdminApiError(0, err instanceof Error ? err.message : String(err));

/**
 * Một nguồn dữ liệu có thể hủy: đổi key (bộ lọc) → AbortController hủy request cũ, kết quả về muộn bị bỏ.
 * Giữ dữ liệu cũ trong lúc tải lại để bảng không nháy skeleton; lần đầu chưa có dữ liệu → loading (skeleton).
 * State chỉ được ghi khi request kết thúc (không setState đồng bộ trong effect).
 */
const useAbortableQuery = <T,>(key: string | null, fetcher: (signal: AbortSignal) => Promise<T>): SourceQuery<T> => {
  const [settled, setSettled] = useState<Settled<T> | null>(null);
  const [lastData, setLastData] = useState<T | null>(null);
  const [attempt, setAttempt] = useState(0);
  const fetcherRef = useRef(fetcher);
  useEffect(() => {
    fetcherRef.current = fetcher;
  });

  const requestId = key === null ? null : `${key}#${attempt}`;

  useEffect(() => {
    if (requestId === null) return;
    const controller = new AbortController();
    fetcherRef
      .current(controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        setSettled({ requestId, data, error: null });
        setLastData(data);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setSettled({ requestId, data: null, error: toAdminError(err) });
        setLastData(null);
      });
    return () => controller.abort();
  }, [requestId]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  if (requestId === null) return { data: null, error: null, status: 'idle', isFetching: false, retry };
  const current = settled?.requestId === requestId ? settled : null;
  const error = current?.error ?? null;
  const data = current ? current.data : lastData;
  let status: AdminQueryStatus;
  if (error) status = toQueryStatus(error);
  else if (data === null) status = 'loading';
  else status = 'ready';
  return { data, error, status, isFetching: current === null, retry };
};

/**
 * US-079: hai nguồn độc lập của trang Hoa hồng – lỗi nguồn này không làm mất nguồn kia.
 *  - summary (PaymentService): gọi lại khi đổi kỳ.
 *  - rates (AdminService): gọi lại khi đổi tìm kiếm / nguồn tỷ lệ.
 * range = null (kỳ tùy chọn sai) → không gọi PaymentService.
 */
export const useCommissionData = (range: { fromUtc: string; toUtc: string } | null, search: string, source: CommissionRateSource | null) => {
  const summary = useAbortableQuery<AdminCommissionSummaryDto>(range ? `${range.fromUtc}|${range.toUtc}` : null, (signal) =>
    adminCommissionApi.summary(range!.fromUtc, range!.toUtc, signal)
  );
  const rates = useAbortableQuery<CommissionRateListDto>(`${search}|${source ?? ''}`, (signal) =>
    adminCommissionApi.allRates({ search: search || undefined, source: source ?? undefined }, signal)
  );
  return { summary, rates };
};
