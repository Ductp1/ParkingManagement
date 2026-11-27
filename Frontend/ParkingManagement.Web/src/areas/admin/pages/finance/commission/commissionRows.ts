import type {
  AdminCommissionByRateSourceDto,
  AdminCommissionSummaryDto,
  CommissionLotRateDto,
  CommissionRateListDto,
  CommissionRateReason,
  CommissionRateSource,
} from '@/types/adminCommission';

/**
 * US-079: ghép 2 nguồn theo parkingLotId – tỷ lệ hiện tại (AdminService) + số hoa hồng trong kỳ (PaymentService).
 * Một nguồn chưa có / lỗi → cột của nguồn đó là null (hiện "—"), phần còn lại vẫn hiển thị.
 */

export const SOURCE_LABEL: Record<CommissionRateSource, string> = { LotContract: 'Hợp đồng', DefaultConfig: 'Mặc định' };

export const REASON_LABEL: Record<CommissionRateReason, string> = {
  BeforeEffective: 'Hợp đồng chưa đến ngày hiệu lực',
  AfterExpiry: 'Hợp đồng đã hết hạn',
  NoSignedContract: 'Chưa có hợp đồng đã ký',
};

export const isRateSource = (v: string): v is CommissionRateSource => v === 'LotContract' || v === 'DefaultConfig';

export interface CommissionMoney {
  gross: number;
  refunded: number;
  net: number;
  commission: number;
  effectiveRate: number | null;
  transactionCount: number;
  pendingCount: number;
}

export interface CommissionLotRow {
  parkingLotId: number;
  lotName: string;
  district: string | null;
  ownerProfileId: number | null;
  ownerName: string | null;
  /** Bãi có trong danh sách tỷ lệ của AdminService (false = chỉ thấy trong sổ hoa hồng → "Bãi #id"). */
  known: boolean;
  /** Tỷ lệ hiện tại (phân số) – null khi chưa đọc được AdminService. */
  currentRate: number | null;
  /** Nguồn tỷ lệ hiện tại; bãi chưa biết → nguồn đã dùng trong sổ hoa hồng (nếu một). */
  source: CommissionRateSource | null;
  reason: CommissionRateReason | null;
  contractId: number | null;
  contractNumber: string | null;
  contractVersion: number | null;
  /** Nguồn tỷ lệ đã dùng cho các giao dịch trong kỳ. */
  ledgerSources: CommissionRateSource[];
  /** null khi chưa đọc được PaymentService. */
  money: CommissionMoney | null;
}

export interface CommissionOwnerRow {
  ownerProfileId: number;
  ownerName: string | null;
  lotCount: number;
  money: CommissionMoney | null;
}

export interface CommissionSourceRow {
  source: CommissionRateSource;
  /** Số bãi đang áp dụng nguồn này (theo tỷ lệ hiện tại) – null khi chưa đọc được AdminService. */
  lotCount: number | null;
  stats: AdminCommissionByRateSourceDto | null;
}

export interface CommissionFilter {
  search: string;
  source: CommissionRateSource | null;
}

const ZERO: CommissionMoney = { gross: 0, refunded: 0, net: 0, commission: 0, effectiveRate: null, transactionCount: 0, pendingCount: 0 };

/** Bỏ dấu + "đ" → "d" + chữ thường để tìm "ba dinh" ra "Ba Đình". */
export const fold = (text: string) =>
  text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();

const matches = (term: string, ...fields: Array<string | number | null | undefined>) => {
  const needle = fold(term.trim().replace(/^#/, ''));
  return needle === '' || fields.some((f) => f !== null && f !== undefined && fold(String(f)).includes(needle));
};

export const buildLotRows = (
  rates: CommissionRateListDto | null,
  summary: AdminCommissionSummaryDto | null,
  filter: CommissionFilter
): CommissionLotRow[] => {
  const rateById = new Map<number, CommissionLotRateDto>((rates?.lots.items ?? []).map((r) => [r.parkingLotId, r]));
  const moneyById = new Map((summary?.byLot ?? []).map((l) => [l.parkingLotId, l]));
  const filtered = filter.search.trim() !== '' || filter.source !== null;

  const rows: CommissionLotRow[] = [];
  for (const rate of rateById.values()) {
    const m = moneyById.get(rate.parkingLotId);
    rows.push({
      parkingLotId: rate.parkingLotId,
      lotName: rate.lotName,
      district: rate.district,
      ownerProfileId: rate.ownerProfileId ?? m?.ownerProfileId ?? null,
      ownerName: rate.ownerName,
      known: true,
      currentRate: rate.currentRate,
      source: rate.source,
      reason: rate.reason,
      contractId: rate.contractId,
      contractNumber: rate.contractNumber,
      contractVersion: rate.contractVersion,
      ledgerSources: m?.rateSources ?? [],
      money: summary ? (m ? toMoney(m) : ZERO) : null,
    });
  }

  // Bãi có giao dịch nhưng không có trong danh sách tỷ lệ → vẫn hiện ("Bãi #id"), không ẩn.
  for (const m of moneyById.values()) {
    if (rateById.has(m.parkingLotId)) continue;
    const ledgerSource = m.rateSources.length === 1 ? m.rateSources[0] : null;
    const row: CommissionLotRow = {
      parkingLotId: m.parkingLotId,
      lotName: `Bãi #${m.parkingLotId}`,
      district: null,
      ownerProfileId: m.ownerProfileId,
      ownerName: ownerNameOf(rates, m.ownerProfileId),
      known: false,
      currentRate: null,
      source: ledgerSource,
      reason: null,
      contractId: null,
      contractNumber: null,
      contractVersion: null,
      ledgerSources: m.rateSources,
      money: toMoney(m),
    };
    // Danh sách tỷ lệ đã lọc ở AdminService: bãi ngoài danh sách chỉ hiện khi tự khớp bộ lọc.
    if (filtered && !(matches(filter.search, row.lotName, row.parkingLotId, row.ownerName) &&
      (filter.source === null || m.rateSources.includes(filter.source)))) continue;
    rows.push(row);
  }
  return rows;
};

const toMoney = (m: {
  grossAmount: number;
  refundedAmount: number;
  netAmount: number;
  commissionAmount: number;
  effectiveRate: number | null;
  transactionCount: number;
  pendingCount: number;
}): CommissionMoney => ({
  gross: m.grossAmount,
  refunded: m.refundedAmount,
  net: m.netAmount,
  commission: m.commissionAmount,
  effectiveRate: m.effectiveRate,
  transactionCount: m.transactionCount,
  pendingCount: m.pendingCount,
});

const ownerNameOf = (rates: CommissionRateListDto | null, ownerProfileId: number) =>
  rates?.lots.items.find((r) => r.ownerProfileId === ownerProfileId && r.ownerName)?.ownerName ?? null;

export const buildOwnerRows = (
  rates: CommissionRateListDto | null,
  summary: AdminCommissionSummaryDto | null,
  lotRows: CommissionLotRow[],
  filter: CommissionFilter
): CommissionOwnerRow[] => {
  const visibleOwners = new Set(lotRows.map((r) => r.ownerProfileId).filter((id): id is number => id !== null));
  const rows = new Map<number, CommissionOwnerRow>();

  for (const o of summary?.byOwner ?? []) {
    rows.set(o.ownerProfileId, { ownerProfileId: o.ownerProfileId, ownerName: ownerNameOf(rates, o.ownerProfileId), lotCount: o.lotCount, money: toMoney(o) });
  }
  // Chủ bãi chưa có giao dịch trong kỳ (hoặc PaymentService lỗi): lấy từ danh sách tỷ lệ.
  for (const r of rates?.lots.items ?? []) {
    if (r.ownerProfileId === null || rows.has(r.ownerProfileId)) continue;
    rows.set(r.ownerProfileId, {
      ownerProfileId: r.ownerProfileId,
      ownerName: r.ownerName,
      lotCount: rates!.lots.items.filter((x) => x.ownerProfileId === r.ownerProfileId).length,
      money: summary ? ZERO : null,
    });
  }

  // Có lọc: giữ chủ bãi có bãi đang hiện trong bảng theo bãi, hoặc (chỉ tìm kiếm) tên / mã chủ bãi khớp.
  const filtered = filter.search.trim() !== '' || filter.source !== null;
  return [...rows.values()].filter(
    (o) =>
      !filtered ||
      visibleOwners.has(o.ownerProfileId) ||
      (filter.source === null && matches(filter.search, o.ownerName, o.ownerProfileId))
  );
};

export const buildSourceRows = (rates: CommissionRateListDto | null, summary: AdminCommissionSummaryDto | null): CommissionSourceRow[] =>
  (['LotContract', 'DefaultConfig'] as const).map((source) => ({
    source,
    lotCount: rates ? rates.lots.items.filter((r) => r.source === source).length : null,
    stats: summary ? summary.byRateSource.find((s) => s.source === source) ?? { source, transactionCount: 0, baseAmount: 0, refundedAmount: 0, commissionAmount: 0, effectiveRate: null } : null,
  }));

// ===== Sắp xếp (bảng theo bãi) =====

export type LotSort = 'commission' | 'gross' | 'rate' | 'pending' | 'name';

export const LOT_SORT_OPTIONS: Array<{ value: LotSort; label: string }> = [
  { value: 'commission', label: 'Hoa hồng ↓' },
  { value: 'gross', label: 'GMV ↓' },
  { value: 'rate', label: 'Tỷ lệ hiện tại ↓' },
  { value: 'pending', label: 'Chờ tính ↓' },
  { value: 'name', label: 'Tên bãi A→Z' },
];

export const isLotSort = (v: string): v is LotSort => LOT_SORT_OPTIONS.some((o) => o.value === v);

const collator = new Intl.Collator('vi-VN', { sensitivity: 'base' });
const desc = (a: number | null | undefined, b: number | null | undefined) => (b ?? -1) - (a ?? -1);

export const sortLotRows = (rows: CommissionLotRow[], sort: LotSort): CommissionLotRow[] =>
  [...rows].sort((a, b) => {
    const byKey =
      sort === 'gross' ? desc(a.money?.gross, b.money?.gross)
      : sort === 'rate' ? desc(a.currentRate, b.currentRate)
      : sort === 'pending' ? desc(a.money?.pendingCount, b.money?.pendingCount)
      : sort === 'name' ? collator.compare(a.lotName, b.lotName)
      : desc(a.money?.commission, b.money?.commission);
    return byKey !== 0 ? byKey : collator.compare(a.lotName, b.lotName) || a.parkingLotId - b.parkingLotId;
  });

export const sortOwnerRows = (rows: CommissionOwnerRow[]): CommissionOwnerRow[] =>
  [...rows].sort((a, b) => desc(a.money?.commission, b.money?.commission) || a.ownerProfileId - b.ownerProfileId);
