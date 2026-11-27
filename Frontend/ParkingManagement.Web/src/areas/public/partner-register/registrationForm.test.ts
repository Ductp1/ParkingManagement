import { describe, expect, it } from 'vitest';
import {
  checkFile,
  composeOperatingHours,
  createEmptyForm,
  DOCUMENTS,
  EMPTY_FILES,
  mapServerError,
  maskEmail,
  normalizeTaxCode,
  toSubmitFields,
  validateStep,
} from './registrationForm';
import { DRAFT_KEY, loadDraft, saveDraft } from './registrationDraft';

const file = (name: string, size = 3) => {
  const f = new File([new Uint8Array([1, 2, 3])], name);
  Object.defineProperty(f, 'size', { value: size });
  return f;
};

describe('registrationForm (US-053)', () => {
  it('MST: 13 số liền → 10-3 (BE chỉ nhận 10 hoặc 10-3)', () => {
    expect(normalizeTaxCode('0312345678001')).toBe('0312345678-001');
    expect(normalizeTaxCode('0312345678')).toBe('0312345678');
    expect(normalizeTaxCode('0312345678-001')).toBe('0312345678-001');
  });

  it('giờ hoạt động: 24/7, khoảng giờ, hoặc rỗng', () => {
    expect(composeOperatingHours({ open24h: true, openTime: '06:00', closeTime: '22:00' })).toBe('24/7');
    expect(composeOperatingHours({ open24h: false, openTime: '06:00', closeTime: '22:00' })).toBe('06:00-22:00');
    expect(composeOperatingHours({ open24h: false, openTime: '', closeTime: '' })).toBe('');
  });

  it('toSubmitFields: ghép địa chỉ, chuẩn hoá SĐT, người đại diện là tôi', () => {
    const fields = toSubmitFields({
      ...createEmptyForm(),
      fullName: ' An ',
      position: 'Giám đốc',
      phone: '0901.234-567',
      representativeIsMe: true,
      legalRepresentativeName: 'Người khác',
      businessAddress: { province: 'Hà Nội', ward: 'Phường Hàng Bạc', street: '1 Hàng Bạc' },
      open24h: true,
    });
    expect(fields.phone).toBe('0901234567');
    expect(fields.businessAddress).toBe('1 Hàng Bạc, Phường Hàng Bạc, Hà Nội');
    expect(fields.legalRepresentativeName).toBe('An');
    expect(fields.legalRepresentativePosition).toBe('Giám đốc');
    expect(fields.operatingHours).toBe('24/7');
  });

  it('chỉ nhập một trong hai giờ → báo lỗi', () => {
    const errors = validateStep(3, { ...createEmptyForm(), openTime: '06:00', lotAddress: { province: 'a', ward: 'b', street: 'c' } }, EMPTY_FILES);
    expect(errors.operatingHours).toBe('Vui lòng nhập cả giờ mở cửa và đóng cửa');
  });

  it('map message 400 của server về ô', () => {
    expect(mapServerError('Chức vụ người đại diện tối đa 100 ký tự.')).toBe('legalRepresentativePosition');
    expect(mapServerError('Chức vụ tối đa 100 ký tự.')).toBe('position');
    expect(mapServerError('Số điện thoại không hợp lệ (ví dụ 0901234567).')).toBe('phone');
    expect(mapServerError('Địa chỉ bãi chính không được để trống.')).toBe('lotAddress');
    expect(mapServerError('Vui lòng tải lên giấy tờ pháp lý mặt bằng bãi.')).toBe('doc-lotLegalProof');
    expect(mapServerError('Giấy đăng ký kinh doanh: chỉ nhận file PDF, JPG hoặc PNG.')).toBe('doc-businessLicense');
    expect(mapServerError('Tổng dung lượng giấy tờ tối đa 25 MB.')).toBe('documents');
    expect(mapServerError('Dữ liệu không hợp lệ')).toBeNull();
  });

  it('checkFile: tổng 25 MB', () => {
    const other = DOCUMENTS.find((d) => d.kind === 'other')!;
    const files = { ...EMPTY_FILES, businessLicense: [file('a.pdf', 9 * 1024 * 1024), file('b.pdf', 9 * 1024 * 1024)] };
    expect(checkFile(other, file('c.pdf', 8 * 1024 * 1024), files)).toBe('Tổng dung lượng giấy tờ tối đa 25 MB');
  });

  it('che email', () => {
    expect(maskEmail('nguyenvanan@gmail.com')).toBe('nguyen...@gmail.com');
    expect(maskEmail('an@x.vn')).toBe('a...@x.vn');
  });

  it('bản nháp: không giữ dấu xác nhận, hết hạn sau 7 ngày', () => {
    localStorage.clear();
    const now = Date.UTC(2026, 10, 20);
    saveDraft({ ...createEmptyForm(), fullName: 'An', consentAccepted: true }, 3, now);
    expect(JSON.parse(localStorage.getItem(DRAFT_KEY) ?? '{}').form.consentAccepted).toBe(false);
    expect(loadDraft(now + 1000)?.form.fullName).toBe('An');
    expect(loadDraft(now + 7 * 24 * 60 * 60 * 1000 + 1)).toBeNull();
  });
});
