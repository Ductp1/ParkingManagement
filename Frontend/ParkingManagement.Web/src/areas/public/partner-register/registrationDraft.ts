import { createEmptyForm, type RegistrationForm, type StepId } from './registrationForm';

/**
 * Bản nháp đăng ký đối tác (Figma 13 draft restored): chỉ lưu trường chữ / số của RegistrationForm + bước đang làm.
 * KHÔNG lưu file giấy tờ hay ảnh CCCD. Hết hạn sau 7 ngày, xoá khi gửi thành công hoặc bấm "Bắt đầu lại".
 * localStorage có thể bị chặn (chế độ riêng tư) → mọi thao tác đều nuốt lỗi, trang vẫn chạy bình thường.
 */
export const DRAFT_KEY = 'parkmaster.partnerRegistration.draft.v1';
export const DRAFT_TTL_MS = 7 * 24 * 60 * 60 * 1000;

export interface RegistrationDraft {
  form: RegistrationForm;
  step: StepId;
  savedAt: number;
}

export const saveDraft = (form: RegistrationForm, step: StepId, now = Date.now()): number | null => {
  try {
    // Không giữ dấu xác nhận: người dùng phải tick lại sau khi xem lại.
    const draft: RegistrationDraft = { form: { ...form, consentAccepted: false }, step, savedAt: now };
    localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
    return now;
  } catch {
    return null;
  }
};

export const clearDraft = () => {
  try {
    localStorage.removeItem(DRAFT_KEY);
  } catch {
    /* localStorage bị chặn – không có gì để xoá */
  }
};

export const loadDraft = (now = Date.now()): RegistrationDraft | null => {
  try {
    const raw = localStorage.getItem(DRAFT_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<RegistrationDraft>;
    if (typeof parsed.savedAt !== 'number' || now - parsed.savedAt > DRAFT_TTL_MS || !parsed.form) {
      clearDraft();
      return null;
    }
    const empty = createEmptyForm();
    const form: RegistrationForm = {
      ...empty,
      ...parsed.form,
      businessAddress: { ...empty.businessAddress, ...parsed.form.businessAddress },
      lotAddress: { ...empty.lotAddress, ...parsed.form.lotAddress },
      consentAccepted: false,
    };
    // File không được lưu → khôi phục tối đa về bước "Giấy tờ pháp lý" để chọn lại giấy tờ.
    const step = Math.min(Math.max(Number(parsed.step) || 1, 1), 4) as StepId;
    return { form, step, savedAt: parsed.savedAt };
  } catch {
    clearDraft();
    return null;
  }
};

export const formatClock = (timestamp: number) =>
  new Date(timestamp).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false });
