export const PARTNER_BASE = '/admin/operations/partner-onboarding';

/**
 * Link bổ sung hồ sơ gửi cho đối tác (T3/T4): trang public /partner/apply/edit, mã + token nằm sau "#" nên không lên server / log
 * truy cập; trang đọc fragment rồi gửi token qua header X-Edit-Token.
 */
export const partnerEditUrl = (origin: string, link: { code: string; token: string }) =>
  `${origin}/partner/apply/edit#code=${encodeURIComponent(link.code)}&token=${encodeURIComponent(link.token)}`;

/** Kiểm tra tài khoản nhận thanh toán phía client, cùng quy tắc BankAccount của AdminService (server vẫn kiểm tra lại). */
export const bankErrors = (v: { bin: string; bankName: string; accountNumber: string; accountName: string }) => ({
  bin: /^\d{6}$/.test(v.bin.trim()) ? null : 'Bank BIN must be 6 digits.',
  bankName: v.bankName.trim() ? null : 'Bank name is required.',
  accountNumber: /^\d{6,30}$/.test(v.accountNumber.trim()) ? null : 'Account number must be 6–30 digits.',
  accountName: v.accountName.trim() ? null : 'Account name is required.',
});
