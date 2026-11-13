import type { PartnerApplicationRequest, PartnerBusinessType } from '@/api/partnerApplicationsApi';

/**
 * Form "Đăng ký đối tác" (US-116 T1 / US-053). Kiểm tra phía client cùng quy tắc với AdminService PartnerApplicationInput
 * (server vẫn kiểm tra lại). Cùng quy tắc với dialog admin (areas/admin/.../partnerIntake.ts) nhưng thông báo tiếng Việt cho chủ bãi.
 */
export interface PartnerApplyForm {
  fullName: string;
  email: string;
  phone: string;
  position: string;
  businessType: PartnerBusinessType | '';
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string;
  lotCount: string;
  primaryLotAddress: string;
  estimatedCarSlots: string;
  estimatedMotorbikeSlots: string;
  operatingHours: string;
  consentAccepted: boolean;
}

export type PartnerApplyField = keyof PartnerApplyForm;
export type PartnerApplyErrors = Partial<Record<PartnerApplyField, string>>;

export const MAX_LOTS = 100;
export const MAX_SLOTS = 10_000;

/** 4 bước của form; bước cuối là xem lại + đồng ý. */
export const STEPS: Array<{ label: string; fields: PartnerApplyField[] }> = [
  { label: 'Liên hệ', fields: ['fullName', 'position', 'email', 'phone'] },
  {
    label: 'Doanh nghiệp',
    fields: ['businessType', 'businessName', 'taxCode', 'businessAddress', 'legalRepresentativeName', 'legalRepresentativePosition'],
  },
  // Chỉ để ParkMaster đánh giá sơ bộ – KHÔNG tạo bãi. Bãi thật khai báo sau khi có tài khoản (Owner portal → KYB → hợp đồng từng bãi).
  { label: 'Quy mô dự kiến', fields: ['lotCount', 'primaryLotAddress', 'estimatedCarSlots', 'estimatedMotorbikeSlots'] },
  { label: 'Xác nhận', fields: ['consentAccepted'] },
];

export const BUSINESS_TYPES: Array<{ value: PartnerBusinessType; label: string }> = [
  { value: 'Company', label: 'Doanh nghiệp' },
  { value: 'HouseholdBusiness', label: 'Hộ kinh doanh' },
];

export const emptyForm = (): PartnerApplyForm => ({
  fullName: '',
  email: '',
  phone: '',
  position: '',
  businessType: '',
  businessName: '',
  taxCode: '',
  businessAddress: '',
  legalRepresentativeName: '',
  legalRepresentativePosition: '',
  lotCount: '1',
  primaryLotAddress: '',
  estimatedCarSlots: '',
  estimatedMotorbikeSlots: '',
  operatingHours: '',
  consentAccepted: false,
});

/** "+84 901.234.567" → "0901234567" (giống server). */
export const normalizePhone = (phone: string): string => {
  let digits = phone.replace(/[^\d+]/g, '');
  if (digits.startsWith('+84')) digits = `0${digits.slice(3)}`;
  else if (digits.startsWith('84') && digits.length === 11) digits = `0${digits.slice(2)}`;
  return digits.replace(/\+/g, '');
};

const normalizeTaxCode = (taxCode: string) => taxCode.replace(/\s/g, '');

const lengthError = (value: string, label: string, min: number, max: number) => {
  const n = value.trim().length;
  if (n === 0) return `Vui lòng nhập ${label.toLowerCase()}.`;
  return n < min || n > max ? `${label} phải dài từ ${min} đến ${max} ký tự.` : undefined;
};

const optionalMax = (value: string, label: string, max: number) => (value.trim().length > max ? `${label} tối đa ${max} ký tự.` : undefined);

const slotsError = (value: string, label: string) => {
  if (value.trim() === '') return undefined;
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 && n <= MAX_SLOTS ? undefined : `${label} phải là số nguyên từ 0 đến ${MAX_SLOTS.toLocaleString('vi-VN')}.`;
};

export const validate = (f: PartnerApplyForm): PartnerApplyErrors => {
  const lots = Number(f.lotCount);
  const errors: PartnerApplyErrors = {
    fullName: lengthError(f.fullName, 'Họ và tên', 2, 200),
    position: optionalMax(f.position, 'Chức vụ', 100),
    email: /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(f.email.trim()) && f.email.trim().length <= 254 ? undefined : 'Email chưa đúng định dạng.',
    phone: /^0\d{9,10}$/.test(normalizePhone(f.phone)) ? undefined : 'Số điện thoại chưa đúng (ví dụ 0901 234 567).',
    businessType: f.businessType ? undefined : 'Vui lòng chọn loại hình kinh doanh.',
    businessName: lengthError(f.businessName, 'Tên doanh nghiệp / hộ kinh doanh', 2, 300),
    taxCode: /^(\d{10}(-\d{3})?|\d{12})$/.test(normalizeTaxCode(f.taxCode))
      ? undefined
      : 'Mã số thuế gồm 10 số, 10-3 số (chi nhánh) hoặc 12 số (cá nhân).',
    businessAddress: lengthError(f.businessAddress, 'Địa chỉ kinh doanh', 5, 300),
    legalRepresentativeName: lengthError(f.legalRepresentativeName, 'Người đại diện theo pháp luật', 2, 200),
    legalRepresentativePosition: optionalMax(f.legalRepresentativePosition, 'Chức vụ người đại diện', 100),
    lotCount: Number.isInteger(lots) && lots >= 1 && lots <= MAX_LOTS ? undefined : `Số bãi dự kiến từ 1 đến ${MAX_LOTS}.`,
    primaryLotAddress: lengthError(f.primaryLotAddress, 'Khu vực / địa chỉ bãi chính', 5, 300),
    estimatedCarSlots: slotsError(f.estimatedCarSlots, 'Số chỗ ô tô'),
    estimatedMotorbikeSlots: slotsError(f.estimatedMotorbikeSlots, 'Số chỗ xe máy'),
    operatingHours: optionalMax(f.operatingHours, 'Giờ hoạt động', 100),
    consentAccepted: f.consentAccepted ? undefined : 'Vui lòng đồng ý để ParkMaster xử lý thông tin đăng ký.',
  };
  return Object.fromEntries(Object.entries(errors).filter(([, v]) => v)) as PartnerApplyErrors;
};

/** Lỗi của một bước (để chặn "Tiếp tục"). */
export const stepErrors = (f: PartnerApplyForm, step: number): PartnerApplyErrors => {
  const all = validate(f);
  return Object.fromEntries(STEPS[step].fields.filter((k) => all[k]).map((k) => [k, all[k]])) as PartnerApplyErrors;
};

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());
const numberOrNull = (value: string) => (value.trim() === '' ? null : Number(value));

export const toRequest = (f: PartnerApplyForm): PartnerApplicationRequest => ({
  fullName: f.fullName.trim(),
  email: f.email.trim().toLowerCase(),
  phone: normalizePhone(f.phone),
  position: orNull(f.position),
  businessType: f.businessType as PartnerBusinessType,
  businessName: f.businessName.trim(),
  taxCode: normalizeTaxCode(f.taxCode),
  businessAddress: f.businessAddress.trim(),
  legalRepresentativeName: f.legalRepresentativeName.trim(),
  legalRepresentativePosition: orNull(f.legalRepresentativePosition),
  lotCount: Number(f.lotCount),
  primaryLotAddress: f.primaryLotAddress.trim(),
  estimatedCarSlots: numberOrNull(f.estimatedCarSlots),
  estimatedMotorbikeSlots: numberOrNull(f.estimatedMotorbikeSlots),
  operatingHours: orNull(f.operatingHours),
  consentAccepted: f.consentAccepted,
});

/** Hồ sơ đọc từ link bổ sung → form (ô số để chuỗi, ô trống = ""); phải đồng ý lại trước khi gửi. */
export const fromEditView = (v: {
  fullName: string; email: string; phone: string; position: string | null; businessType: string; businessName: string; taxCode: string;
  businessAddress: string; legalRepresentativeName: string; legalRepresentativePosition: string | null; lotCount: number;
  primaryLotAddress: string; estimatedCarSlots: number | null; estimatedMotorbikeSlots: number | null; operatingHours: string | null;
}): PartnerApplyForm => ({
  fullName: v.fullName,
  email: v.email,
  phone: v.phone,
  position: v.position ?? '',
  businessType: v.businessType === 'Company' || v.businessType === 'HouseholdBusiness' ? v.businessType : '',
  businessName: v.businessName,
  taxCode: v.taxCode,
  businessAddress: v.businessAddress,
  legalRepresentativeName: v.legalRepresentativeName,
  legalRepresentativePosition: v.legalRepresentativePosition ?? '',
  lotCount: String(v.lotCount),
  primaryLotAddress: v.primaryLotAddress,
  estimatedCarSlots: v.estimatedCarSlots === null ? '' : String(v.estimatedCarSlots),
  estimatedMotorbikeSlots: v.estimatedMotorbikeSlots === null ? '' : String(v.estimatedMotorbikeSlots),
  operatingHours: v.operatingHours ?? '',
  consentAccepted: false,
});

/**
 * Đọc tham số sau "#" của link trong email / tin nhắn (token không nằm trong query string nên không lên server, log hay Referer),
 * rồi xóa khỏi thanh địa chỉ để token không ở lại lịch sử trình duyệt.
 */
export const takeHashParams = (): URLSearchParams => {
  const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
  if (window.location.hash) window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search);
  return params;
};

/** Điều kiện mật khẩu khi kích hoạt tài khoản chủ bãi – cùng quy tắc với đăng ký và UserService (IdentityRules.Password). */
export const passwordProblems = (password: string) => [
  { label: 'Ít nhất 8 ký tự', met: password.length >= 8 && new TextEncoder().encode(password).length <= 72 },
  { label: 'Có chữ cái', met: /\p{L}/u.test(password) },
  { label: 'Có chữ số', met: /\p{Nd}/u.test(password) },
];
