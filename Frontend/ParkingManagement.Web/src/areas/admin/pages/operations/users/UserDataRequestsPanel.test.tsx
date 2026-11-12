import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminDataRequestDto } from '@/types/adminIdentity';
import type { AdminUserDto } from '@/types/adminUsers';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { UserDataRequestsPanel } from './UserDataRequestsPanel';
import { UserDetailPage } from './UserDetailPage';
import { MAX_PAGES, isOpenRequest, loadUserDataRequests, requestCode } from './userDataRequests';

const h = vi.hoisted(() => ({ list: vi.fn(), getById: vi.fn(), listOwnerSanctions: vi.fn() }));
vi.mock('@/api/adminIdentityApi', () => ({ adminDataRequestsApi: { list: h.list } }));
vi.mock('@/api/adminUsersApi', () => ({ adminUsersApi: { getById: h.getById, listOwnerSanctions: h.listOwnerSanctions } }));

const PAST = '2020-01-01T00:00:00Z';
const FUTURE = '2099-01-01T00:00:00Z';

let seq = 1000;
const dsr = (over: Partial<AdminDataRequestDto> = {}): AdminDataRequestDto => ({
  id: seq++,
  userId: 99,
  type: 'Export',
  status: 'Submitted',
  createdAtUtc: '2026-11-01T02:00:00Z',
  dueAtUtc: FUTURE,
  completedAtUtc: null,
  handledByUserId: null,
  resultNote: null,
  ...over,
});
/** Một trang đầy (20 dòng) của người khác, chèn thêm `mine` ở đầu. */
const fullPage = (...mine: AdminDataRequestDto[]) => [...mine, ...Array.from({ length: 20 - mine.length }, () => dsr())];

const staff = (): AdminUserDto => ({
  id: 7, fullName: 'Lan Staff', email: 'lan@example.test', phoneNumber: null, status: 'Active', kycStatus: 'NotSubmitted',
  roles: ['Staff'], ownerProfile: null,
});

const renderPanel = (userId = 7) =>
  render(
    <MemoryRouter>
      <UserDataRequestsPanel userId={userId} />
    </MemoryRouter>
  );

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
});
afterEach(cleanup);

describe('loadUserDataRequests', () => {
  it('reads pages until a short page and keeps only this user', async () => {
    const a = dsr({ userId: 7 });
    const b = dsr({ userId: 7 });
    h.list.mockResolvedValueOnce(fullPage(a)).mockResolvedValueOnce([dsr(), b]);

    const result = await loadUserDataRequests(7);

    expect(h.list).toHaveBeenCalledTimes(2);
    expect(h.list).toHaveBeenNthCalledWith(1, { page: 1 });
    expect(h.list).toHaveBeenNthCalledWith(2, { page: 2 });
    expect(result).toEqual({ items: [a, b], truncated: false, scanned: 22 });
  });

  it(`stops after ${MAX_PAGES} full pages and reports truncation`, async () => {
    h.list.mockImplementation(() => Promise.resolve(fullPage()));

    const result = await loadUserDataRequests(7);

    expect(h.list).toHaveBeenCalledTimes(MAX_PAGES);
    expect(result).toEqual({ items: [], truncated: true, scanned: MAX_PAGES * 20 });
  });

  it('propagates source errors', async () => {
    h.list.mockRejectedValue(new AdminApiError(500, 'boom'));
    await expect(loadUserDataRequests(7)).rejects.toBeInstanceOf(AdminApiError);
  });

  it('formats codes and open statuses', () => {
    expect(requestCode(12)).toBe('DSR-00012');
    expect(isOpenRequest(dsr({ status: 'Submitted' }))).toBe(true);
    expect(isOpenRequest(dsr({ status: 'InProgress' }))).toBe(true);
    expect(isOpenRequest(dsr({ status: 'Completed' }))).toBe(false);
    expect(isOpenRequest(dsr({ status: 'Rejected' }))).toBe(false);
  });
});

describe('UserDataRequestsPanel', () => {
  it('lists the user requests with links to the request on Security & Privacy', async () => {
    h.list.mockResolvedValueOnce([
      dsr({ id: 41, userId: 7, type: 'Deletion', status: 'InProgress', dueAtUtc: PAST }),
      dsr({ id: 42, userId: 7, type: 'Correction', status: 'Completed', dueAtUtc: PAST }),
      dsr({ id: 43, userId: 8 }),
    ]);
    renderPanel();

    const link = await screen.findByRole('link', { name: 'DSR-00041' });
    expect(link.getAttribute('href')).toBe('/admin/access/security-privacy?request=41');
    expect(screen.getByRole('link', { name: 'DSR-00042' }).getAttribute('href')).toBe('/admin/access/security-privacy?request=42');
    expect(screen.queryByText('DSR-00043')).toBeNull();
    expect(screen.getByRole('link', { name: 'All requests' }).getAttribute('href')).toBe('/admin/access/security-privacy');

    // Quá hạn chỉ tính cho yêu cầu còn mở; yêu cầu đã xong giữ trạng thái thật.
    const rows = screen.getAllByRole('row');
    expect(within(rows[1]).getByText('Overdue')).toBeTruthy();
    expect(within(rows[1]).getByText('Deletion')).toBeTruthy();
    expect(within(rows[2]).queryByText('Overdue')).toBeNull();
    expect(within(rows[2]).getByText('Completed')).toBeTruthy();
    expect(screen.queryByText(/older ones are not shown/)).toBeNull();
  });

  it('notes when only the latest requests were searched', async () => {
    h.list.mockImplementation((q: { page: number }) => Promise.resolve(q.page === 1 ? fullPage(dsr({ id: 5, userId: 7 })) : fullPage()));
    renderPanel();

    expect(await screen.findByRole('link', { name: 'DSR-00005' })).toBeTruthy();
    expect(screen.getByText('Searched the latest 100 requests; older ones are not shown.')).toBeTruthy();
  });

  it('shows an empty state when the user has no requests', async () => {
    h.list.mockResolvedValueOnce([dsr({ userId: 8 })]);
    renderPanel();

    expect(await screen.findByText('No data requests')).toBeTruthy();
    expect(screen.getByText('This user has not asked to view, export, correct or delete their data.')).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('shows the server message and retries on error', async () => {
    h.list.mockRejectedValueOnce(new AdminApiError(500, 'UserService lỗi.', { title: 'Error', detail: 'UserService lỗi.' }));
    renderPanel();

    expect(await screen.findByText('Could not load data requests')).toBeTruthy();
    expect(screen.getByText('UserService lỗi.')).toBeTruthy();

    h.list.mockResolvedValueOnce([dsr({ id: 9, userId: 7 })]);
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByRole('link', { name: 'DSR-00009' })).toBeTruthy();
  });

  it('shows a permission state on 403', async () => {
    h.list.mockRejectedValueOnce(new AdminApiError(403, 'Forbidden'));
    renderPanel();

    expect(await screen.findByText('Only administrators can see data requests.')).toBeTruthy();
  });
});

describe('UserDetailPage data panels', () => {
  it('shows real data requests and no placeholder panels without a source', async () => {
    h.getById.mockResolvedValue(staff());
    h.list.mockResolvedValueOnce([dsr({ id: 77, userId: 7 })]);
    render(
      <AdminToastProvider>
        <MemoryRouter initialEntries={['/admin/operations/users/7']}>
          <Routes>
            <Route element={<Outlet context={{ setDetailLabel: () => {} }} />}>
              <Route path="/admin/operations/users/:id" element={<UserDetailPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AdminToastProvider>
    );

    expect(await screen.findByRole('link', { name: 'DSR-00077' })).toBeTruthy();
    expect(screen.getByText('Data subject requests')).toBeTruthy();
    expect(screen.queryByText('Security events')).toBeNull();
    expect(screen.queryByText(/has no endpoint/)).toBeNull();
  });
});
