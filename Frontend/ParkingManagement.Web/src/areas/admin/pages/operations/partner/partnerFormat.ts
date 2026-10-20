import type { AdminFile, ContractDraftDto } from '@/types/adminPartnerOnboarding';
import { formatDate, formatNumber } from '../../../utils/format';

export const PARTNER_BASE = '/admin/operations/partner-onboarding';

/** "8.5%" – commissionRate trong hợp đồng tính theo %. */
export const percentText = (rate: number) => `${formatNumber(rate)}%`;

/** "01/11/2026 – 31/10/2027 (12 months)". */
export const termText = (draft: Pick<ContractDraftDto, 'effectiveFrom' | 'effectiveTo' | 'termMonths'>) =>
  `${formatDate(draft.effectiveFrom)} – ${formatDate(draft.effectiveTo)} (${draft.termMonths} months)`;

/** "Vietcombank (970436) · 0011000000099 · CONG TY …". */
export const bankText = (draft: Pick<ContractDraftDto, 'bankName' | 'bankBin' | 'bankAccountNumber' | 'bankAccountName'>) =>
  `${draft.bankName} (${draft.bankBin}) · ${draft.bankAccountNumber} · ${draft.bankAccountName}`;

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
export const termsErrors = (v: {
  rate: string;
  effectiveFrom: string;
  termMonths: string;
  bin: string;
  bankName: string;
  accountNumber: string;
  accountName: string;
}) => {
  const rate = Number(v.rate);
  return {
    rate:
      v.rate.trim() === ''
        ? 'Commission rate is required.'
        : !Number.isFinite(rate) || rate <= 0 || rate > 100 || Math.round(rate * 100) !== rate * 100
          ? 'Use a rate above 0 and up to 100, with at most 2 decimals.'
          : null,
    effectiveFrom: v.effectiveFrom ? null : 'Effective date is required.',
    termMonths: v.termMonths ? null : 'Choose a term.',
    bin: /^\d{6}$/.test(v.bin.trim()) ? null : 'Bank BIN must be 6 digits.',
    bankName: v.bankName.trim() ? null : 'Bank name is required.',
    accountNumber: /^\d{6,30}$/.test(v.accountNumber.trim()) ? null : 'Account number must be 6–30 digits.',
    accountName: v.accountName.trim() ? null : 'Account name is required.',
  };
};
