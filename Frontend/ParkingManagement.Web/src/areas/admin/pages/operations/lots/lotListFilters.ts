import { ParkingLotStatus } from '@/types/adminEnums';
import type { AdminLotSummaryRow } from '@/types/adminLots';

/** US-081: bộ lọc màn Parking Lots (lọc phía client – lots-summary trả toàn bộ bãi, không phân trang). */
export interface LotListFilters {
  status: string;
  flag: string;
  city: string;
  tier: string;
  search: string;
}

export type LotFilterGroup = 'status' | 'flag' | 'city' | 'tier' | 'search';

export const LOT_STATUSES: string[] = Object.values(ParkingLotStatus);

export const LOT_FLAGS = [
  { value: 'emergency', label: 'Emergency stopped' },
  { value: 'paused', label: 'Instant booking paused' },
] as const;

const matchesFlag = (lot: AdminLotSummaryRow, flag: string) =>
  flag === 'emergency' ? lot.isEmergencyStopped : flag === 'paused' ? lot.instantBookingPaused : true;

const matchesSearch = (lot: AdminLotSummaryRow, search: string) => {
  if (search === '') return true;
  const q = search.toLowerCase();
  return [lot.name, lot.city, lot.district ?? ''].some((x) => x.toLowerCase().includes(q));
};

/** Áp mọi bộ lọc, trừ nhóm `skip` (để đếm số bãi cho từng mục của nhóm đó). */
export const filterLots = (lots: AdminLotSummaryRow[], f: LotListFilters, skip?: LotFilterGroup): AdminLotSummaryRow[] =>
  lots.filter(
    (lot) =>
      (skip === 'status' || f.status === '' || lot.status === f.status) &&
      (skip === 'flag' || f.flag === '' || matchesFlag(lot, f.flag)) &&
      (skip === 'city' || f.city === '' || lot.city === f.city) &&
      (skip === 'tier' || f.tier === '' || lot.integrationTier === f.tier) &&
      (skip === 'search' || matchesSearch(lot, f.search))
  );

/** Số bãi cho từng giá trị của một nhóm, khi các nhóm khác vẫn đang lọc. */
export const countBy = (lots: AdminLotSummaryRow[], f: LotListFilters, group: 'status' | 'flag', values: string[]) => {
  const base = filterLots(lots, f, group);
  return Object.fromEntries(
    values.map((v) => [v, group === 'status' ? base.filter((l) => l.status === v).length : base.filter((l) => matchesFlag(l, v)).length])
  ) as Record<string, number>;
};

/** Giá trị phân biệt (sắp theo bảng chữ cái tiếng Việt) – dùng cho dropdown City / Integration. */
export const distinct = (values: string[]): string[] => [...new Set(values.filter((v) => v !== ''))].sort((a, b) => a.localeCompare(b, 'vi'));
