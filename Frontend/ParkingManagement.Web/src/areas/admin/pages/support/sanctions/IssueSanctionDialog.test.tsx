import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { IssueSanctionDialog, type IssueSanctionPreset } from './IssueSanctionDialog';

const h = vi.hoisted(() => ({
  listUsers: vi.fn(),
  getUser: vi.fn(),
  ownerLots: vi.fn(),
  suggestion: vi.fn(),
  issue: vi.fn(),
}));
vi.mock('@/api/adminUsersApi', () => ({ adminUsersApi: { list: h.listUsers, getById: h.getUser } }));
vi.mock('@/api/adminOperationsApi', () => ({ adminOperationsApi: { getOwnerParkingLots: h.ownerLots } }));
vi.mock('@/api/adminSanctionsApi', () => ({ adminSanctionsApi: { suggestion: h.suggestion, issue: h.issue } }));

const lot = (id: number, name: string) => ({
  id, name, address: 'TP.HCM', city: 'TP.HCM', district: null, totalSlots: 100, availableSlots: 10, maxHeightCm: 210,
  status: 'Active', openingHours: '06:00 - 23:00', createdAtUtc: '2026-01-01T00:00:00Z',
});

beforeEach(() => {
  vi.clearAllMocks();
  h.listUsers.mockResolvedValue({ items: [{ id: 3, fullName: 'Nguyễn Văn A', email: null, status: 'Active', roles: ['LotOwner'], businessName: 'Bãi xe TSN' }], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 });
  h.getUser.mockResolvedValue({ id: 3, fullName: 'Nguyễn Văn A', email: null, phoneNumber: null, status: 'Active', kycStatus: 'Verified', roles: ['LotOwner'], ownerProfile: { id: 2, businessName: 'Bãi xe TSN' } });
  h.ownerLots.mockResolvedValue([lot(4, 'Bến Thành'), lot(5, 'Tân Sơn Nhất')]);
  h.suggestion.mockResolvedValue({ ownerProfileId: 2, suggestedLevel: 'RankingPenalty', windowDays: 90, rationale: 'Đã có cảnh cáo.', basis: [{ id: 1 }] });
  h.issue.mockResolvedValue({ sanctions: [{ id: 10, parkingSyncStatus: 'Synced', userServiceSyncStatus: 'NotRequired', notificationSyncStatus: 'Synced' }] });
});
afterEach(() => cleanup());

const renderDialog = (preset?: IssueSanctionPreset) => {
  const onIssued = vi.fn();
  const onClose = vi.fn();
  render(
    <AdminToastProvider>
      <IssueSanctionDialog preset={preset} onClose={onClose} onIssued={onIssued} />
    </AdminToastProvider>
  );
  return { onIssued, onClose, dialog: () => screen.getByRole('dialog') };
};

const chooseOwner = async () => {
  const user = userEvent.setup();
  await waitFor(() => expect(screen.getByRole('option', { name: 'Bãi xe TSN · U#3' })).toBeTruthy());
  await user.selectOptions(screen.getByLabelText('Owner'), '3');
  await waitFor(() => expect(screen.getByText('OwnerProfile #2')).toBeTruthy());
  return user;
};

describe('IssueSanctionDialog (US-097)', () => {
  it('trường đổi theo cấp: L1 hạn khắc phục, L2 thời điểm kết thúc, L4 ẩn bãi và cần gõ mã chủ bãi', async () => {
    renderDialog();
    const user = await chooseOwner();

    await user.selectOptions(screen.getByLabelText('Level'), 'Warning');
    expect(screen.getByLabelText('Correction deadline (ICT)')).toBeTruthy();
    expect(screen.queryByLabelText('Ends at (ICT)')).toBeNull();

    await user.selectOptions(screen.getByLabelText('Level'), 'RankingPenalty');
    expect(screen.getByLabelText('Ends at (ICT)')).toBeTruthy();
    expect(screen.getByText('At most 30 days from now.')).toBeTruthy();

    await user.selectOptions(screen.getByLabelText('Level'), 'PermanentBan');
    expect(screen.queryByText(/Select all lots/)).toBeNull();
    await user.type(screen.getByLabelText('Reason'), 'Gian lận doanh thu');
    await user.click(screen.getByRole('button', { name: 'Terminate partner' }));
    expect(await screen.findByText('Type 2 to confirm the permanent ban.')).toBeTruthy();
    expect(h.issue).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText('Type 2 to confirm'), '2');
    await user.click(screen.getByRole('button', { name: 'Terminate partner' }));
    await waitFor(() => expect(h.issue).toHaveBeenCalledTimes(1));
    expect(h.issue.mock.calls[0][0]).toMatchObject({ ownerProfileId: 2, ownerUserId: 3, level: 'PermanentBan', parkingLotIds: [], endsAtUtc: null });
  });

  it('chọn tất cả bãi của chủ bãi → 1 request cấp 3 cho cả 2 bãi', async () => {
    const { onIssued } = renderDialog();
    const user = await chooseOwner();
    await user.selectOptions(screen.getByLabelText('Level'), 'TemporarySuspension');
    expect(await screen.findByText(/temporary account lock of the whole owner/)).toBeTruthy();

    await user.click(await screen.findByLabelText(/Select all lots of this owner \(2\)/));
    expect(screen.getByText('2 lot(s) selected · one sanction per lot')).toBeTruthy();
    await user.type(screen.getByLabelText('Reason'), 'Mất kết nối thiết bị');
    await user.click(screen.getByRole('button', { name: 'Issue sanction' }));

    await waitFor(() => expect(onIssued).toHaveBeenCalled());
    const body = h.issue.mock.calls[0][0];
    expect(body.parkingLotIds).toEqual([4, 5]);
    expect(body.level).toBe('TemporarySuspension');
    expect(body.endsAtUtc).toMatch(/Z$/);
  });

  it('lỗi 409 của server hiện trong dialog, dialog không đóng', async () => {
    h.issue.mockRejectedValue(new AdminApiError(409, 'Bãi 5 đang có chế tài Tạm ngừng còn hiệu lực (bãi 5 – chế tài #7).'));
    const { onClose } = renderDialog({ ownerProfileId: 2, level: 'TemporarySuspension', lotIds: [5] });
    const user = userEvent.setup();
    await waitFor(() => expect(screen.getByText('OwnerProfile #2')).toBeTruthy());     // chủ bãi được chọn sẵn theo OwnerProfileId

    await user.type(screen.getByLabelText('Reason'), 'Từ chối booking');
    await user.click(screen.getByRole('button', { name: 'Issue sanction' }));

    expect(await screen.findByText(/bãi 5 – chế tài #7/)).toBeTruthy();
    expect(onClose).not.toHaveBeenCalled();
    expect(h.issue.mock.calls[0][0].parkingLotIds).toEqual([5]);
  });

  it('banner gợi ý cấp từ sanction-suggestion, bấm "Use suggestion" chọn cấp đó', async () => {
    renderDialog();
    const user = await chooseOwner();

    const banner = await screen.findByTestId('sanction-suggestion');
    expect(banner.textContent).toContain('L2 · Ranking penalty');
    await user.click(within(banner.parentElement!.parentElement!).getByRole('button', { name: 'Use suggestion' }));
    expect((screen.getByLabelText('Level') as HTMLSelectElement).value).toBe('RankingPenalty');
    expect(h.suggestion.mock.calls[0][1]).toBeInstanceOf(AbortSignal);
  });
});
