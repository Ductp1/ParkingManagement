import { useEffect } from 'react';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { SanctionDetailDto, SanctionSummaryDto } from '@/types/adminSanctions';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { SanctionsListPage } from './SanctionsListPage';

const h = vi.hoisted(() => ({ list: vi.fn(), stats: vi.fn(), getById: vi.fn(), retrySync: vi.fn(), revoke: vi.fn() }));
vi.mock('@/api/adminSanctionsApi', () => ({
  adminSanctionsApi: { list: h.list, stats: h.stats, getById: h.getById, retrySync: h.retrySync, revoke: h.revoke, suggestion: vi.fn(), issue: vi.fn() },
}));
vi.mock('@/api/adminUsersApi', () => ({ adminUsersApi: { list: vi.fn(() => new Promise(() => undefined)), getById: vi.fn() } }));

const sanction = (patch: Partial<SanctionSummaryDto> = {}): SanctionSummaryDto => ({
  id: 7,
  ownerProfileId: 2,
  ownerUserId: 3,
  parkingLotId: 4,
  level: 'RankingPenalty',
  reason: 'Từ chối 5 booking trong tuần',
  startsAtUtc: '2026-11-06T08:00:00Z',
  endsAtUtc: '2026-11-20T08:00:00Z',
  correctionDueAtUtc: null,
  appealDeadlineUtc: '2026-11-13T16:59:59Z',
  status: 'Active',
  penaltyAmount: 500000,
  issuedByUserId: 1,
  revokedAtUtc: null,
  userServiceSyncStatus: 'NotRequired',
  parkingSyncStatus: 'Failed',
  notificationSyncStatus: 'Synced',
  createdAtUtc: '2026-11-06T08:00:00Z',
  updatedAtUtc: null,
  ...patch,
});

const detail = (patch: Partial<SanctionSummaryDto> = {}): SanctionDetailDto => ({
  sanction: sanction(patch),
  evidenceJson: '{"note":"BK-1, BK-2"}',
  revokedByUserId: null,
  revokeReason: null,
  userServiceSyncedAtUtc: null,
  parkingSyncedAtUtc: null,
  notificationSentAtUtc: '2026-11-06T08:00:01Z',
  riskFlagIds: [],
  timeline: [{ auditLogId: 1, atUtc: '2026-11-06T08:00:00Z', action: 'Sanction.Issued', userId: 1, reason: 'Từ chối 5 booking trong tuần' }],
});

const seen = { search: '' };
/** Ghi query string hiện tại sau mỗi lần render (trong effect, không gán biến ngoài khi render). */
const LocationProbe = () => {
  const { search } = useLocation();
  useEffect(() => {
    seen.search = search;
  }, [search]);
  return null;
};

const renderPage = (url = '/admin/support/sanctions') =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route
            path="/admin/support/sanctions"
            element={
              <>
                <SanctionsListPage />
                <LocationProbe />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

beforeEach(() => {
  vi.clearAllMocks();
  h.list.mockResolvedValue({ items: [sanction()], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 });
  h.stats.mockResolvedValue({ activeTotal: 3, warning: 1, rankingPenalty: 1, temporarySuspension: 1, permanentBan: 0, syncFailed: 1, syncPending: 2, endingWithin7Days: 1 });
  h.getById.mockResolvedValue(detail());
});
afterEach(() => cleanup());

describe('SanctionsListPage (US-097)', () => {
  it('hiển thị KPI và dòng chế tài: cấp, phạm vi, đồng bộ lỗi, tiền phạt vi-VN', async () => {
    renderPage();
    const row = (await screen.findByText('Từ chối 5 booking trong tuần')).closest('tr')!;
    expect(within(row).getByText('L2 · Ranking penalty')).toBeTruthy();
    expect(within(row).getByText('Lot #4')).toBeTruthy();
    expect(within(row).getByText('Failed')).toBeTruthy();
    expect(within(row).getByText('₫500.000')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('kpi-Sync failed').textContent).toContain('2 pending'));
  });

  it('đổi bộ lọc ghi lên URL và gọi lại API với bộ lọc đó', async () => {
    const user = userEvent.setup();
    renderPage('/admin/support/sanctions?ownerProfileId=2');
    await screen.findByText('Từ chối 5 booking trong tuần');
    expect(h.list.mock.calls[0][0]).toMatchObject({ ownerProfileId: 2 });

    await user.click(screen.getByRole('button', { name: /L4 · Termination/ }));
    await waitFor(() => expect(seen.search).toContain('level=PermanentBan'));
    await waitFor(() => expect(h.list).toHaveBeenLastCalledWith(expect.objectContaining({ level: 'PermanentBan', ownerProfileId: 2 }), 1, 20));

    await user.click(screen.getByRole('button', { name: /Sync failed: 1/ }));
    await waitFor(() => expect(seen.search).toContain('syncFailed=true'));
    await waitFor(() => expect(h.list).toHaveBeenLastCalledWith(expect.objectContaining({ syncFailed: true }), 1, 20));

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    await waitFor(() => expect(seen.search).toBe(''));
  });

  it('mở chi tiết, "Retry sync" gọi API rồi cập nhật trạng thái đồng bộ', async () => {
    h.retrySync.mockResolvedValue(detail({ parkingSyncStatus: 'Synced' }));
    const user = userEvent.setup();
    renderPage('/admin/support/sanctions?sanction=7');

    const parkingRow = await screen.findByTestId('sync-Parking');
    expect(within(parkingRow).getByText('Failed')).toBeTruthy();
    expect(screen.getByText('BK-1, BK-2')).toBeTruthy();
    expect(screen.getByText('Sanction issued')).toBeTruthy();

    await user.click(screen.getByRole('button', { name: 'Retry sync' }));
    await waitFor(() => expect(h.retrySync).toHaveBeenCalledWith(7));
    await waitFor(() => expect(within(screen.getByTestId('sync-Parking')).getByText('Synced')).toBeTruthy());
    expect(await screen.findByText('Sanction synced')).toBeTruthy();
  });

  it('gỡ chế tài bắt buộc lý do rồi gọi revoke', async () => {
    h.revoke.mockResolvedValue(detail({ status: 'Revoked', revokedAtUtc: '2026-11-06T09:00:00Z' }));
    const user = userEvent.setup();
    renderPage('/admin/support/sanctions?sanction=7');

    await user.click(await screen.findByRole('button', { name: 'Revoke sanction' }));
    const dialog = await screen.findByRole('dialog', { name: 'Revoke sanction #7' });
    await user.click(within(dialog).getByRole('button', { name: 'Revoke sanction' }));
    expect(await within(dialog).findByText('A reason is required.')).toBeTruthy();
    expect(h.revoke).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText('Reason'), 'Khiếu nại được chấp nhận');
    await user.click(within(dialog).getByRole('button', { name: 'Revoke sanction' }));
    await waitFor(() => expect(h.revoke).toHaveBeenCalledWith(7, { reason: 'Khiếu nại được chấp nhận' }));
    expect(await screen.findByText('Sanction #7 revoked')).toBeTruthy();
  });
});
