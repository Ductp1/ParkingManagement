import { describe, expect, it } from 'vitest';
import type { AdminCommissionSummaryDto, CommissionRateListDto } from '@/types/adminCommission';
import { formatRate, lotCsv } from './commissionFormat';
import { ictToday, resolveRange } from './commissionPeriod';
import { buildLotRows, buildOwnerRows, fold, sortLotRows } from './commissionRows';

// US-079: kỳ theo ngày ICT + ghép tỷ lệ (AdminService) với số hoa hồng (PaymentService) – hàm thuần.

describe('commissionPeriod', () => {
  it('ngày ICT: 17:00 UTC đã là ngày hôm sau', () => {
    expect(ictToday(new Date('2026-11-05T16:59:59Z'))).toBe('2026-11-05');
    expect(ictToday(new Date('2026-11-05T17:00:00Z'))).toBe('2026-11-06');
  });

  it('Tháng này = ngày 1 → hôm nay (ICT), toUtc = 00:00 ICT ngày mai', () => {
    const r = resolveRange('', '', '', new Date('2026-11-06T03:00:00Z'));
    expect(r.ok && r.range).toMatchObject({ period: 'month', from: '2026-11-01', to: '2026-11-06', fromUtc: '2026-10-31T17:00:00.000Z', toUtc: '2026-11-06T17:00:00.000Z', days: 6 });
  });

  it('Tháng trước qua năm mới', () => {
    const r = resolveRange('prev-month', '', '', new Date('2027-01-10T03:00:00Z'));
    expect(r.ok && r.range).toMatchObject({ from: '2026-12-01', to: '2026-12-31', days: 31 });
  });

  it('7 ngày qua gồm hôm nay', () => {
    const r = resolveRange('7d', '', '', new Date('2026-11-06T03:00:00Z'));
    expect(r.ok && r.range).toMatchObject({ from: '2026-10-31', to: '2026-11-06', days: 7 });
  });

  it.each([
    ['2026-11-05', '2026-11-01', 'trước hoặc bằng'],
    ['2026-02-30', '2026-03-01', 'hợp lệ'],
    ['', '2026-03-01', 'hợp lệ'],
    ['2025-01-01', '2026-01-02', '366 ngày'],
  ])('Tùy chọn sai (%s → %s) → lỗi, không gọi API', (from, to, message) => {
    const r = resolveRange('custom', from, to, new Date('2026-11-06T03:00:00Z'));
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error).toContain(message);
  });

  it('Tùy chọn đúng 366 ngày được phép', () => {
    const r = resolveRange('custom', '2025-01-01', '2026-01-01', new Date('2026-11-06T03:00:00Z'));
    expect(r.ok && r.range.days).toBe(366);
  });
});

const rates: CommissionRateListDto = {
  asOfUtc: '', defaultRate: 0.1, defaultEffectiveFromUtc: null, defaultNextChange: null, partial: false, partialReason: null,
  lots: {
    items: [
      { parkingLotId: 1, lotName: 'Bãi Đống Đa', district: 'Đống Đa', city: null, lotStatus: 'Active', ownerProfileId: 7, ownerName: 'Công ty A',
        currentRate: 0.05, source: 'LotContract', reason: null, contractId: 1, contractNumber: 'HD-1', contractVersion: 1, effectiveFrom: null, effectiveTo: null },
      { parkingLotId: 2, lotName: 'An Bình', district: null, city: null, lotStatus: 'Active', ownerProfileId: null, ownerName: null,
        currentRate: 0.1, source: 'DefaultConfig', reason: 'NoSignedContract', contractId: null, contractNumber: null, contractVersion: null, effectiveFrom: null, effectiveTo: null },
    ],
    page: 1, pageSize: 2, totalCount: 2, totalPages: 1,
  },
};

const money = (commission: number, pending = 0) => ({
  grossAmount: commission * 10, refundedAmount: 0, netAmount: commission * 10, commissionAmount: commission, effectiveRate: 0.1,
  transactionCount: 1, pendingCount: pending, status: 'Calculated' as const,
});

const summary: AdminCommissionSummaryDto = {
  asOfUtc: '', fromUtc: '', toUtc: '', currency: 'VND', totals: money(600),
  byLot: [{ parkingLotId: 2, ownerProfileId: 8, rateSources: ['DefaultConfig'], ...money(500) }, { parkingLotId: 9, ownerProfileId: 7, rateSources: ['LotContract'], ...money(100, 1) }],
  byOwner: [{ ownerProfileId: 7, lotCount: 2, ...money(100, 1) }, { ownerProfileId: 8, lotCount: 1, ...money(500) }],
  byRateSource: [], series: [],
};

describe('commissionRows', () => {
  it('ghép theo parkingLotId; bãi chỉ có trong sổ hoa hồng → "Bãi #id", không ẩn', () => {
    const rows = buildLotRows(rates, summary, { search: '', source: null });
    expect(rows.map((r) => r.parkingLotId)).toEqual([1, 2, 9]);
    expect(rows[0].money).toMatchObject({ commission: 0, pendingCount: 0 });                // có tỷ lệ, chưa có giao dịch
    expect(rows[1]).toMatchObject({ ownerProfileId: 8, money: { commission: 500 } });       // chủ bãi bù từ sổ hoa hồng
    expect(rows[2]).toMatchObject({ lotName: 'Bãi #9', known: false, source: 'LotContract', ownerName: 'Công ty A' });
  });

  it('một nguồn chưa có → cột của nguồn đó null, phần còn lại vẫn có', () => {
    expect(buildLotRows(rates, null, { search: '', source: null }).every((r) => r.money === null)).toBe(true);
    const ledgerOnly = buildLotRows(null, summary, { search: '', source: null });
    expect(ledgerOnly.map((r) => r.lotName)).toEqual(['Bãi #2', 'Bãi #9']);
  });

  it('có lọc: bãi ngoài danh sách tỷ lệ chỉ hiện khi tự khớp', () => {
    expect(buildLotRows(rates, summary, { search: '#9', source: null }).map((r) => r.parkingLotId)).toContain(9);
    expect(buildLotRows(rates, summary, { search: 'đống', source: null }).map((r) => r.parkingLotId)).not.toContain(9);
    expect(buildLotRows(rates, summary, { search: '', source: 'DefaultConfig' }).map((r) => r.parkingLotId)).not.toContain(9);
  });

  it('chủ bãi: tên lấy từ tỷ lệ, có lọc thì giữ chủ bãi của các bãi đang hiện', () => {
    const lots = buildLotRows(rates, summary, { search: '', source: null });
    expect(buildOwnerRows(rates, summary, lots, { search: '', source: null }).map((o) => [o.ownerProfileId, o.ownerName])).toEqual([[7, 'Công ty A'], [8, null]]);
    // AdminService đã lọc theo search → chỉ trả bãi 1 của "Công ty A".
    const filteredRates = { ...rates, lots: { ...rates.lots, items: [rates.lots.items[0]], totalCount: 1 } };
    const filter = { search: 'cong ty a', source: null };
    const filtered = buildLotRows(filteredRates, summary, filter);
    expect(buildOwnerRows(filteredRates, summary, filtered, filter).map((o) => o.ownerProfileId)).toEqual([7]);
  });

  it('sắp xếp theo hoa hồng, tên (vi-VN)', () => {
    const rows = buildLotRows(rates, summary, { search: '', source: null });
    expect(sortLotRows(rows, 'commission').map((r) => r.parkingLotId)).toEqual([2, 9, 1]);
    expect(sortLotRows(rows, 'name').map((r) => r.lotName)).toEqual(['An Bình', 'Bãi #9', 'Bãi Đống Đa']);
  });

  it('bỏ dấu khi tìm: "Đ" → "d"', () => expect(fold('Bãi Đống Đa')).toBe('bai dong da'));

  it('định dạng tỷ lệ và CSV chống công thức', () => {
    expect(formatRate(0.0725)).toBe('7,25%');
    expect(formatRate(null)).toBe('—');
    const csv = lotCsv([{ ...buildLotRows(rates, null, { search: '', source: null })[0], lotName: '=HYPERLINK("x")' }]);
    expect(csv.split('\r\n')[1].startsWith('1,"\'=HYPERLINK(""x"")"')).toBe(true);
  });
});
