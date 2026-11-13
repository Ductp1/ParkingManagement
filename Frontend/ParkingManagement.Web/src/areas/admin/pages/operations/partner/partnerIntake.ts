import type { AdminCreatePartnerApplicationRequest, PartnerBusinessType } from '@/types/adminPartnerOnboarding';

/**
 * US-116 T1 – Admin nhập hồ sơ đối tác nhận qua điện thoại. Kiểm tra phía client cùng quy tắc với
 * AdminService PartnerApplicationInput (server vẫn kiểm tra lại và trả message riêng).
 */
export interface PartnerIntakeForm {
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
  intakeNote: string;
}

export type PartnerIntakeErrors = Partial<Record<keyof PartnerIntakeForm, string>>;

export const MAX_LOTS = 100;
export const MAX_SLOTS = 10_000;
export const MAX_INTAKE_NOTE = 1000;

export const BUSINESS_TYPE_OPTIONS: Array<{ value: PartnerBusinessType; label: string }> = [
  { value: 'Company', label: 'Company' },
  { value: 'HouseholdBusiness', label: 'Household business' },
];

export const emptyIntake = (): PartnerIntakeForm => ({
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
  intakeNote: '',
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
  return n < min || n > max ? `${label} must be ${min}–${max} characters.` : null;
};

const optionalMax = (value: string, label: string, max: number) => (value.trim().length > max ? `${label} must be at most ${max} characters.` : null);

/** Ô số tùy chọn: rỗng = null; không phải số nguyên trong [0, MAX_SLOTS] → lỗi. */
const slotsError = (value: string, label: string) => {
  if (value.trim() === '') return null;
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 && n <= MAX_SLOTS ? null : `${label} must be a whole number from 0 to ${MAX_SLOTS.toLocaleString('vi-VN')}.`;
};

export const intakeErrors = (f: PartnerIntakeForm): PartnerIntakeErrors => {
  const lots = Number(f.lotCount);
  const errors: PartnerIntakeErrors = {
    fullName: lengthError(f.fullName, 'Contact name', 2, 200) ?? undefined,
    email: /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(f.email.trim()) && f.email.trim().length <= 254 ? undefined : 'Enter a valid email address.',
    phone: /^0\d{9,10}$/.test(normalizePhone(f.phone)) ? undefined : 'Enter a Vietnamese phone number, e.g. 0901 234 567.',
    position: optionalMax(f.position, 'Position', 100) ?? undefined,
    businessType: f.businessType ? undefined : 'Choose the business type.',
    businessName: lengthError(f.businessName, 'Business name', 2, 300) ?? undefined,
    taxCode: /^(\d{10}(-\d{3})?|\d{12})$/.test(normalizeTaxCode(f.taxCode))
      ? undefined
      : 'Tax code must be 10 digits, 10-3 digits (branch) or 12 digits (individual).',
    businessAddress: lengthError(f.businessAddress, 'Business address', 5, 300) ?? undefined,
    legalRepresentativeName: lengthError(f.legalRepresentativeName, 'Legal representative', 2, 200) ?? undefined,
    legalRepresentativePosition: optionalMax(f.legalRepresentativePosition, 'Position', 100) ?? undefined,
    lotCount: Number.isInteger(lots) && lots >= 1 && lots <= MAX_LOTS ? undefined : `Number of lots must be 1–${MAX_LOTS}.`,
    primaryLotAddress: lengthError(f.primaryLotAddress, 'Main lot address', 5, 300) ?? undefined,
    estimatedCarSlots: slotsError(f.estimatedCarSlots, 'Car spots') ?? undefined,
    estimatedMotorbikeSlots: slotsError(f.estimatedMotorbikeSlots, 'Motorbike spots') ?? undefined,
    operatingHours: optionalMax(f.operatingHours, 'Opening hours', 100) ?? undefined,
    consentAccepted: f.consentAccepted ? undefined : 'Confirm the partner agreed to data processing on the call.',
    intakeNote: optionalMax(f.intakeNote, 'Call note', MAX_INTAKE_NOTE) ?? undefined,
  };
  return Object.fromEntries(Object.entries(errors).filter(([, v]) => v)) as PartnerIntakeErrors;
};

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());
const numberOrNull = (value: string) => (value.trim() === '' ? null : Number(value));

/** Form đã hợp lệ → body POST /api/v1/admin/partner-applications. */
export const toCreateRequest = (f: PartnerIntakeForm): AdminCreatePartnerApplicationRequest => ({
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
  intakeNote: orNull(f.intakeNote),
});
