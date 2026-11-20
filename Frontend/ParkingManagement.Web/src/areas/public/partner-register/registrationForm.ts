import {
  PARTNER_DOCUMENT_LIMITS as LIMITS,
  type PartnerBusinessType,
  type PartnerDocumentFiles,
  type PartnerDocumentKind,
  type PartnerRegistrationFields,
} from '@/api/partnerRegistrationApi';

/**
 * US-053 – logic thuần của wizard "Đăng ký trở thành đối tác" (Figma Partner Registration 221:139…221:161).
 * Luật kiểm tra khớp SubmitPartnerApplicationUseCase + PartnerDocumentRules (AdminService): client báo lỗi sớm, server vẫn là chuẩn.
 * Figma tách địa chỉ (tỉnh / phường / số nhà) và giờ mở – đóng cửa nhưng API chỉ có một chuỗi → FE ghép khi gửi (toSubmitFields).
 */

export type StepId = 1 | 2 | 3 | 4 | 5;

export const STEPS: ReadonlyArray<{ id: StepId; title: string; description: string }> = [
  { id: 1, title: 'Người đăng ký', description: 'Cho chúng tôi biết cách liên hệ với Bạn.' },
  { id: 2, title: 'Doanh nghiệp / hộ kinh doanh', description: 'Điền thông tin pháp lý để ParkMaster thẩm định hồ sơ.' },
  { id: 3, title: 'Bãi đỗ dự kiến', description: 'Giới thiệu bãi đỗ chính mà Bạn dự kiến hợp tác.' },
  { id: 4, title: 'Giấy tờ pháp lý', description: 'Tải lên giấy tờ rõ nét và còn hiệu lực.' },
  { id: 5, title: 'Xem lại & gửi', description: 'Kiểm tra thông tin và xác nhận trước khi gửi hồ sơ.' },
];

export interface AddressParts {
  province: string;
  ward: string;
  street: string;
}

/** Chỉ gồm trường chữ / số – đây cũng là nội dung bản nháp (không có file, không có ảnh CCCD). */
export interface RegistrationForm {
  fullName: string;
  position: string;
  email: string;
  phone: string;
  businessType: PartnerBusinessType;
  taxCode: string;
  businessName: string;
  businessAddress: AddressParts;
  representativeIsMe: boolean;
  legalRepresentativeName: string;
  legalRepresentativePosition: string;
  lotCount: number;
  lotAddress: AddressParts;
  openTime: string;
  closeTime: string;
  open24h: boolean;
  estimatedCarSlots: string;
  estimatedMotorbikeSlots: string;
  consentAccepted: boolean;
}

const EMPTY_ADDRESS: AddressParts = { province: '', ward: '', street: '' };

export const createEmptyForm = (): RegistrationForm => ({
  fullName: '',
  position: '',
  email: '',
  phone: '',
  businessType: 'Company',
  taxCode: '',
  businessName: '',
  businessAddress: { ...EMPTY_ADDRESS },
  representativeIsMe: false,
  legalRepresentativeName: '',
  legalRepresentativePosition: '',
  lotCount: 1,
  lotAddress: { ...EMPTY_ADDRESS },
  openTime: '',
  closeTime: '',
  open24h: false,
  estimatedCarSlots: '',
  estimatedMotorbikeSlots: '',
  consentAccepted: false,
});

export const EMPTY_FILES: PartnerDocumentFiles = { businessLicense: [], lotLegalProof: [], idCardFront: [], idCardBack: [], other: [] };

/** Khoá lỗi = id của ô nhập (input id = `pr-${key}`), để summary nhảy đúng chỗ và gắn aria-describedby. */
export type FieldKey =
  | 'fullName'
  | 'position'
  | 'email'
  | 'phone'
  | 'businessType'
  | 'taxCode'
  | 'businessName'
  | 'businessAddress'
  | 'legalRepresentativeName'
  | 'legalRepresentativePosition'
  | 'lotCount'
  | 'lotAddress'
  | 'operatingHours'
  | 'estimatedCarSlots'
  | 'estimatedMotorbikeSlots'
  | `doc-${PartnerDocumentKind}`
  | 'documents'
  | 'consentAccepted';

export type FieldErrors = Partial<Record<FieldKey, string>>;

export const FIELD_STEP: Record<FieldKey, StepId> = {
  fullName: 1,
  position: 1,
  email: 1,
  phone: 1,
  businessType: 2,
  taxCode: 2,
  businessName: 2,
  businessAddress: 2,
  legalRepresentativeName: 2,
  legalRepresentativePosition: 2,
  lotCount: 3,
  lotAddress: 3,
  operatingHours: 3,
  estimatedCarSlots: 3,
  estimatedMotorbikeSlots: 3,
  'doc-businessLicense': 4,
  'doc-lotLegalProof': 4,
  'doc-idCardFront': 4,
  'doc-idCardBack': 4,
  'doc-other': 4,
  documents: 4,
  consentAccepted: 5,
};

/** Nhãn đổi theo loại hình (Figma 3 business / 3b household). */
export const businessLabels = (type: PartnerBusinessType) =>
  type === 'HouseholdBusiness'
    ? { name: 'Tên hộ kinh doanh', representative: 'Chủ hộ kinh doanh', typeName: 'Hộ kinh doanh' }
    : { name: 'Tên doanh nghiệp', representative: 'Người đại diện pháp luật', typeName: 'Doanh nghiệp' };

/** Nhãn dùng trong summary lỗi (Figma 7: link tới từng mục cần sửa). */
export const fieldLabel = (key: FieldKey, type: PartnerBusinessType): string => {
  const labels = businessLabels(type);
  const doc = DOCUMENTS.find((d) => `doc-${d.kind}` === key);
  if (doc) return doc.label;
  const map: Record<string, string> = {
    fullName: 'Họ và tên',
    position: 'Chức vụ',
    email: 'Email',
    phone: 'Số điện thoại',
    businessType: 'Loại hình',
    taxCode: 'Mã số thuế',
    businessName: labels.name,
    businessAddress: 'Địa chỉ trụ sở',
    legalRepresentativeName: labels.representative,
    legalRepresentativePosition: 'Chức vụ người đại diện',
    lotCount: 'Số bãi',
    lotAddress: 'Địa chỉ bãi chính',
    operatingHours: 'Giờ hoạt động',
    estimatedCarSlots: 'Ô tô',
    estimatedMotorbikeSlots: 'Xe máy',
    documents: 'Giấy tờ pháp lý',
    consentAccepted: 'Xác nhận thông tin',
  };
  return map[key] ?? key;
};

// ---------- Giấy tờ (PartnerDocumentRules) ----------

export interface DocumentSpec {
  kind: PartnerDocumentKind;
  label: string;
  hint: string;
  required: boolean;
  /** CCCD: đúng 1 ảnh JPG / PNG mỗi mặt. */
  imageOnly: boolean;
  /** Label BE dùng trong message lỗi (PartnerDocumentRules.Label) – để map lỗi 400 về đúng ô. */
  serverLabel: string;
}

export const DOCUMENTS: ReadonlyArray<DocumentSpec> = [
  { kind: 'businessLicense', label: 'Giấy đăng ký kinh doanh', hint: 'Giấy chứng nhận đăng ký doanh nghiệp hoặc hộ kinh doanh', required: true, imageOnly: false, serverLabel: 'Giấy đăng ký kinh doanh' },
  { kind: 'lotLegalProof', label: 'Giấy tờ pháp lý mặt bằng', hint: 'Sổ đỏ, hợp đồng thuê mặt bằng…', required: true, imageOnly: false, serverLabel: 'Giấy tờ pháp lý mặt bằng bãi' },
  { kind: 'idCardFront', label: 'CCCD người đại diện – mặt trước', hint: '1 ảnh JPG/PNG', required: true, imageOnly: true, serverLabel: 'Ảnh CCCD mặt trước' },
  { kind: 'idCardBack', label: 'CCCD người đại diện – mặt sau', hint: '1 ảnh JPG/PNG', required: true, imageOnly: true, serverLabel: 'Ảnh CCCD mặt sau' },
  { kind: 'other', label: 'Giấy tờ khác', hint: 'Chứng nhận PCCC, giấy phép liên quan…', required: false, imageOnly: false, serverLabel: 'Giấy tờ khác' },
];

const IMAGE_EXT = /\.(jpe?g|png)$/i;
const ANY_EXT = /\.(pdf|jpe?g|png)$/i;

export const isImageFile = (file: File) => IMAGE_EXT.test(file.name);

export const fileTypeLabel = (file: File) => (/\.pdf$/i.test(file.name) ? 'PDF' : /\.png$/i.test(file.name) ? 'PNG' : 'JPG');

/** "2,4 MB" / "320 KB" – định dạng số kiểu Việt Nam. */
export const formatBytes = (bytes: number) =>
  bytes >= 1024 * 1024
    ? `${(bytes / 1024 / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`
    : `${Math.max(1, Math.round(bytes / 1024)).toLocaleString('vi-VN')} KB`;

export const usedMegabytes = (bytes: number) => (bytes / 1024 / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 });

export const allFiles = (files: PartnerDocumentFiles) => Object.values(files).flat();

/**
 * Kiểm tra file vừa chọn cho một ô. `replacing` = file đang được thay (không tính vào giới hạn).
 * Trả về message lỗi hoặc null. Server còn kiểm tra magic bytes; client chỉ xem đuôi file.
 */
export const checkFile = (spec: DocumentSpec, file: File, files: PartnerDocumentFiles, replacing?: File): string | null => {
  if (spec.imageOnly ? !IMAGE_EXT.test(file.name) : !ANY_EXT.test(file.name))
    return spec.imageOnly ? 'Chỉ nhận ảnh JPG hoặc PNG' : 'Chỉ nhận file PDF, JPG hoặc PNG';
  if (file.size === 0) return 'File rỗng';
  if (file.size > LIMITS.maxFileBytes) return 'File vượt quá 10 MB';
  const others = allFiles(files).filter((f) => f !== replacing);
  // CCCD chọn lại = thay ảnh cũ của chính ô đó.
  const kept = spec.imageOnly ? others.filter((f) => !files[spec.kind].includes(f)) : others;
  if (kept.length + 1 > LIMITS.maxFiles) return `Tối đa ${LIMITS.maxFiles} file giấy tờ cho mỗi hồ sơ`;
  if (kept.reduce((s, f) => s + f.size, 0) + file.size > LIMITS.maxTotalBytes) return 'Tổng dung lượng giấy tờ tối đa 25 MB';
  return null;
};

// ---------- Chuẩn hoá & ghép payload ----------

const PHONE = /^(\+84|0)\d{9,10}$/;
const TAX_CODE = /^\d{10}(-?\d{3})?$/;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;

export const normalizePhone = (value: string) => value.replace(/[\s.-]/g, '');

/** Q1 (An duyệt): BE nhận `10 số` hoặc `10 số-3 số`; người dùng gõ 13 số liền thì FE chèn dấu gạch trước khi gửi. */
export const normalizeTaxCode = (value: string) => {
  const text = value.replace(/\s/g, '');
  return /^\d{13}$/.test(text) ? `${text.slice(0, 10)}-${text.slice(10)}` : text;
};

/** "118 Nguyễn Thị Minh Khai, Phường Bến Thành, TP. Hồ Chí Minh" – thứ tự số nhà → phường → tỉnh. */
export const composeAddress = (a: AddressParts) => [a.street, a.ward, a.province].map((p) => p.trim()).filter(Boolean).join(', ');

/** "24/7" hoặc "06:00-22:00"; không nhập gì thì rỗng (BE: optional, tối đa 100 ký tự). */
export const composeOperatingHours = (form: Pick<RegistrationForm, 'open24h' | 'openTime' | 'closeTime'>) =>
  form.open24h ? '24/7' : form.openTime && form.closeTime ? `${form.openTime}-${form.closeTime}` : '';

/** "Người đại diện là tôi" → lấy họ tên / chức vụ ở bước 1. */
export const representativeOf = (form: RegistrationForm) =>
  form.representativeIsMe
    ? { name: form.fullName, position: form.position }
    : { name: form.legalRepresentativeName, position: form.legalRepresentativePosition };

export const toSubmitFields = (form: RegistrationForm): PartnerRegistrationFields => {
  const rep = representativeOf(form);
  return {
    fullName: form.fullName.trim(),
    position: form.position.trim(),
    email: form.email.trim(),
    phone: normalizePhone(form.phone.trim()),
    businessType: form.businessType,
    businessName: form.businessName.trim(),
    taxCode: normalizeTaxCode(form.taxCode),
    businessAddress: composeAddress(form.businessAddress),
    legalRepresentativeName: rep.name.trim(),
    legalRepresentativePosition: rep.position.trim(),
    lotCount: String(form.lotCount),
    primaryLotAddress: composeAddress(form.lotAddress),
    estimatedCarSlots: form.estimatedCarSlots.trim(),
    estimatedMotorbikeSlots: form.estimatedMotorbikeSlots.trim(),
    operatingHours: composeOperatingHours(form),
    consentAccepted: form.consentAccepted,
  };
};

// ---------- Kiểm tra theo bước ----------

const required = (value: string, message: string, max: number, maxLabel: string): string | undefined => {
  const text = value.trim();
  if (!text) return message;
  if (text.length > max) return `${maxLabel} tối đa ${max} ký tự`;
  return undefined;
};

const optionalMax = (value: string, max: number, label: string) => (value.trim().length > max ? `${label} tối đa ${max} ký tự` : undefined);

const slots = (value: string, label: string) => {
  const text = value.trim();
  if (!text) return undefined;
  if (!/^\d+$/.test(text) || Number(text) > 10000) return `${label} phải từ 0 đến 10.000`;
  return undefined;
};

const address = (a: AddressParts, message: string, label: string) => {
  if (!a.province.trim() || !a.ward.trim() || !a.street.trim()) return message;
  if (composeAddress(a).length > 500) return `${label} tối đa 500 ký tự`;
  return undefined;
};

const clean = (errors: FieldErrors): FieldErrors =>
  Object.fromEntries(Object.entries(errors).filter(([, v]) => Boolean(v))) as FieldErrors;

export const validateStep = (step: StepId, form: RegistrationForm, files: PartnerDocumentFiles): FieldErrors => {
  const labels = businessLabels(form.businessType);
  switch (step) {
    case 1:
      return clean({
        fullName: required(form.fullName, 'Vui lòng nhập họ và tên', 200, 'Họ và tên'),
        position: optionalMax(form.position, 100, 'Chức vụ'),
        email: !form.email.trim() ? 'Vui lòng nhập email' : !EMAIL.test(form.email.trim()) || form.email.trim().length > 254 ? 'Email không hợp lệ' : undefined,
        phone: !form.phone.trim()
          ? 'Vui lòng nhập số điện thoại'
          : !PHONE.test(normalizePhone(form.phone.trim()))
            ? 'Số điện thoại không hợp lệ (ví dụ 0901234567)'
            : undefined,
      });
    case 2: {
      const rep = representativeOf(form);
      return clean({
        taxCode: !form.taxCode.trim()
          ? 'Vui lòng nhập mã số thuế'
          : !TAX_CODE.test(form.taxCode.replace(/\s/g, ''))
            ? 'Mã số thuế phải gồm 10 hoặc 13 chữ số'
            : undefined,
        businessName: required(form.businessName, `Vui lòng nhập ${labels.name.toLowerCase()}`, 200, labels.name),
        businessAddress: address(form.businessAddress, 'Vui lòng nhập đầy đủ địa chỉ trụ sở', 'Địa chỉ trụ sở'),
        legalRepresentativeName: required(rep.name, `Vui lòng nhập ${labels.representative.toLowerCase()}`, 200, labels.representative),
        legalRepresentativePosition: optionalMax(rep.position, 100, 'Chức vụ người đại diện'),
      });
    }
    case 3:
      return clean({
        lotCount: !Number.isInteger(form.lotCount) || form.lotCount < 1 || form.lotCount > 100 ? 'Số bãi dự kiến phải từ 1 đến 100' : undefined,
        lotAddress: address(form.lotAddress, 'Vui lòng nhập đầy đủ địa chỉ bãi chính', 'Địa chỉ bãi chính'),
        operatingHours:
          form.open24h || (!form.openTime && !form.closeTime)
            ? undefined
            : !TIME.test(form.openTime) || !TIME.test(form.closeTime)
              ? 'Vui lòng nhập cả giờ mở cửa và đóng cửa'
              : undefined,
        estimatedCarSlots: slots(form.estimatedCarSlots, 'Số chỗ ô tô'),
        estimatedMotorbikeSlots: slots(form.estimatedMotorbikeSlots, 'Số chỗ xe máy'),
      });
    case 4: {
      const errors: FieldErrors = {};
      DOCUMENTS.forEach((d) => {
        if (d.required && files[d.kind].length === 0) errors[`doc-${d.kind}`] = `Vui lòng tải lên ${d.label.charAt(0).toLowerCase()}${d.label.slice(1)}`;
      });
      return errors;
    }
    case 5:
      return clean({ consentAccepted: form.consentAccepted ? undefined : 'Vui lòng xác nhận thông tin để gửi hồ sơ' });
  }
};

export const validateAll = (form: RegistrationForm, files: PartnerDocumentFiles): FieldErrors =>
  ([1, 2, 3, 4, 5] as StepId[]).reduce<FieldErrors>((acc, s) => ({ ...acc, ...validateStep(s, form, files) }), {});

// ---------- Lỗi 400 của server → ô nhập ----------

/**
 * Server ném ValidationException với một `detail` (không có key field) → map theo tiền tố message của
 * SubmitPartnerApplicationUseCase / PartnerDocumentRules. Thứ tự quan trọng ("Chức vụ người đại diện" trước "Chức vụ").
 * Message vẫn hiện nguyên văn; không map được thì hiện ở đầu bước "Xem lại & gửi".
 */
const SERVER_PREFIXES: ReadonlyArray<[string, FieldKey]> = [
  ['Họ tên người đăng ký', 'fullName'],
  ['Chức vụ người đại diện', 'legalRepresentativePosition'],
  ['Chức vụ', 'position'],
  ['Email', 'email'],
  ['Số điện thoại', 'phone'],
  ['Mã số thuế', 'taxCode'],
  ['Vui lòng chọn loại hình', 'businessType'],
  ['Tên doanh nghiệp', 'businessName'],
  ['Địa chỉ trụ sở', 'businessAddress'],
  ['Người đại diện pháp luật', 'legalRepresentativeName'],
  ['Số bãi dự kiến', 'lotCount'],
  ['Địa chỉ bãi chính', 'lotAddress'],
  ['Giờ hoạt động', 'operatingHours'],
  ['Số chỗ ô tô', 'estimatedCarSlots'],
  ['Số chỗ xe máy', 'estimatedMotorbikeSlots'],
  ['Vui lòng đồng ý', 'consentAccepted'],
  ['Tối đa', 'documents'],
  ['Tổng dung lượng', 'documents'],
];

export const mapServerError = (message: string): FieldKey | null => {
  const text = message.trim();
  const lower = text.toLowerCase();
  for (const d of DOCUMENTS) {
    const label = d.serverLabel.toLowerCase();
    if (lower.startsWith(`${label}:`) || lower === `vui lòng tải lên ${label}.`) return `doc-${d.kind}`;
  }
  const hit = SERVER_PREFIXES.find(([prefix]) => text.startsWith(prefix));
  return hit ? hit[1] : null;
};

/** "nguyen...@gmail.com" – che bớt email khi hiển thị (Figma 2 signed-in, 10 success). */
export const maskEmail = (email: string) => {
  const [local, domain] = email.split('@');
  if (!domain) return email;
  const keep = local.length <= 2 ? 1 : Math.min(6, Math.ceil(local.length / 2));
  return `${local.slice(0, keep)}...@${domain}`;
};
