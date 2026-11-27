import { useMemo } from 'react';
import { adminLotsApi } from '@/api/adminLotsApi';
import type { AdminLotSummaryRow } from '@/types/adminLots';
import { useAdminQuery, type AdminQueryResult } from './useAdminQuery';

export interface LotOptionsResult {
  query: AdminQueryResult<AdminLotSummaryRow[]>;
  /** Bãi sắp theo tên (thứ tự của API). Rỗng khi chưa tải xong hoặc lỗi. */
  lots: AdminLotSummaryRow[];
  /** Tra tên bãi theo id; undefined khi chưa có dữ liệu hoặc id không tồn tại. */
  byId: (id: number) => AdminLotSummaryRow | undefined;
}

/** US-081: danh sách bãi dùng chung cho bộ chọn bãi (LotPicker) và cột "Lot" – nguồn lots-summary (ParkingService). */
export const useLotOptions = (): LotOptionsResult => {
  const query = useAdminQuery(async () => (await adminLotsApi.lotsSummary()).lots, [], (lots) => lots.length === 0);
  const lots = useMemo(() => query.data ?? [], [query.data]);
  const index = useMemo(() => new Map(lots.map((l) => [l.parkingLotId, l])), [lots]);
  return useMemo(() => ({ query, lots, byId: (id: number) => index.get(id) }), [query, lots, index]);
};
