import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OwnerActivatePage } from './OwnerActivatePage';
import { PartnerApplyEditPage } from './PartnerApplyEditPage';

const h = vi.hoisted(() => ({ getForEdit: vi.fn(), resubmit: vi.fn(), validate: vi.fn(), activate: vi.fn() }));
vi.mock('@/api/partnerApplicationsApi', () => ({ getPartnerApplicationForEdit: h.getForEdit, resubmitPartnerApplication: h.resubmit }));
vi.mock('@/api/ownerActivationApi', () => ({ validateOwnerActivation: h.validate, activateOwnerAccount: h.activate }));

const apiError = (message: string, code?: string) => Object.assign(new Error(message), { status: 400, code });

const view = (patch: Record<string, unknown> = {}) => ({
  code: 'PA-2026-0012', status: 'NeedsMoreInfo', infoRequestNote: 'Chụp lại CCCD mặt sau rõ nét', editLinkExpiresAtUtc: '2026-11-27T16:59:59Z',
  fullName: 'Trần Văn Minh', email: 'minh@example.vn', phone: '0901234567', position: null, businessType: 'Company',
  businessName: 'Công ty TNHH Bãi xe Thủ Đức', taxCode: '0312345678', businessAddress: '12 Võ Văn Ngân, TP. Thủ Đức',
  legalRepresentativeName: 'Trần Văn Minh', legalRepresentativePosition: null, lotCount: 2, primaryLotAddress: '45 Kha Vạn Cân, TP. Thủ Đức',
  estimatedCarSlots: null, estimatedMotorbikeSlots: 120, operatingHours: null,
  infoRequestItems: ['taxCode', 'doc:idCardBack'], infoRequestDueAtUtc: '2026-11-27T16:59:59Z', ...patch,
});

/** Hiện state của route /login để kiểm tra email được truyền qua router state (không lên URL). */
function LoginProbe() {
  const location = useLocation();
  return <p>login:{(location.state as { email?: string } | null)?.email ?? 'none'}</p>;
}

const open = (path: string, hash: string, page: React.ReactElement) => {
  window.history.replaceState(null, '', `${path}${hash}`);
  return render(
    <MemoryRouter>
      <Routes>
        <Route path="*" element={page} />
        <Route path="/login" element={<LoginProbe />} />
      </Routes>
    </MemoryRouter>
  );
};
const input = (label: RegExp) => screen.getByLabelText(label) as HTMLInputElement;
const png = (name: string) => new File([new Uint8Array([1, 2, 3])], name, { type: 'image/png' });

beforeEach(() => Object.values(h).forEach((fn) => fn.mockReset()));
afterEach(cleanup);

describe('PartnerApplyEditPage (T4, Figma 221:157)', () => {
  it('opens only the requested items, locks the rest, and resubmits multipart fields + the requested document', async () => {
    const user = userEvent.setup();
    h.getForEdit.mockResolvedValue(view());
    h.resubmit.mockResolvedValue({ code: 'PA-2026-0012', status: 'UnderReview', submittedAtUtc: '2026-11-20T03:00:00Z' });
    open('/partner/apply/edit', '#code=PA-2026-0012&token=tok_123', <PartnerApplyEditPage />);

    expect(await screen.findByText(/Chụp lại CCCD mặt sau rõ nét/)).toBeTruthy();
    expect(screen.getByText('ParkMaster cần bạn bổ sung')).toBeTruthy();
    expect(h.getForEdit).toHaveBeenCalledWith('PA-2026-0012', 'tok_123');
    expect(window.location.hash).toBe('');   // token đã bị xóa khỏi thanh địa chỉ

    expect(input(/^Mã số thuế/).readOnly).toBe(false);
    expect(input(/^Họ và tên/).readOnly).toBe(true);
    expect(input(/^Email/).readOnly).toBe(true);
    expect(input(/^Tên doanh nghiệp/).readOnly).toBe(true);
    expect(screen.getByText('Giấy tờ cần bổ sung')).toBeTruthy();
    expect(screen.getByRole('button', { name: /CCCD người đại diện – mặt sau: kéo thả hoặc chọn file/ })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /mặt trước: kéo thả/ })).toBeNull();

    const submit = screen.getByRole('button', { name: 'Gửi bổ sung' }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);   // chưa xác nhận
    await user.click(screen.getByRole('checkbox'));
    await user.click(submit);
    expect(await screen.findByText(/Vui lòng tải lên ảnh CCCD mặt sau|Vui lòng tải lên CCCD người đại diện – mặt sau/i)).toBeTruthy();
    expect(h.resubmit).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Tải lên CCCD người đại diện – mặt sau'), { target: { files: [png('sau.png')] } });
    await user.clear(input(/^Mã số thuế/));
    await user.type(input(/^Mã số thuế/), '0399999999');
    await user.click(submit);

    await waitFor(() => expect(h.resubmit).toHaveBeenCalledTimes(1));
    const [code, token, fields, files] = h.resubmit.mock.calls[0];
    expect([code, token]).toEqual(['PA-2026-0012', 'tok_123']);
    expect(fields).toMatchObject({ taxCode: '0399999999', fullName: 'Trần Văn Minh', email: 'minh@example.vn', lotCount: '2', consentAccepted: true });
    expect(files.idCardBack).toHaveLength(1);
    expect(await screen.findByRole('heading', { name: 'Đã gửi bổ sung' })).toBeTruthy();
  });

  it('lets the applicant edit everything when the admin picked no item (legacy requests)', async () => {
    h.getForEdit.mockResolvedValue(view({ infoRequestItems: [] }));
    open('/partner/apply/edit', '#code=PA-2026-0012&token=tok', <PartnerApplyEditPage />);
    await screen.findByText('ParkMaster cần bạn bổ sung');
    expect(input(/^Họ và tên/).readOnly).toBe(false);
    expect(input(/^Email/).readOnly).toBe(true);
  });

  it('shows a clear screen for invalid, expired and incomplete links, and the server message verbatim on a 400', async () => {
    h.getForEdit.mockRejectedValue(apiError('Link bổ sung hồ sơ không hợp lệ, đã được dùng hoặc đã hết hạn.', 'edit_link_expired'));
    open('/partner/apply/edit', '#code=PA-2026-0012&token=old', <PartnerApplyEditPage />);
    expect(await screen.findByRole('heading', { name: 'Link bổ sung đã hết hạn' })).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('đã hết hạn');
    cleanup();

    open('/partner/apply/edit', '', <PartnerApplyEditPage />);
    expect(screen.getByRole('heading', { name: 'Link bổ sung không hợp lệ' })).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('không đầy đủ');
    expect(h.getForEdit).toHaveBeenCalledTimes(1);
    cleanup();

    h.getForEdit.mockResolvedValue(view({ infoRequestItems: ['lotCount'] }));
    h.resubmit.mockRejectedValue(apiError('Số bãi dự kiến phải từ 1 đến 100.'));
    const user = userEvent.setup();
    open('/partner/apply/edit', '#code=PA-2026-0012&token=tok', <PartnerApplyEditPage />);
    await screen.findByText('ParkMaster cần bạn bổ sung');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Gửi bổ sung' }));
    expect((await screen.findByRole('alert')).textContent).toContain('Số bãi dự kiến phải từ 1 đến 100.');
  });
});

describe('OwnerActivatePage (U4 / U5)', () => {
  const info = { fullName: 'Trần Văn Minh', email: 'minh@example.vn', businessName: 'Công ty TNHH Bãi xe Thủ Đức', expiresAtUtc: '2026-11-23T03:00:00Z' };

  it('checks the link, shows live password rules, enables submit only when valid, then sends the email to login via router state', async () => {
    const user = userEvent.setup();
    h.validate.mockResolvedValue(info);
    h.activate.mockResolvedValue(undefined);
    open('/owner/activate', '#token=act_456', <OwnerActivatePage />);

    expect(await screen.findByText('Công ty TNHH Bãi xe Thủ Đức')).toBeTruthy();
    expect(screen.getByText('Người đại diện: Trần Văn Minh')).toBeTruthy();
    expect(h.validate).toHaveBeenCalledWith('act_456');
    expect(window.location.hash).toBe('');
    expect(document.querySelector('meta[name="referrer"]')?.getAttribute('content')).toBe('no-referrer');
    expect(input(/^Email đăng nhập/).readOnly).toBe(true);
    expect(input(/^Email đăng nhập/).value).toBe('minh@example.vn');

    const submit = screen.getByRole('button', { name: 'Kích hoạt tài khoản' }) as HTMLButtonElement;
    const rules = () => screen.getByRole('list', { name: 'Điều kiện mật khẩu' }).textContent;
    expect(rules()).toContain('Ít nhất 8 ký tự – chưa đạt');
    await user.type(input(/^Mật khẩu \*/), 'matkhauu');
    expect(rules()).toContain('Ít nhất 8 ký tự – đã đạt');
    expect(rules()).toContain('Có chữ số – chưa đạt');
    await user.type(input(/^Mật khẩu \*/), '123');
    expect(rules()).toContain('Có chữ số – đã đạt');
    expect(submit.disabled).toBe(true);   // chưa xác nhận

    await user.type(input(/^Xác nhận mật khẩu/), 'matkhauu124');
    expect(screen.getByText('Mật khẩu khớp').closest('li')?.textContent).toContain('chưa đạt');
    expect(submit.disabled).toBe(true);
    await user.clear(input(/^Xác nhận mật khẩu/));
    await user.type(input(/^Xác nhận mật khẩu/), 'matkhauu123');
    expect(submit.disabled).toBe(false);
    await user.click(submit);

    await waitFor(() => expect(h.activate).toHaveBeenCalledWith('act_456', 'matkhauu123'));
    expect(await screen.findByRole('heading', { name: 'Tài khoản đã sẵn sàng' })).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }));
    expect(await screen.findByText('login:minh@example.vn')).toBeTruthy();
  });

  it('toggles password visibility on each field and warns when Caps Lock is on', async () => {
    const user = userEvent.setup();
    h.validate.mockResolvedValue(info);
    open('/owner/activate', '#token=t', <OwnerActivatePage />);
    await screen.findByText('Công ty TNHH Bãi xe Thủ Đức');

    expect(input(/^Mật khẩu \*/).type).toBe('password');
    expect(input(/^Mật khẩu \*/).autocomplete).toBe('new-password');
    await user.click(screen.getByRole('button', { name: 'Hiện mật khẩu' }));
    expect(input(/^Mật khẩu \*/).type).toBe('text');
    expect(input(/^Xác nhận mật khẩu/).type).toBe('password');

    fireEvent.keyDown(input(/^Mật khẩu \*/), { key: 'a', modifierCapsLock: true });
    expect(screen.getByText('Caps Lock đang bật')).toBeTruthy();
  });

  it.each([
    ['activation_expired', 'Link kích hoạt đã hết hạn'],
    ['activation_invalid', 'Link kích hoạt không hợp lệ'],
    ['activation_used', 'Tài khoản đã được kích hoạt'],
    ['already_activated', 'Tài khoản đã được kích hoạt'],
  ])('shows a dedicated screen for %s and hides the form', async (code, title) => {
    h.validate.mockRejectedValue(apiError('Link kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn. Liên hệ ParkMaster (1900 6868) để nhận link mới.', code));
    open('/owner/activate', '#token=old', <OwnerActivatePage />);

    expect(await screen.findByRole('heading', { name: title })).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('đã được dùng');
    expect(screen.queryByLabelText(/^Mật khẩu \*/)).toBeNull();
    if (code.includes('used') || code.includes('already')) expect(screen.getByRole('button', { name: 'Đăng nhập' })).toBeTruthy();
    else expect(screen.getByRole('link', { name: '1900 6868' }).getAttribute('href')).toBe('tel:19006868');
  });

  it('keeps the form and shows the server message verbatim when activation is rejected', async () => {
    const user = userEvent.setup();
    h.validate.mockResolvedValue(info);
    h.activate.mockRejectedValue(apiError('Mật khẩu không đáp ứng yêu cầu bảo mật.'));
    open('/owner/activate', '#token=act_456', <OwnerActivatePage />);

    await screen.findByText('Công ty TNHH Bãi xe Thủ Đức');
    await user.type(input(/^Mật khẩu \*/), 'matkhauu123');
    await user.type(input(/^Xác nhận mật khẩu/), 'matkhauu123');
    await user.click(screen.getByRole('button', { name: 'Kích hoạt tài khoản' }));

    expect((await screen.findByRole('alert')).textContent).toContain('Mật khẩu không đáp ứng yêu cầu bảo mật.');
    expect(input(/^Mật khẩu \*/)).toBeTruthy();
    expect(h.activate).toHaveBeenCalledTimes(1);
  });

  it('shows a hint for a link without token and never calls the API', () => {
    open('/owner/activate', '', <OwnerActivatePage />);
    expect(screen.getByRole('heading', { name: 'Link kích hoạt không hợp lệ' })).toBeTruthy();
    expect(h.validate).not.toHaveBeenCalled();
  });
});
