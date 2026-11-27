import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { PayoutsPage } from './PayoutsPage';
import { account, page, renderPage, settlement, stats } from './settlementTestFixtures';

const h = vi.hoisted(() => ({ list: vi.fn(), stats: vi.fn(), payoutAccounts: vi.fn(), markPaid: vi.fn(), get: vi.fn(), lines: vi.fn() }));
vi.mock('@/api/adminSettlementsApi', () => ({ adminSettlementsApi: h }));

const PATH = '/admin/finance/payouts';
const APPROVED = settlement({ status: 'Approved', approvedAtUtc: '2026-11-10T03:00:00Z', netPayout: 85_000 });
const NEGATIVE = settlement({ id: 7, code: 'STL-20261102-0003', ownerProfileId: 3, status: 'Approved', approvedAtUtc: '2026-11-10T03:00:00Z', netPayout: -10_000 });

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.list.mockResolvedValue(page([APPROVED, NEGATIVE]));
  h.stats.mockResolvedValue(stats());
  h.payoutAccounts.mockResolvedValue({ items: [account(1, 'Công ty Bãi Xanh')], missingOwnerProfileIds: [3] });
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('PayoutsPage (US-080)', () => {
  it('lists approved settlements with the masked bank account; non-positive net payout has no Mark paid', async () => {
    renderPage(PATH, PATH, <PayoutsPage />);

    const row = (await screen.findByText('Công ty Bãi Xanh')).closest('tr') as HTMLElement;
    expect(within(row).getByText('MB Bank · ****6789')).toBeTruthy();
    expect(within(row).getByRole('button', { name: 'Mark paid' })).toBeTruthy();
    expect(within(row).getByRole('link', { name: 'Công ty Bãi Xanh' }).getAttribute('href')).toBe('/admin/operations/partner-onboarding/41');
    const negative = screen.getByText('STL-20261102-0003').closest('tr') as HTMLElement;
    expect(within(negative).queryByRole('button', { name: 'Mark paid' })).toBeNull();
    expect(within(negative).getByText('Carried over')).toBeTruthy();
    expect(within(negative).getByText('No payout account on file')).toBeTruthy();
    expect(h.list).toHaveBeenCalledWith({ status: 'Approved' }, 1, 20);
    expect(await screen.findByText('₫120.000')).toBeTruthy();                       // tổng chờ chuyển từ API
  });

  it('Paid history is a URL filter', async () => {
    renderPage(PATH, `${PATH}?status=Paid`, <PayoutsPage />);
    await waitFor(() => expect(h.list).toHaveBeenCalledWith({ status: 'Paid' }, 1, 20));
  });

  it('Mark paid requires a payout reference and sends it with the transfer time', async () => {
    h.markPaid.mockResolvedValue({ ...APPROVED, status: 'Paid', payoutReference: 'FT26314XYZ' });
    renderPage(PATH, PATH, <PayoutsPage />);

    fireEvent.click(within((await screen.findByText('Công ty Bãi Xanh')).closest('tr') as HTMLElement).getByRole('button', { name: 'Mark paid' }));
    const d = screen.getByRole('dialog', { name: /Mark STL-20261102-0001 as paid/ });
    expect(within(d).getByText('₫85.000')).toBeTruthy();
    fireEvent.click(within(d).getByRole('button', { name: 'Mark as paid' }));
    expect(await within(d).findByText('A payout reference is required.')).toBeTruthy();
    expect(h.markPaid).not.toHaveBeenCalled();

    fireEvent.change(within(d).getByLabelText(/Payout reference/), { target: { value: ' FT26314XYZ ' } });
    fireEvent.change(within(d).getByLabelText(/Transferred at/), { target: { value: '2026-11-10T09:30' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Mark as paid' }));

    await waitFor(() => expect(h.markPaid).toHaveBeenCalledWith(5, 'rv1', 'FT26314XYZ', new Date('2026-11-10T09:30').toISOString()));
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });

  it('409 on Mark paid shows the conflict and reloads the list', async () => {
    h.markPaid.mockRejectedValue(new AdminApiError(409, 'Dữ liệu kỳ quyết toán đã thay đổi, hãy tải lại.'));
    renderPage(PATH, PATH, <PayoutsPage />);

    fireEvent.click(within((await screen.findByText('Công ty Bãi Xanh')).closest('tr') as HTMLElement).getByRole('button', { name: 'Mark paid' }));
    const d = screen.getByRole('dialog', { name: /as paid/ });
    fireEvent.change(within(d).getByLabelText(/Payout reference/), { target: { value: 'FT1' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Mark as paid' }));

    expect(await within(d).findByText(/Data has changed since you opened it/)).toBeTruthy();
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });
});
