import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { SettlementsPage } from './SettlementsPage';
import { account, detail, page, renderPage, settlement, stats } from './settlementTestFixtures';

// ---- Mock tầng API ----
const h = vi.hoisted(() => ({
  list: vi.fn(), stats: vi.fn(), periods: vi.fn(), get: vi.fn(), lines: vi.fn(), payoutAccounts: vi.fn(), generate: vi.fn(),
  regenerate: vi.fn(), submit: vi.fn(), approve: vi.fn(), reject: vi.fn(), markPaid: vi.fn(), exportList: vi.fn(), exportLines: vi.fn(),
}));
vi.mock('@/api/adminSettlementsApi', () => ({ adminSettlementsApi: h }));

const PATH = '/admin/finance/settlements';
const location = () => screen.getByTestId('location').textContent ?? '';
const dialog = (name: RegExp) => screen.getByRole('dialog', { name });

const DRAFT = settlement();
const BLOCKED = settlement({ id: 6, code: 'STL-20261102-0002', ownerProfileId: 2, isBlocked: true, blockedReasons: ['HoldsNotLoaded'], netPayout: 40_000 });
const PENDING = settlement({ status: 'PendingReview', submittedAtUtc: '2026-11-10T04:00:00Z' });

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.list.mockResolvedValue(page([DRAFT, BLOCKED]));
  h.stats.mockResolvedValue(stats());
  h.periods.mockResolvedValue([
    { id: 1, periodStart: '2026-11-09', periodEnd: '2026-11-15', cycle: 'WEEKLY', hasEnded: false, settlementCount: 0 },
    { id: 2, periodStart: '2026-11-02', periodEnd: '2026-11-08', cycle: 'WEEKLY', hasEnded: true, settlementCount: 2 },
  ]);
  h.payoutAccounts.mockResolvedValue({ items: [account(1, 'Công ty Bãi Xanh')], missingOwnerProfileIds: [2] });
  h.lines.mockResolvedValue(page([
    { id: 90, lineType: 'Booking', paymentId: 1, refundId: null, parkingLotId: 11, referenceCode: 'BK-0001', amount: 100_000, commissionAmount: 10_000,
      isOwnerCollected: false, occurredAtUtc: '2026-11-05T04:00:00Z', blockedReason: null },
  ]));
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('SettlementsPage (US-080)', () => {
  it('lists settlements with owner names from AdminService and KPIs from PaymentService', async () => {
    renderPage(PATH, PATH, <SettlementsPage />);

    expect(await screen.findByText('Công ty Bãi Xanh')).toBeTruthy();
    expect(screen.getByText('OwnerProfile #2')).toBeTruthy();                                    // chưa có hồ sơ đối tác → không bịa tên
    const row = screen.getByText('STL-20261102-0001').closest('tr') as HTMLElement;
    expect(within(row).getByText('₫150.000')).toBeTruthy();
    expect(within(row).getByText('₫85.000')).toBeTruthy();
    expect(within(screen.getByText('STL-20261102-0002').closest('tr') as HTMLElement).getByText('Blocked')).toBeTruthy();
    expect(h.list).toHaveBeenCalledWith({ status: null, periodStart: null, search: null, blocked: null }, 1, 20);
    expect(h.payoutAccounts).toHaveBeenCalledWith([1, 2]);
    await waitFor(() => expect(screen.getByTestId('kpi-blocked').getAttribute('data-tone')).toBe('warning'));
  });

  it('keeps filters on the URL and queries with them', async () => {
    renderPage(PATH, `${PATH}?status=Approved&period=2026-11-02&blocked=true`, <SettlementsPage />);

    await waitFor(() =>
      expect(h.list).toHaveBeenCalledWith({ status: 'Approved', periodStart: '2026-11-02', search: null, blocked: true }, 1, 20));
    expect(h.stats).toHaveBeenCalledWith('2026-11-02');

    fireEvent.click(await screen.findByRole('button', { name: /Pending review/ }));
    await waitFor(() => expect(location()).toContain('status=PendingReview'));
    await waitFor(() => expect(h.list).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'PendingReview' }), 1, 20));
  });

  it('blocked Draft: shows the reasons and does not allow Submit', async () => {
    h.get.mockResolvedValue(detail(BLOCKED, ['Regenerate']));
    renderPage(PATH, `${PATH}?settlement=6`, <SettlementsPage />);

    const reasons = await screen.findByTestId('blocked-reasons');
    expect(within(reasons).getByText(/Dispute holds not loaded/)).toBeTruthy();
    expect((screen.getByRole('button', { name: 'Submit for review' }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole('button', { name: 'Regenerate' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('opens the drawer from a row and shows the NetPayout formula from the API', async () => {
    h.get.mockResolvedValue(detail(DRAFT, ['Regenerate', 'Submit']));
    renderPage(PATH, PATH, <SettlementsPage />);

    fireEvent.click((await screen.findByText('STL-20261102-0001')).closest('tr') as HTMLElement);
    await waitFor(() => expect(location()).toContain('settlement=5'));
    const formula = await screen.findByTestId('formula');
    expect(within(formula).getByText('Collected by owner')).toBeTruthy();
    expect(screen.getByTestId('net-payout').textContent).toBe('₫85.000');
    expect(await screen.findByText('BK-0001')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'View commission for this period' }).getAttribute('href'))
      .toBe('/admin/finance/commission?period=custom&from=2026-11-02&to=2026-11-08');
  });

  it('Approve calls the API with the row version, then reloads', async () => {
    h.get.mockResolvedValue(detail(PENDING, ['Approve', 'Reject']));
    h.approve.mockResolvedValue({ ...PENDING, status: 'Approved', rowVersion: 'rv2' });
    renderPage(PATH, `${PATH}?settlement=5`, <SettlementsPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));
    fireEvent.click(within(dialog(/Approve STL-20261102-0001/)).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(h.approve).toHaveBeenCalledWith(5, 'rv1'));
    await waitFor(() => expect(h.get).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('Settlement approved')).toBeTruthy();
  });

  it('409 conflict: tells the admin the data changed and reloads the latest version', async () => {
    h.get.mockResolvedValue(detail(PENDING, ['Approve', 'Reject']));
    h.approve.mockRejectedValue(new AdminApiError(409, 'Dữ liệu kỳ quyết toán đã thay đổi, hãy tải lại.'));
    renderPage(PATH, `${PATH}?settlement=5`, <SettlementsPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));
    fireEvent.click(within(dialog(/Approve STL/)).getByRole('button', { name: 'Approve' }));

    expect(await within(dialog(/Approve STL/)).findByText(/Data has changed since you opened it/)).toBeTruthy();
    await waitFor(() => expect(h.get).toHaveBeenCalledTimes(2));
  });

  it('Reject requires a reason', async () => {
    h.get.mockResolvedValue(detail(PENDING, ['Approve', 'Reject']));
    h.reject.mockResolvedValue({ ...PENDING, status: 'Draft' });
    renderPage(PATH, `${PATH}?settlement=5`, <SettlementsPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Reject' }));
    const d = dialog(/Reject STL/);
    fireEvent.click(within(d).getByRole('button', { name: 'Reject' }));
    expect(await within(d).findByText('A reason is required.')).toBeTruthy();
    expect(h.reject).not.toHaveBeenCalled();

    fireEvent.change(within(d).getByRole('textbox'), { target: { value: 'Missing hold CP-0001' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(h.reject).toHaveBeenCalledWith(5, 'rv1', 'Missing hold CP-0001'));
  });

  it('Generate calls the API with the chosen ended period and reports the outcome', async () => {
    h.generate.mockResolvedValue({
      periodStart: '2026-11-02', periodEnd: '2026-11-08', cycle: 'WEEKLY',
      owners: [
        { ownerProfileId: 1, outcome: 'Created', reason: null, settlementId: 5, code: 'STL-20261102-0001', netPayout: 85_000, isBlocked: false, blockedReasons: [], rowVersion: 'x' },
        { ownerProfileId: 3, outcome: 'Skipped', reason: 'NothingToSettle', settlementId: null, code: null, netPayout: null, isBlocked: null, blockedReasons: [], rowVersion: null },
      ],
    });
    renderPage(PATH, PATH, <SettlementsPage />);
    await screen.findByText('Công ty Bãi Xanh');

    fireEvent.click(screen.getByRole('button', { name: 'Generate' }));
    const d = dialog(/Generate settlements/);
    const select = within(d).getByRole('combobox') as HTMLSelectElement;
    expect([...select.options].map((o) => o.value)).toEqual(['', '2026-11-02']);                 // kỳ đang chạy không được chọn
    fireEvent.click(within(d).getByRole('button', { name: 'Generate' }));

    await waitFor(() => expect(h.generate).toHaveBeenCalledWith('2026-11-02', null));
    expect(await screen.findByText(/1 created, 0 regenerated, 1 skipped \(Nothing to settle\)/)).toBeTruthy();
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });

  it('Generate validates the optional owner ID', async () => {
    renderPage(PATH, PATH, <SettlementsPage />);
    await screen.findByText('Công ty Bãi Xanh');

    fireEvent.click(screen.getByRole('button', { name: 'Generate' }));
    const d = dialog(/Generate settlements/);
    fireEvent.change(within(d).getByRole('spinbutton'), { target: { value: '-2' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Generate' }));
    expect(await within(d).findByText(/positive whole number/)).toBeTruthy();
    expect(h.generate).not.toHaveBeenCalled();
  });

  it('shows the service error with Retry instead of fake zeros', async () => {
    h.list.mockRejectedValue(new AdminApiError(500, 'Lỗi hệ thống'));
    renderPage(PATH, PATH, <SettlementsPage />);

    expect(await screen.findByText('Could not load settlements')).toBeTruthy();
    expect(screen.queryByText('STL-20261102-0001')).toBeNull();                               // không có dòng nào, không số giả
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });
});
