import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { configureStore } from '@reduxjs/toolkit';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import authReducer, { setUser } from '@/store/slices/authSlice';
import type { AuthUser } from '@/types/auth';
import { PartnerRegisterPage } from './PartnerRegisterPage';
import { DRAFT_KEY } from './partner-register/registrationDraft';
import { createEmptyForm, type RegistrationForm } from './partner-register/registrationForm';

const h = vi.hoisted(() => ({ post: vi.fn() }));
vi.mock('@/api/client', () => ({ apiClient: { post: (...args: unknown[]) => h.post(...args) } }));

const file = (name: string, type: string, size?: number) => {
  const f = new File([new Uint8Array([1, 2, 3])], name, { type });
  if (size !== undefined) Object.defineProperty(f, 'size', { value: size });
  return f;
};
const apiError = (message: string, status?: number) => Object.assign(new Error(message), { status });

const type = (label: RegExp, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });
const upload = (label: string, ...files: File[]) => fireEvent.change(screen.getByLabelText(`Tải lên ${label}`), { target: { files } });
const next = () => fireEvent.click(screen.getByRole('button', { name: 'Tiếp tục' }));
const heading = () => screen.getByRole('heading', { level: 2, name: /^Bước \d\/5/ }).textContent;

const renderPage = (user?: AuthUser) => {
  const store = configureStore({ reducer: { auth: authReducer } });
  if (user) store.dispatch(setUser(user));
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <PartnerRegisterPage />
      </MemoryRouter>
    </Provider>
  );
};

const fillApplicant = () => {
  type(/^Họ và tên/, 'Nguyễn Văn An');
  type(/^Chức vụ$/, 'Giám đốc');
  type(/^Email/, 'nguyenvanan@gmail.com');
  type(/^Số điện thoại/, '0901 234 567');
};

const fillAddress = (street: string) => {
  type(/^Tỉnh\/Thành phố/, 'TP. Hồ Chí Minh');
  type(/^Phường\/Xã/, 'Phường Bến Thành');
  type(/^Số nhà, tên đường/, street);
};

const fillBusiness = () => {
  type(/^Mã số thuế/, '0312345678001');
  type(/^Tên doanh nghiệp/, 'Công ty TNHH Bãi đỗ An Tâm');
  fillAddress('118 Nguyễn Thị Minh Khai');
  fireEvent.click(screen.getByLabelText('Người đại diện là tôi'));
};

const fillLot = () => {
  fillAddress('45 Lê Lợi');
  type(/^Mở cửa$/, '06:00');
  type(/^Đóng cửa$/, '22:00');
  type(/^Ô tô/, '24');
  type(/^Xe máy/, '120');
};

const uploadRequired = () => {
  upload('Giấy đăng ký kinh doanh', file('giay-dang-ky-kinh-doanh.pdf', 'application/pdf'));
  upload('Giấy tờ pháp lý mặt bằng', file('hop-dong-thue.jpg', 'image/jpeg'));
  upload('CCCD người đại diện – mặt trước', file('cccd-mat-truoc.jpg', 'image/jpeg'));
  upload('CCCD người đại diện – mặt sau', file('cccd-mat-sau.png', 'image/png'));
};

/** Bản nháp đủ chữ / số → mở trang ở bước 4 (file không lưu trong nháp). */
const seedDraft = (patch: Partial<RegistrationForm> = {}, savedAt = Date.now()) => {
  const form: RegistrationForm = {
    ...createEmptyForm(),
    fullName: 'Nguyễn Văn An',
    position: 'Giám đốc',
    email: 'nguyenvanan@gmail.com',
    phone: '0901234567',
    taxCode: '0312345678',
    businessName: 'Công ty TNHH Bãi đỗ An Tâm',
    businessAddress: { province: 'TP. Hồ Chí Minh', ward: 'Phường Bến Thành', street: '118 Nguyễn Thị Minh Khai' },
    representativeIsMe: true,
    lotAddress: { province: 'TP. Hồ Chí Minh', ward: 'Phường Bến Thành', street: '45 Lê Lợi' },
    ...patch,
  };
  localStorage.setItem(DRAFT_KEY, JSON.stringify({ form, step: 5, savedAt }));
};

const toReview = () => {
  seedDraft();
  renderPage();
  uploadRequired();
  next();
  fireEvent.click(screen.getByRole('checkbox', { name: /Tôi xác nhận thông tin/ }));
};

const submitButton = () => screen.getByRole('button', { name: /Gửi hồ sơ đăng ký|Thử lại/ });

beforeEach(() => {
  h.post.mockReset();
  localStorage.clear();
});
afterEach(cleanup);

describe('PartnerRegisterPage – wizard 5 bước (US-053, Figma Partner Registration)', () => {
  it('đi hết 5 bước, gửi multipart đúng tên field BE (địa chỉ / giờ / MST đã ghép) và hiện màn thành công', async () => {
    h.post.mockResolvedValue({ data: { code: 'PA-2026-0042', status: 'Submitted', submittedAtUtc: '2026-11-20T03:00:00Z' } });
    renderPage();

    expect(heading()).toBe('Bước 1/5 · Người đăng ký');
    fillApplicant();
    next();
    expect(heading()).toBe('Bước 2/5 · Doanh nghiệp / hộ kinh doanh');
    fillBusiness();
    next();
    expect(heading()).toBe('Bước 3/5 · Bãi đỗ dự kiến');
    fillLot();
    next();
    expect(heading()).toBe('Bước 4/5 · Giấy tờ pháp lý');
    uploadRequired();
    next();
    expect(heading()).toBe('Bước 5/5 · Xem lại & gửi');

    expect(screen.getByText('118 Nguyễn Thị Minh Khai, Phường Bến Thành, TP. Hồ Chí Minh')).toBeTruthy();
    expect(screen.getByText('06:00–22:00 · Không mở cửa 24/7')).toBeTruthy();
    expect(submitButton()).toHaveProperty('disabled', true);
    fireEvent.click(screen.getByRole('checkbox', { name: /Tôi xác nhận thông tin/ }));
    fireEvent.click(submitButton());

    await waitFor(() => expect(h.post).toHaveBeenCalledTimes(1));
    const [url, form] = h.post.mock.calls[0] as [string, FormData];
    expect(url).toBe('/api/v1/admin/partner-registrations');
    expect(form.get('fullName')).toBe('Nguyễn Văn An');
    expect(form.get('phone')).toBe('0901234567');
    expect(form.get('taxCode')).toBe('0312345678-001');
    expect(form.get('businessType')).toBe('Company');
    expect(form.get('businessAddress')).toBe('118 Nguyễn Thị Minh Khai, Phường Bến Thành, TP. Hồ Chí Minh');
    expect(form.get('legalRepresentativeName')).toBe('Nguyễn Văn An');
    expect(form.get('legalRepresentativePosition')).toBe('Giám đốc');
    expect(form.get('primaryLotAddress')).toBe('45 Lê Lợi, Phường Bến Thành, TP. Hồ Chí Minh');
    expect(form.get('operatingHours')).toBe('06:00-22:00');
    expect(form.get('lotCount')).toBe('1');
    expect(form.get('estimatedCarSlots')).toBe('24');
    expect(form.get('estimatedMotorbikeSlots')).toBe('120');
    expect(form.get('consentAccepted')).toBe('true');
    expect((form.get('businessLicenseFiles') as File).name).toBe('giay-dang-ky-kinh-doanh.pdf');
    expect((form.get('lotLegalProofFiles') as File).name).toBe('hop-dong-thue.jpg');
    expect((form.get('idCardFrontFiles') as File).name).toBe('cccd-mat-truoc.jpg');
    expect((form.get('idCardBackFiles') as File).name).toBe('cccd-mat-sau.png');
    expect(form.get('otherFiles')).toBeNull();

    expect(await screen.findByRole('heading', { name: 'Đã gửi hồ sơ thành công' })).toBeTruthy();
    expect(screen.getByText('PA-2026-0042')).toBeTruthy();
    expect(screen.getByText('Kết quả thẩm định và tài khoản sẽ gửi về nguyen...@gmail.com')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Về trang chủ' }).getAttribute('href')).toBe('/');
    expect(screen.queryByText('Theo dõi hồ sơ')).toBeNull();
    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
  });

  it('bấm Tiếp tục khi thiếu thông tin → summary + lỗi inline, stepper "Cần sửa", không sang bước', () => {
    renderPage();
    type(/^Họ và tên/, 'An');
    next();

    expect(heading()).toBe('Bước 1/5 · Người đăng ký');
    const summary = screen.getByRole('alert');
    expect(within(summary).getByText('Còn 2 mục cần sửa')).toBeTruthy();
    expect(screen.getByText('Vui lòng nhập email')).toBeTruthy();
    expect(screen.getByText('Vui lòng nhập số điện thoại')).toBeTruthy();
    const email = screen.getByLabelText(/^Email/);
    expect(email.getAttribute('aria-invalid')).toBe('true');
    expect(email.getAttribute('aria-describedby')).toBe('pr-email-msg');
    expect(screen.getAllByText('Cần sửa').length).toBeGreaterThan(0);

    fireEvent.click(within(summary).getByRole('link', { name: 'Số điện thoại' }));
    expect(document.activeElement).toBe(screen.getByLabelText(/^Số điện thoại/));

    type(/^Email/, 'an@gmail.com');
    type(/^Số điện thoại/, '0901234567');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('kiểm tra giống server: MST 12 số, SĐT sai, địa chỉ thiếu phần', () => {
    seedDraft({ taxCode: '031234567812', businessAddress: { province: '', ward: '', street: '' } });
    renderPage();
    fireEvent.click(screen.getByRole('button', { name: /Doanh nghiệp \/ hộ kinh doanh/ }));
    next();
    expect(screen.getByText('Mã số thuế phải gồm 10 hoặc 13 chữ số')).toBeTruthy();
    expect(screen.getByText('Vui lòng nhập đầy đủ địa chỉ trụ sở')).toBeTruthy();
  });

  it('đổi nhãn theo loại hình: Hộ kinh doanh → "Tên hộ kinh doanh", "Chủ hộ kinh doanh"', () => {
    renderPage();
    fillApplicant();
    next();
    expect(screen.getByLabelText(/^Tên doanh nghiệp/)).toBeTruthy();
    expect(screen.getByLabelText(/^Người đại diện pháp luật/)).toBeTruthy();

    fireEvent.click(screen.getByRole('radio', { name: 'Hộ kinh doanh' }));
    expect(screen.queryByLabelText(/^Tên doanh nghiệp/)).toBeNull();
    expect(screen.getByLabelText(/^Tên hộ kinh doanh/)).toBeTruthy();
    expect(screen.getByLabelText(/^Chủ hộ kinh doanh/)).toBeTruthy();

    fireEvent.click(screen.getByLabelText('Người đại diện là tôi'));
    expect((screen.getByLabelText(/^Chủ hộ kinh doanh/) as HTMLInputElement).value).toBe('Nguyễn Văn An');
    expect((screen.getByLabelText(/^Chức vụ người đại diện/) as HTMLInputElement).value).toBe('Giám đốc');
  });

  it('Mở cửa 24/7 → khoá ô giờ; số bãi dùng nút −/+ trong 1..100', () => {
    seedDraft();
    renderPage();
    fireEvent.click(screen.getByRole('button', { name: /Bãi đỗ dự kiến/ }));
    expect(heading()).toBe('Bước 3/5 · Bãi đỗ dự kiến');

    expect(screen.getByRole('button', { name: 'Giảm số bãi' })).toHaveProperty('disabled', true);
    fireEvent.click(screen.getByRole('button', { name: 'Tăng số bãi' }));
    expect((screen.getByLabelText(/^Số bãi/) as HTMLInputElement).value).toBe('2');

    fireEvent.click(screen.getByRole('switch', { name: 'Mở cửa 24/7' }));
    expect(screen.getByLabelText(/^Mở cửa$/)).toHaveProperty('disabled', true);
    expect(screen.getByLabelText(/^Đóng cửa$/)).toHaveProperty('disabled', true);
  });

  it('upload: CCCD chỉ nhận JPG/PNG và chọn lại thì thay; file > 10 MB bị chặn; thanh dung lượng; Thay file / Xóa', () => {
    seedDraft();
    renderPage();
    expect(heading()).toBe('Bước 4/5 · Giấy tờ pháp lý');

    upload('CCCD người đại diện – mặt trước', file('a.png', 'image/png'));
    upload('CCCD người đại diện – mặt trước', file('b.jpg', 'image/jpeg'));
    expect(screen.queryByText('a.png')).toBeNull();
    expect(screen.getByText('b.jpg')).toBeTruthy();

    upload('CCCD người đại diện – mặt sau', file('cccd.pdf', 'application/pdf'));
    expect(screen.getByText('Chỉ nhận ảnh JPG hoặc PNG')).toBeTruthy();

    upload('Giấy tờ khác', file('pccc.pdf', 'application/pdf', 11 * 1024 * 1024));
    expect(screen.getByText('File vượt quá 10 MB')).toBeTruthy();
    expect(screen.queryByText('pccc.pdf')).toBeNull();

    upload('Giấy đăng ký kinh doanh', file('dkkd.pdf', 'application/pdf', Math.round(2.4 * 1024 * 1024)));
    expect(screen.getByText('PDF · 2,4 MB')).toBeTruthy();
    expect(screen.getByText(/Đã dùng 2,4 \/ 25 MB · 2\/8 file/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Thay file dkkd.pdf' }));
    upload('Giấy đăng ký kinh doanh', file('dkkd-moi.pdf', 'application/pdf'));
    expect(screen.queryByText('dkkd.pdf')).toBeNull();
    expect(screen.getByText('dkkd-moi.pdf')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa dkkd-moi.pdf' }));
    expect(screen.queryByText('dkkd-moi.pdf')).toBeNull();
    expect(screen.getByRole('button', { name: 'Giấy đăng ký kinh doanh: kéo thả hoặc chọn file' })).toBeTruthy();

    next();
    expect(screen.getByText('Vui lòng tải lên giấy đăng ký kinh doanh')).toBeTruthy();
  });

  it('tối đa 8 file giấy tờ', () => {
    seedDraft();
    renderPage();
    uploadRequired();
    upload('Giấy tờ khác', ...Array.from({ length: 5 }, (_, i) => file(`khac-${i}.pdf`, 'application/pdf')));
    expect(screen.getByText(/khac-4\.pdf: Tối đa 8 file giấy tờ cho mỗi hồ sơ/)).toBeTruthy();
    expect(screen.getByText(/8\/8 file/)).toBeTruthy();
  });

  it('Xem lại: "Sửa" quay về đúng bước', () => {
    toReview();
    fireEvent.click(screen.getByRole('button', { name: 'Sửa Bãi đỗ dự kiến' }));
    expect(heading()).toBe('Bước 3/5 · Bãi đỗ dự kiến');
  });

  it('400 từ server → map về đúng ô, hiện nguyên văn, nhảy về bước có lỗi', async () => {
    h.post.mockRejectedValue(apiError('Mã số thuế phải gồm 10 số hoặc 10 số kèm -3 số chi nhánh.', 400));
    toReview();
    fireEvent.click(submitButton());

    expect(await screen.findByText('Mã số thuế phải gồm 10 số hoặc 10 số kèm -3 số chi nhánh.')).toBeTruthy();
    expect(heading()).toBe('Bước 2/5 · Doanh nghiệp / hộ kinh doanh');
    expect(screen.getByLabelText(/^Mã số thuế/).getAttribute('aria-invalid')).toBe('true');
  });

  it('400 về giấy tờ → về bước 4, lỗi nằm ở đúng ô', async () => {
    h.post.mockRejectedValue(apiError('Ảnh CCCD mặt sau: chỉ nhận ảnh JPG hoặc PNG.', 400));
    toReview();
    fireEvent.click(submitButton());
    expect(await screen.findByText('Ảnh CCCD mặt sau: chỉ nhận ảnh JPG hoặc PNG.')).toBeTruthy();
    expect(heading()).toBe('Bước 4/5 · Giấy tờ pháp lý');
  });

  it('409 → hiện nguyên văn ProblemDetails ở bước Xem lại', async () => {
    const msg = 'Đã có hồ sơ đối tác đang được xử lý với mã số thuế / email này. Vui lòng chờ Admin phản hồi hoặc gọi hotline 1900 6868.';
    h.post.mockRejectedValue(apiError(msg, 409));
    toReview();
    fireEvent.click(submitButton());
    expect(await screen.findByText(msg)).toBeTruthy();
    expect(heading()).toBe('Bước 5/5 · Xem lại & gửi');
  });

  it('429 → hiện thông báo chờ của server', async () => {
    h.post.mockRejectedValue(apiError('Bạn đã gửi hồ sơ quá nhiều lần. Vui lòng thử lại sau 10 phút.', 429));
    toReview();
    fireEvent.click(submitButton());
    expect(await screen.findByText('Bạn đã gửi hồ sơ quá nhiều lần. Vui lòng thử lại sau 10 phút.')).toBeTruthy();
  });

  it('lỗi mạng → giữ dữ liệu + giấy tờ, nút "Thử lại" gửi lại thành công', async () => {
    h.post
      .mockRejectedValueOnce(apiError('Không thể kết nối đến API Gateway (Port 5000). Vui lòng kiểm tra backend.'))
      .mockResolvedValueOnce({ data: { code: 'PA-2026-0043', status: 'Submitted', submittedAtUtc: '2026-11-20T03:00:00Z' } });
    toReview();
    fireEvent.click(submitButton());

    expect(await screen.findByText('Không thể gửi hồ sơ lúc này')).toBeTruthy();
    expect(screen.getByText('Kết nối bị gián đoạn. Thông tin và giấy tờ của Bạn vẫn được giữ lại.')).toBeTruthy();
    expect(screen.getByText(/cccd-mat-sau\.png/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByText('PA-2026-0043')).toBeTruthy();
    expect(h.post).toHaveBeenCalledTimes(2);
    expect((h.post.mock.calls[1][1] as FormData).get('idCardBackFiles')).toBeTruthy();
  });

  it('khôi phục bản nháp: toast + dữ liệu, "Bắt đầu lại" xoá nháp và về bước 1', () => {
    seedDraft();
    renderPage();
    expect(screen.getByText(/^Đã khôi phục bản nháp lúc \d{2}:\d{2}$/)).toBeTruthy();
    expect(heading()).toBe('Bước 4/5 · Giấy tờ pháp lý');

    fireEvent.click(screen.getByRole('button', { name: 'Bắt đầu lại' }));
    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
    expect(heading()).toBe('Bước 1/5 · Người đăng ký');
    expect((screen.getByLabelText(/^Họ và tên/) as HTMLInputElement).value).toBe('');
  });

  it('bản nháp quá 7 ngày bị bỏ', () => {
    seedDraft({}, Date.now() - 8 * 24 * 60 * 60 * 1000);
    renderPage();
    expect(heading()).toBe('Bước 1/5 · Người đăng ký');
    expect(screen.queryByText(/Đã khôi phục bản nháp/)).toBeNull();
    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
  });

  it('Lưu nháp chỉ lưu chữ / số, không có file', () => {
    vi.useFakeTimers();
    try {
      seedDraft();
      renderPage();
      uploadRequired();
      fireEvent.click(screen.getByRole('button', { name: 'Lưu nháp' }));
      const saved = localStorage.getItem(DRAFT_KEY) ?? '';
      expect(saved).toContain('Nguyễn Văn An');
      expect(saved).not.toContain('cccd-mat-truoc');
      expect(saved).not.toContain('Files');
      expect(screen.getAllByText(/^Đã lưu nháp lúc \d{2}:\d{2}$/).length).toBeGreaterThan(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('tự lưu nháp sau khi gõ', () => {
    vi.useFakeTimers();
    try {
      renderPage();
      type(/^Họ và tên/, 'Trần Thị Linh');
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(localStorage.getItem(DRAFT_KEY)).toContain('Trần Thị Linh');
    } finally {
      vi.useRealTimers();
    }
  });

  it('đang đăng nhập: điền sẵn từ tài khoản, email khoá và che bớt', () => {
    renderPage({ id: 7, fullName: 'Nguyễn Văn An', email: 'nguyenvanan@gmail.com', phone: '0901234567', role: 'Driver' });
    expect(screen.getByText('Bạn đang đăng ký bằng tài khoản nguyen...@gmail.com')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Không phải bạn? Đăng xuất' })).toBeTruthy();
    const email = screen.getByLabelText(/^Email/) as HTMLInputElement;
    expect(email.readOnly).toBe(true);
    expect(email.value).toBe('nguyen...@gmail.com');
    expect((screen.getByLabelText(/^Họ và tên/) as HTMLInputElement).value).toBe('Nguyễn Văn An');
    expect((screen.getByLabelText(/^Số điện thoại/) as HTMLInputElement).value).toBe('0901234567');
  });

  it('stepper đánh dấu bước hiện tại bằng aria-current', () => {
    renderPage();
    const current = screen.getAllByRole('listitem').filter((li) => li.getAttribute('aria-current') === 'step');
    expect(current.length).toBeGreaterThan(0);
    expect(current[0].textContent).toContain('Người đăng ký');
  });
});
