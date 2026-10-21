import type { LotContractVersionDto } from '@/types/adminLotContracts';
import type { AdminFile, PartnerBankAccountDto } from '@/types/adminPartnerOnboarding';
import { formatDate, formatNumber } from '../../../utils/format';

/** "7.5%" – phí nền tảng theo %. */
export const percentText = (rate: number | null) => (rate === null ? '—' : `${formatNumber(rate)}%`);

/** "01/11/2026 – 31/10/2027 (12 months)". */
export const termText = (v: Pick<LotContractVersionDto, 'effectiveFrom' | 'effectiveTo' | 'termMonths'>) =>
  `${formatDate(v.effectiveFrom)} – ${formatDate(v.effectiveTo)} (${v.termMonths} months)`;

/** "Vietcombank (970436) · 0011000000099 · CONG TY …". */
export const bankText = (b: PartnerBankAccountDto) => `${b.name} (${b.bin}) · ${b.accountNumber} · ${b.accountName}`;

/** Lưu file nhận từ API xuống máy (không mở trong tab để HTML hợp đồng không chạy trong origin của portal). */
export const saveFile = (file: AdminFile, fallbackName: string) => {
  const url = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = file.fileName ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

/** Kiểm tra điều khoản phía client, cùng quy tắc ContractTerms của AdminService (server vẫn kiểm tra lại). */
export const termsErrors = (v: { rate: string; effectiveFrom: string; termMonths: string }) => {
  const rate = Number(v.rate);
  return {
    rate:
      v.rate.trim() === ''
        ? 'Platform fee is required.'
        : !Number.isFinite(rate) || rate <= 0 || rate > 100 || Math.round(rate * 100) !== rate * 100
          ? 'Use a rate above 0 and up to 100, with at most 2 decimals.'
          : null,
    effectiveFrom: v.effectiveFrom ? null : 'Effective date is required.',
    termMonths: v.termMonths ? null : 'Choose a term.',
  };
};
