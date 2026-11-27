import { EMPTY, formatNumber } from '../../../utils/format';
import { SOURCE_LABEL, type CommissionLotRow, type CommissionOwnerRow, type CommissionSourceRow } from './commissionRows';

/** Tỷ lệ phân số → "7,5%" / "7,25%" (tối đa 2 chữ số thập phân như hợp đồng). */
const rateFmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
export const formatRate = (fraction: number | null | undefined): string =>
  fraction === null || fraction === undefined ? EMPTY : `${rateFmt.format(fraction * 100)}%`;

/** Link tra hợp đồng của bãi (trang Lot Contracts lọc theo số hợp đồng). */
export const lotContractLink = (contractNumber: string) => `/admin/operations/lot-contracts?q=${encodeURIComponent(contractNumber)}`;

/**
 * Link chủ bãi: trang chi tiết đối tác dùng id hồ sơ đối tác, AdminService chưa trả id này theo OwnerProfileId →
 * mở danh sách Partner Onboarding lọc theo tên doanh nghiệp (không có tên thì không có link).
 */
export const partnerLink = (ownerName: string | null) =>
  ownerName ? `/admin/operations/partner-onboarding?q=${encodeURIComponent(ownerName)}` : null;

// ===== CSV (phía client, chỉ số tổng hợp – không có dữ liệu từng khách) =====

const cell = (value: string | number | null | undefined): string => {
  if (value === null || value === undefined) return '';
  let text = String(value);
  // Chống CSV injection như US-078 / US-100.
  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`;
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};
const money = (v: number | null | undefined) => (v === null || v === undefined ? '' : String(Math.round(v)));
/** Tỷ lệ dạng phân số (máy đọc), ví dụ 0.075. */
const fraction = (v: number | null | undefined) => (v === null || v === undefined ? '' : String(v));

export const toCsv = (header: string[], rows: Array<Array<string | number | null | undefined>>): string =>
  [header, ...rows].map((r) => r.map(cell).join(',')).join('\r\n') + '\r\n';

export const lotCsv = (rows: CommissionLotRow[]) =>
  toCsv(
    ['ParkingLotId', 'Lot', 'District', 'OwnerProfileId', 'Owner', 'CurrentRate', 'RateSource', 'RateReason', 'ContractNumber', 'ContractVersion',
      'Gross', 'Refunded', 'Net', 'Commission', 'EffectiveRate', 'Transactions', 'Pending'],
    rows.map((r) => [
      r.parkingLotId, r.lotName, r.district, r.ownerProfileId, r.ownerName, fraction(r.currentRate), r.source, r.reason, r.contractNumber,
      r.contractVersion, money(r.money?.gross), money(r.money?.refunded), money(r.money?.net), money(r.money?.commission),
      fraction(r.money?.effectiveRate), r.money?.transactionCount, r.money?.pendingCount,
    ])
  );

export const ownerCsv = (rows: CommissionOwnerRow[]) =>
  toCsv(
    ['OwnerProfileId', 'Owner', 'Lots', 'Gross', 'Refunded', 'Net', 'Commission', 'EffectiveRate', 'Transactions', 'Pending'],
    rows.map((r) => [
      r.ownerProfileId, r.ownerName, r.lotCount, money(r.money?.gross), money(r.money?.refunded), money(r.money?.net),
      money(r.money?.commission), fraction(r.money?.effectiveRate), r.money?.transactionCount, r.money?.pendingCount,
    ])
  );

export const sourceCsv = (rows: CommissionSourceRow[]) =>
  toCsv(
    ['RateSource', 'Label', 'LotsNow', 'Transactions', 'Base', 'Refunded', 'Commission', 'EffectiveRate'],
    rows.map((r) => [
      r.source, SOURCE_LABEL[r.source], r.lotCount, r.stats?.transactionCount, money(r.stats?.baseAmount), money(r.stats?.refundedAmount),
      money(r.stats?.commissionAmount), fraction(r.stats?.effectiveRate),
    ])
  );

/** Tải CSV xuống máy: UTF-8 có BOM để Excel đọc đúng tiếng Việt. */
export const downloadCsv = (fileName: string, csv: string) => {
  const blob = new Blob(['﻿', csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

export const pendingText = (count: number | null | undefined) =>
  count === null || count === undefined ? EMPTY : count > 0 ? `${formatNumber(count)} chờ tính` : '0';
