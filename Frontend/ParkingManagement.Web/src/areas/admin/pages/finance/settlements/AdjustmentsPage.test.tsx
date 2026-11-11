import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { AdjustmentsPage } from './AdjustmentsPage';
import { account, adjustment, page, renderPage } from './settlementTestFixtures';

const h = vi.hoisted(() => ({
  adjustments: vi.fn(), payoutAccounts: vi.fn(), createAdjustment: vi.fn(), approveAdjustment: vi.fn(), rejectAdjustment: vi.fn(),
}));
vi.mock('@/api/adminSettlementsApi', () => ({ adminSettlementsApi: h }));

const PATH = '/admin/finance/adjustments';
const location = () => screen.getByTestId('location').textContent ?? '';
const PENDING = adjustment();
const APPROVED = adjustment({ id: 12, amount: 20_000, reason: 'Commission over-charged', status: 'Approved', approvedByUserId: 9, settlementId: 5,
  settlementCode: 'STL-20261102-0001', rowVersion: 'arv2' });

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.adjustments.mockResolvedValue(page([PENDING, APPROVED]));
  h.payoutAccounts.mockResolvedValue({ items: [account(1, 'Công ty Bãi Xanh')], missingOwnerProfileIds: [] });
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const openCreate = async () => {
  await screen.findByText('Chargeback BK-0002');
  fireEvent.click(screen.getByRole('button', { name: 'Create adjustment' }));
  return screen.getByRole('dialog', { name: 'Create adjustment' });
};

describe('AdjustmentsPage (US-080)', () => {
  it('lists adjustments, links the attached settlement and only offers allowed actions', async () => {
    renderPage(PATH, PATH, <AdjustmentsPage />);

    const pending = (await screen.findByText('Chargeback BK-0002')).closest('tr') as HTMLElement;
    expect(within(pending).getByText('−₫150.000')).toBeTruthy();
    expect(within(pending).getByRole('button', { name: 'Approve' })).toBeTruthy();
    const approved = screen.getByText('Commission over-charged').closest('tr') as HTMLElement;
    expect(within(approved).getByRole('link', { name: 'STL-20261102-0001' }).getAttribute('href')).toBe('/admin/finance/settlements?settlement=5');
    expect(within(approved).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(within(approved).getByRole('button', { name: 'Reverse' })).toBeTruthy();
    expect(h.adjustments).toHaveBeenCalledWith({ status: null, attached: null, ownerProfileId: null, search: null }, 1, 20);
  });

  it('filters live on the URL', async () => {
    renderPage(PATH, `${PATH}?attached=false&owner=1`, <AdjustmentsPage />);
    await waitFor(() => expect(h.adjustments).toHaveBeenCalledWith({ status: null, attached: false, ownerProfileId: 1, search: null }, 1, 20));

    fireEvent.click(await screen.findByRole('button', { name: /Pending approval/ }));
    await waitFor(() => expect(location()).toContain('status=PendingApproval'));
    await waitFor(() => expect(h.adjustments).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'PendingApproval' }), 1, 20));
  });

  it('create: reason is required and amount must not be 0', async () => {
    renderPage(PATH, PATH, <AdjustmentsPage />);
    const d = await openCreate();

    fireEvent.click(within(d).getByRole('button', { name: 'Create adjustment' }));
    expect(await within(d).findByText('Enter the owner profile ID.')).toBeTruthy();
    expect(within(d).getByText('A reason is required.')).toBeTruthy();

    fireEvent.change(within(d).getByLabelText(/Amount/), { target: { value: '0' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Create adjustment' }));
    expect(await within(d).findByText('Amount must not be 0.')).toBeTruthy();
    expect(h.createAdjustment).not.toHaveBeenCalled();
  });

  it('create: sends the adjustment and shows the owner name looked up from AdminService', async () => {
    h.createAdjustment.mockResolvedValue(adjustment({ id: 13 }));
    renderPage(PATH, PATH, <AdjustmentsPage />);
    const d = await openCreate();

    fireEvent.change(within(d).getByLabelText(/Owner profile ID/), { target: { value: '1' } });
    expect(await within(d).findByText('Công ty Bãi Xanh')).toBeTruthy();
    fireEvent.change(within(d).getByLabelText(/Amount/), { target: { value: '-150000' } });
    fireEvent.change(within(d).getByLabelText(/Reason/), { target: { value: ' Chargeback VNPay BK-0002 ' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Create adjustment' }));

    await waitFor(() =>
      expect(h.createAdjustment).toHaveBeenCalledWith({
        ownerProfileId: 1, amount: -150_000, reason: 'Chargeback VNPay BK-0002', sanctionId: null, reversesAdjustmentId: null,
      }));
    expect(await screen.findByText('Adjustment #13 created')).toBeTruthy();
  });

  it('reverse prefills the opposite amount and links the original', async () => {
    h.createAdjustment.mockResolvedValue(adjustment({ id: 14, amount: -20_000, reversesAdjustmentId: 12 }));
    renderPage(PATH, PATH, <AdjustmentsPage />);

    fireEvent.click(within((await screen.findByText('Commission over-charged')).closest('tr') as HTMLElement).getByRole('button', { name: 'Reverse' }));
    const d = screen.getByRole('dialog', { name: 'Reverse adjustment #12' });
    expect((within(d).getByLabelText(/Amount/) as HTMLInputElement).value).toBe('-20000');
    fireEvent.click(within(d).getByRole('button', { name: 'Create adjustment' }));

    await waitFor(() => expect(h.createAdjustment).toHaveBeenCalledWith(expect.objectContaining({ amount: -20_000, reversesAdjustmentId: 12 })));
  });

  it('approve sends the row version; 403 (own adjustment) is shown in the dialog', async () => {
    h.approveAdjustment.mockRejectedValueOnce(new AdminApiError(403, 'Người tạo bút toán #11 không được tự duyệt – cần một Admin khác.'));
    h.approveAdjustment.mockResolvedValueOnce(adjustment({ status: 'Approved' }));
    renderPage(PATH, PATH, <AdjustmentsPage />);

    fireEvent.click(within((await screen.findByText('Chargeback BK-0002')).closest('tr') as HTMLElement).getByRole('button', { name: 'Approve' }));
    const d = screen.getByRole('dialog', { name: /Approve adjustment #11/ });
    fireEvent.click(within(d).getByRole('button', { name: 'Approve' }));
    expect(await within(d).findByText(/không được tự duyệt/)).toBeTruthy();

    fireEvent.click(within(d).getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(h.approveAdjustment).toHaveBeenLastCalledWith(11, 'arv1'));
    expect(await screen.findByText('Adjustment approved')).toBeTruthy();
  });

  it('reject requires a reason', async () => {
    h.rejectAdjustment.mockResolvedValue(adjustment({ status: 'Rejected' }));
    renderPage(PATH, PATH, <AdjustmentsPage />);

    fireEvent.click(within((await screen.findByText('Chargeback BK-0002')).closest('tr') as HTMLElement).getByRole('button', { name: 'Reject' }));
    const d = screen.getByRole('dialog', { name: /Reject adjustment #11/ });
    fireEvent.click(within(d).getByRole('button', { name: 'Reject' }));
    expect(await within(d).findByText('A reason is required.')).toBeTruthy();
    fireEvent.change(within(d).getByRole('textbox'), { target: { value: 'Duplicate of #5' } });
    fireEvent.click(within(d).getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(h.rejectAdjustment).toHaveBeenCalledWith(11, 'arv1', 'Duplicate of #5'));
  });
});
