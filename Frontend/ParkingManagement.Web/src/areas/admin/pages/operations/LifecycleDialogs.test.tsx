import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import type { AdminUserDto } from '@/types/adminUsers';
import { DiscardApplicationDialog } from './partner/DiscardApplicationDialog';
import { RequestInfoDialog } from './partner/RequestInfoDialog';
import { DeleteUserDialog, ReactivateUserDialog, SuspendUserDialog } from './users/UserDialogs';
import { MoreActionsMenu } from '../../components/MoreActionsMenu';

const h = vi.hoisted(() => {
  const state = {
    cancel: vi.fn(),
    requestInfo: vi.fn(),
    suspend: vi.fn(),
    reactivate: vi.fn(),
    del: vi.fn(),
    failWith: null as Error | null,
  };
  // Lỗi từ API đi qua hàm thường (không qua vi.fn) để vitest không coi promise bị reject của spy là lỗi của test.
  const run = (fn: (...args: unknown[]) => unknown) => async (...args: unknown[]) => {
    if (state.failWith) throw state.failWith;
    return fn(...args);
  };
  return { ...state, state, run };
});
vi.mock('@/api/adminPartnerOnboardingApi', () => ({
  adminPartnerOnboardingApi: { cancel: h.run((...a) => h.cancel(...a)), requestInfo: h.run((...a) => h.requestInfo(...a)) },
}));
vi.mock('@/api/adminUsersApi', () => ({
  adminUsersApi: {
    suspendUser: h.run((...a) => h.suspend(...a)),
    reactivateUser: h.run((...a) => h.reactivate(...a)),
    deleteUser: h.run((...a) => h.del(...a)),
  },
}));

const app = (status: string) =>
  ({ id: 18, code: 'PA-2026-0018', status, businessName: 'CTY Hồng Phát', taxCode: '9981821722', fullName: 'Nguyễn Quốc An', email: 'an@example.vn', rowVersion: 'rv1' }) as PartnerApplicationDetailDto;

const user = { id: 7, fullName: 'Trần Văn Minh', email: 'minh@example.vn', phoneNumber: null, status: 'Active', kycStatus: 'Verified', roles: ['Driver'], ownerProfile: null, rowVersion: 'rv9' } as AdminUserDto;

const result = { userId: 7, status: 'Locked', lockedUntilUtc: null, rowVersion: 'rv10', changed: true };

beforeEach(() => {
  [h.cancel, h.requestInfo, h.suspend, h.reactivate, h.del].forEach((fn) => fn.mockReset());
  h.state.failWith = null;
});
afterEach(cleanup);

describe('Discard application (US-116)', () => {
  it('needs only a reason before account issue and sends the row version', async () => {
    const u = userEvent.setup();
    const onDone = vi.fn();
    render(<DiscardApplicationDialog open app={app('UnderReview')} onClose={vi.fn()} onDone={onDone} />);

    expect(screen.queryByLabelText(/Type the application code/)).toBeNull();
    await u.click(screen.getByRole('button', { name: 'Discard application' }));
    expect(await screen.findByText('A reason is required.')).toBeTruthy();
    expect(h.cancel).not.toHaveBeenCalled();

    await u.type(screen.getByLabelText(/^Reason/), 'Đối tác rút hồ sơ');
    await u.click(screen.getByRole('button', { name: 'Discard application' }));

    await waitFor(() => expect(h.cancel).toHaveBeenCalledWith(18, { reason: 'Đối tác rút hồ sơ', rowVersion: 'rv1', confirmCode: null }));
    expect(onDone).toHaveBeenCalled();
  });

  it('after Account issued warns, requires retyping the code, and shows a 409 verbatim', async () => {
    const u = userEvent.setup();
    h.state.failWith = new AdminApiError(409, 'Chủ bãi đã kích hoạt tài khoản – hãy dùng Đình chỉ hoặc Xóa user thay vì hủy hồ sơ.');
    render(<DiscardApplicationDialog open app={app('AccountIssued')} onClose={vi.fn()} onDone={vi.fn()} />);

    expect(screen.getByText(/The issued owner account \(never activated\) will be removed/)).toBeTruthy();
    await u.type(screen.getByLabelText(/^Reason/), 'Không liên lạc được');
    await u.click(screen.getByRole('button', { name: 'Discard application' }));
    expect(await screen.findByText('Type PA-2026-0018 exactly to confirm.')).toBeTruthy();
    expect(h.cancel).not.toHaveBeenCalled();

    await u.type(screen.getByLabelText(/Type the application code/), 'pa-2026-0018');
    await u.click(screen.getByRole('button', { name: 'Discard application' }));
    expect(await screen.findByText(/Đình chỉ hoặc Xóa user/)).toBeTruthy();
  });
});

describe('Request more information (US-116)', () => {
  it('needs a note, sends chosen items and due date, and rejects a past due date', async () => {
    const u = userEvent.setup();
    const onDone = vi.fn();
    render(<RequestInfoDialog open app={app('UnderReview')} onClose={vi.fn()} onDone={onDone} />);

    await u.click(screen.getByRole('button', { name: 'Send request' }));
    expect(await screen.findByText('A note is required.')).toBeTruthy();

    await u.type(screen.getByLabelText(/^Note to applicant/), 'Chụp lại CCCD rõ nét');
    await u.click(screen.getByLabelText('Tax code'));
    await u.click(screen.getByLabelText('ID card – back'));
    fireEvent.change(screen.getByLabelText(/^Respond by/), { target: { value: '2000-01-01T10:00' } });
    await u.click(screen.getByRole('button', { name: 'Send request' }));
    expect(await screen.findByText('Must be after the current time.')).toBeTruthy();
    expect(h.requestInfo).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText(/^Respond by/), { target: { value: '' } });
    await u.click(screen.getByRole('button', { name: 'Send request' }));

    await waitFor(() => expect(h.requestInfo).toHaveBeenCalledTimes(1));
    expect(h.requestInfo.mock.calls[0]).toEqual([
      18,
      { note: 'Chụp lại CCCD rõ nét', items: ['taxCode', 'doc:idCardBack'], dueAtUtc: null, rowVersion: 'rv1' },
    ]);
    expect(onDone).toHaveBeenCalled();
  });
});

describe('User lifecycle dialogs (US-096)', () => {
  it('suspends with a required reason, optional end date and the row version', async () => {
    const u = userEvent.setup();
    h.suspend.mockResolvedValue(result);
    const onDone = vi.fn();
    render(<SuspendUserDialog open user={user} onClose={vi.fn()} onDone={onDone} />);

    await u.click(screen.getByRole('button', { name: 'Suspend user' }));
    expect(await screen.findByText('A reason is required.')).toBeTruthy();
    await u.type(screen.getByLabelText(/^Reason/), 'Nghi gian lận');
    await u.click(screen.getByRole('button', { name: 'Suspend user' }));

    await waitFor(() => expect(h.suspend).toHaveBeenCalledWith(7, { reason: 'Nghi gian lận', lockedUntilUtc: null, rowVersion: 'rv9' }));
    expect(onDone).toHaveBeenCalledWith(result);
  });

  it('refuses a suspension end date in the past', async () => {
    const u = userEvent.setup();
    render(<SuspendUserDialog open user={user} onClose={vi.fn()} onDone={vi.fn()} />);
    await u.type(screen.getByLabelText(/^Reason/), 'Tạm khóa');
    fireEvent.change(screen.getByLabelText(/^Suspended until/), { target: { value: '2000-01-01T10:00' } });
    await u.click(screen.getByRole('button', { name: 'Suspend user' }));
    expect(await screen.findByText('Must be after the current time.')).toBeTruthy();
    expect(h.suspend).not.toHaveBeenCalled();
  });

  it('reactivates with a reason and shows why the user was suspended', async () => {
    const u = userEvent.setup();
    h.reactivate.mockResolvedValue({ ...result, status: 'Active' });
    render(<ReactivateUserDialog open user={{ ...user, status: 'Locked', lockReason: 'Nghi gian lận' }} onClose={vi.fn()} onDone={vi.fn()} />);

    expect(screen.getByText(/Nghi gian lận/)).toBeTruthy();
    await u.type(screen.getByLabelText(/^Reason/), 'Đã xác minh');
    await u.click(screen.getByRole('button', { name: 'Reactivate user' }));
    await waitFor(() => expect(h.reactivate).toHaveBeenCalledWith(7, { reason: 'Đã xác minh', rowVersion: 'rv9' }));
  });

  it('deletes only after the email is typed and shows the blockers from a 409 verbatim', async () => {
    const u = userEvent.setup();
    h.state.failWith = new AdminApiError(409, 'Không thể xóa người dùng này: còn booking đang mở. Hãy xử lý các mục trên hoặc dùng Đình chỉ thay thế.');
    render(<DeleteUserDialog open user={user} onClose={vi.fn()} onDone={vi.fn()} />);

    await u.type(screen.getByLabelText(/^Reason/), 'Yêu cầu của người dùng');
    await u.click(screen.getByRole('button', { name: 'Delete user' }));
    expect(await screen.findByText('Type minh@example.vn exactly to confirm.')).toBeTruthy();
    expect(h.del).not.toHaveBeenCalled();

    await u.type(screen.getByLabelText(/Type the user/), 'MINH@example.vn');
    await u.click(screen.getByRole('button', { name: 'Delete user' }));
    expect(await screen.findByText(/còn booking đang mở/)).toBeTruthy();
  });
});

describe('MoreActionsMenu', () => {
  it('opens a menu, runs the chosen item, closes on Escape and supports arrow keys', async () => {
    const u = userEvent.setup();
    const onDelete = vi.fn();
    render(<MoreActionsMenu items={[{ key: 'a', label: 'Export' , onSelect: vi.fn() }, { key: 'd', label: 'Delete user…', destructive: true, onSelect: onDelete }]} />);

    const button = screen.getByRole('button', { name: /More actions/ });
    expect(button.getAttribute('aria-expanded')).toBe('false');
    await u.click(button);
    expect(screen.getByRole('menu')).toBeTruthy();
    await u.keyboard('{ArrowDown}');
    expect(document.activeElement?.textContent).toBe('Delete user…');
    await u.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
    expect(document.activeElement).toBe(button);

    await u.click(button);
    await u.click(screen.getByRole('menuitem', { name: 'Delete user…' }));
    expect(onDelete).toHaveBeenCalled();
    expect(screen.queryByRole('menu')).toBeNull();
  });
});
