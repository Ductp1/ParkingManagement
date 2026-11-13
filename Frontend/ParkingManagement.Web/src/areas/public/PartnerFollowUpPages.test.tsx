import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OwnerActivatePage } from './OwnerActivatePage';
import { PartnerApplyEditPage } from './PartnerApplyEditPage';
import { fromEditView } from './partnerApplyForm';

const h = vi.hoisted(() => ({ getForEdit: vi.fn(), resubmit: vi.fn(), submit: vi.fn(), validate: vi.fn(), activate: vi.fn() }));
vi.mock('@/api/partnerApplicationsApi', () => ({
  getPartnerApplicationForEdit: h.getForEdit,
  resubmitPartnerApplication: h.resubmit,
  submitPartnerApplication: h.submit,
}));
vi.mock('@/api/ownerActivationApi', () => ({ validateOwnerActivation: h.validate, activateOwnerAccount: h.activate }));
vi.mock('@/components/auth/AuthLayout', () => ({ AuthLayout: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));

const view = () => ({
  code: 'PA-2026-0012', status: 'NeedsMoreInfo', infoRequestNote: 'Bổ sung số chỗ ô tô', editLinkExpiresAtUtc: '2026-11-20T02:00:00Z',
  fullName: 'Trần Văn Minh', email: 'minh@example.vn', phone: '0901234567', position: null, businessType: 'Company',
  businessName: 'Công ty TNHH Bãi xe Thủ Đức', taxCode: '0312345678', businessAddress: '12 Võ Văn Ngân, TP. Thủ Đức',
  legalRepresentativeName: 'Trần Văn Minh', legalRepresentativePosition: null, lotCount: 2, primaryLotAddress: '45 Kha Vạn Cân, TP. Thủ Đức',
  estimatedCarSlots: null, estimatedMotorbikeSlots: 120, operatingHours: null,
});

const open = (path: string, hash: string, page: React.ReactElement) => {
  window.history.replaceState(null, '', `${path}${hash}`);
  return render(<MemoryRouter>{page}</MemoryRouter>);
};
const byId = (id: string) => document.getElementById(id) as HTMLInputElement;
const next = () => screen.getByRole('button', { name: /Tiếp tục/ });

beforeEach(() => Object.values(h).forEach((fn) => fn.mockReset()));
afterEach(cleanup);

describe('PartnerApplyEditPage (T4)', () => {
  it('maps the stored application into the form and requires consent again', () => {
    expect(fromEditView(view())).toMatchObject({ lotCount: '2', estimatedCarSlots: '', estimatedMotorbikeSlots: '120', position: '', consentAccepted: false });
  });

  it('loads with the link, shows the request, and resubmits with the same code and token', async () => {
    const user = userEvent.setup();
    h.getForEdit.mockResolvedValue(view());
    h.resubmit.mockResolvedValue({ code: 'PA-2026-0012', status: 'UnderReview', submittedAtUtc: '2026-11-13T03:00:00Z' });
    open('/partner/apply/edit', '#code=PA-2026-0012&token=tok_123', <PartnerApplyEditPage />);

    expect(await screen.findByText('Bổ sung số chỗ ô tô')).toBeTruthy();
    expect(h.getForEdit).toHaveBeenCalledWith('PA-2026-0012', 'tok_123');
    expect(window.location.hash).toBe('');
    expect(byId('partner-fullName').value).toBe('Trần Văn Minh');

    await user.click(next());
    await user.click(next());
    await user.type(byId('partner-estimatedCarSlots'), '40');
    await user.click(next());
    await user.click(byId('partner-consentAccepted'));
    await user.click(screen.getByRole('button', { name: /Gửi bổ sung/ }));

    await waitFor(() => expect(h.resubmit).toHaveBeenCalledTimes(1));
    expect(h.resubmit.mock.calls[0][0]).toBe('PA-2026-0012');
    expect(h.resubmit.mock.calls[0][1]).toBe('tok_123');
    expect(h.resubmit.mock.calls[0][2]).toMatchObject({ estimatedCarSlots: 40, lotCount: 2, consentAccepted: true });
    expect(await screen.findByRole('heading', { name: 'Đã gửi bổ sung' })).toBeTruthy();
  });

  it('shows the server message for a used or expired link and a hint for an incomplete link', async () => {
    h.getForEdit.mockRejectedValue(new Error('Link bổ sung hồ sơ không hợp lệ, đã được dùng hoặc đã hết hạn.'));
    open('/partner/apply/edit', '#code=PA-2026-0012&token=old', <PartnerApplyEditPage />);
    expect((await screen.findByRole('alert')).textContent).toContain('đã hết hạn');
    cleanup();

    open('/partner/apply/edit', '', <PartnerApplyEditPage />);
    expect(screen.getByRole('alert').textContent).toContain('không đầy đủ');
    expect(h.getForEdit).toHaveBeenCalledTimes(1);
  });
});

describe('OwnerActivatePage (U4 / U5)', () => {
  const info = { fullName: 'Trần Văn Minh', email: 'minh@example.vn', businessName: 'Công ty TNHH Bãi xe Thủ Đức', expiresAtUtc: '2026-11-16T03:00:00Z' };

  it('checks the link, sets the password and points to login', async () => {
    const user = userEvent.setup();
    h.validate.mockResolvedValue(info);
    h.activate.mockResolvedValue(undefined);
    open('/owner/activate', '#token=act_456', <OwnerActivatePage />);

    expect(await screen.findByText('Công ty TNHH Bãi xe Thủ Đức')).toBeTruthy();
    expect(h.validate).toHaveBeenCalledWith('act_456');
    expect(window.location.hash).toBe('');

    await user.type(byId('activate-password'), 'short');
    await user.click(screen.getByRole('button', { name: /Kích hoạt tài khoản/ }));
    expect(screen.getByRole('alert').textContent).toContain('tối thiểu 8 ký tự');

    await user.clear(byId('activate-password'));
    await user.type(byId('activate-password'), 'matkhau123');
    await user.type(byId('activate-confirm'), 'matkhau124');
    expect(screen.getByText('Mật khẩu xác nhận chưa khớp.')).toBeTruthy();
    await user.clear(byId('activate-confirm'));
    await user.type(byId('activate-confirm'), 'matkhau123');
    await user.click(screen.getByRole('button', { name: /Kích hoạt tài khoản/ }));

    await waitFor(() => expect(h.activate).toHaveBeenCalledWith('act_456', 'matkhau123'));
    expect(await screen.findByRole('heading', { name: 'Tài khoản đã sẵn sàng' })).toBeTruthy();
    expect(screen.getByRole('link', { name: /Đến trang đăng nhập/ }).getAttribute('href')).toBe('/login');
  });

  it('explains an invalid link and keeps the form hidden', async () => {
    h.validate.mockRejectedValue(new Error('Link kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn.'));
    open('/owner/activate', '#token=old', <OwnerActivatePage />);

    expect((await screen.findByRole('alert')).textContent).toContain('đã được dùng');
    expect(document.getElementById('activate-password')).toBeNull();
  });

  it('keeps the form and shows the server message when activation fails', async () => {
    const user = userEvent.setup();
    h.validate.mockResolvedValue(info);
    h.activate.mockRejectedValue(new Error('Link kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn.'));
    open('/owner/activate', '#token=act_456', <OwnerActivatePage />);

    await screen.findByText('Công ty TNHH Bãi xe Thủ Đức');
    await user.type(byId('activate-password'), 'matkhau123');
    await user.type(byId('activate-confirm'), 'matkhau123');
    await user.click(screen.getByRole('button', { name: /Kích hoạt tài khoản/ }));

    expect((await screen.findByRole('alert')).textContent).toContain('đã được dùng');
    expect(byId('activate-password')).toBeTruthy();
  });
});
