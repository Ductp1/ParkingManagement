import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminCommissionByLotDto, AdminCommissionDayDto, AdminCommissionSummaryDto, AdminCommissionTotalsDto } from '@/types/adminCommission';
import { AdminToastProvider } from '../../components/AdminToastProvider';
import { RevenueReportPage } from './RevenueReportPage';
import { RevenueDailyChart } from './RevenueDailyChart';
import { resolveRevenueRange, share } from './revenuePeriod';

const h = vi.hoisted(() => ({ summary: vi.fn(), adminRequest: vi.fn() }));
vi.mock('@/api/adminCommissionApi', () => ({ adminCommissionApi: { summary: h.summary } }));
vi.mock('@/api/adminClientApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/adminClientApi')>()),
  adminRequest: h.adminRequest,
}));

const PATH = '/admin/reports/revenue';

const totals = (patch: Partial<AdminCommissionTotalsDto> = {}): AdminCommissionTotalsDto => ({
  grossAmount: 113_000, refundedAmount: 13_000, netAmount: 100_000, commissionAmount: 7_500, effectiveRate: 0.075,
  transactionCount: 2, pendingCount: 0, status: 'Calculated', ...patch,
});

const day = (date: string, grossAmount = 0, transactionCount = 0): AdminCommissionDayDto => ({
  date, grossAmount, netAmount: grossAmount, commissionAmount: 0, transactionCount, pendingCount: 0,
});

/** Đủ 13 ngày 01–13/11/2026 (API trả cả ngày bằng 0). */
const series = (): AdminCommissionDayDto[] =>
  Array.from({ length: 13 }, (_, i) => {
    const date = `2026-11-${String(i + 1).padStart(2, '0')}`;
    return date === '2026-11-08' ? day(date, 50_000, 1) : date === '2026-11-10' ? day(date, 63_000, 1) : day(date);
  });

const byLot = (parkingLotId: number, grossAmount: number, patch: Partial<AdminCommissionByLotDto> = {}): AdminCommissionByLotDto => ({
  ...totals({ grossAmount, netAmount: grossAmount, refundedAmount: 0, transactionCount: 1 }), parkingLotId, ownerProfileId: 1, rateSources: [], ...patch,
});

const summary = (patch: Partial<AdminCommissionSummaryDto> = {}): AdminCommissionSummaryDto => ({
  asOfUtc: '2026-11-13T03:15:00Z', fromUtc: '2026-10-31T17:00:00Z', toUtc: '2026-11-13T17:00:00Z', currency: 'VND',
  totals: totals(), byLot: [byLot(2, 63_000), byLot(1, 50_000)], byOwner: [], byRateSource: [], series: series(), ...patch,
});

const Probe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

const renderAt = (url = PATH) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path={PATH} element={<><RevenueReportPage /><Probe /></>} />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

const location = () => screen.getByTestId('location').textContent ?? '';
/** Ô KPI theo nhãn (span; bảng sr-only của biểu đồ cũng có cột "Gross revenue"). */
const kpi = (label: string) => screen.getByText(label, { selector: 'span' }).parentElement as HTMLElement;

beforeEach(() => {
  // 13/11/2026 10:00 ICT.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-13T03:00:00Z'));
  Object.values(h).forEach((fn) => fn.mockReset());
  h.summary.mockResolvedValue(summary());
  h.adminRequest.mockResolvedValue({ lots: [{ parkingLotId: 2, name: 'Bãi Sân bay' }] });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('RevenueReportPage (P0-5, US-081)', () => {
  it('loads this month in ICT and shows the four KPIs with real ratios', async () => {
    renderAt();

    await waitFor(() => expect(within(kpi('Gross revenue')).getByText('₫113.000')).toBeTruthy());
    expect(h.summary).toHaveBeenCalledWith('2026-10-31T17:00:00.000Z', '2026-11-13T17:00:00.000Z');
    expect(within(kpi('Gross revenue')).getByText('2 payments')).toBeTruthy();
    expect(within(kpi('Platform commission')).getByText('₫7.500')).toBeTruthy();
    expect(within(kpi('Platform commission')).getByText('7,5% of net revenue')).toBeTruthy();
    expect(within(kpi('Refunds')).getByText('₫13.000')).toBeTruthy();
    expect(within(kpi('Refunds')).getByText('11,5% of gross')).toBeTruthy();
    expect(within(kpi('Succeeded payments')).getByText('Net revenue ₫100.000')).toBeTruthy();
    expect(screen.getByText('01/11/2026 – 13/11/2026 (ICT) · updated 10:15 ICT')).toBeTruthy();
    expect(screen.queryByText(/Data source/)).toBeNull();
    expect(screen.queryByRole('button', { name: /Export/ })).toBeNull();
  });

  it('marks commission as not final while payments are still being calculated', async () => {
    h.summary.mockResolvedValue(summary({ totals: totals({ commissionAmount: 0, pendingCount: 2, status: 'Pending', effectiveRate: 0 }) }));
    renderAt();

    expect(await screen.findByText('Not final · 2 payments not calculated yet')).toBeTruthy();
    expect(screen.queryByText('0% of net revenue')).toBeNull();
  });

  it('draws one bar per day of the period with an accessible table', async () => {
    renderAt();

    await screen.findByRole('img', { name: 'Gross revenue by day, 13 days, total ₫113.000' });
    expect(screen.getAllByTestId('revenue-bar')).toHaveLength(13);
    const table = screen.getByRole('table', { name: 'Gross revenue by day' });
    expect(within(table).getByText('08/11/2026').closest('tr')?.textContent).toContain('₫50.000');
  });

  it('lists top lots by gross with names, falling back to "Lot #id", linking to the lot', async () => {
    renderAt();

    const panel = (await screen.findByText('Top lots by gross revenue')).closest('section') as HTMLElement;
    await within(panel).findByRole('link', { name: 'Bãi Sân bay' }); // tên bãi tải sau số liệu doanh thu
    const links = within(panel).getAllByRole('link');
    expect(links.map((l) => l.textContent)).toEqual(['Bãi Sân bay', 'Lot #1']);
    expect(links[0].getAttribute('href')).toBe('/admin/operations/parking-lots/2');
  });

  it('switches to last month on the URL and reloads that range', async () => {
    renderAt();
    await waitFor(() => expect(h.summary).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText('Period'), { target: { value: 'prev-month' } });

    await waitFor(() => expect(location()).toBe(`${PATH}?period=prev-month`));
    await waitFor(() => expect(h.summary).toHaveBeenLastCalledWith('2026-09-30T17:00:00.000Z', '2026-10-31T17:00:00.000Z'));
  });

  it('does not call the API for an invalid custom range and explains why', async () => {
    renderAt(`${PATH}?period=custom&from=2026-11-10&to=2026-11-01`);

    expect(await screen.findByText('The start date must be on or before the end date.')).toBeTruthy();
    expect(h.summary).not.toHaveBeenCalled();
    expect(screen.getByLabelText('From')).toHaveProperty('value', '2026-11-10');
  });

  it('shows real zero KPIs and an empty chart when the period has no payments', async () => {
    h.summary.mockResolvedValue(summary({ totals: totals({ grossAmount: 0, refundedAmount: 0, netAmount: 0, commissionAmount: 0, effectiveRate: null, transactionCount: 0 }), byLot: [] }));
    renderAt();

    expect(await screen.findByText('No payments in this period')).toBeTruthy();
    expect(within(kpi('Gross revenue')).getByText('₫0')).toBeTruthy();
    expect(screen.queryByText('Top lots by gross revenue')).toBeNull();
    expect(screen.queryByText(/of gross/)).toBeNull();
  });

  it('shows the server message on error and retries', async () => {
    h.summary.mockRejectedValueOnce(new AdminApiError(400, 'Khoảng thời gian tối đa 366 ngày.', { title: 'x', detail: 'Khoảng thời gian tối đa 366 ngày.' }));
    renderAt();

    expect(await screen.findByText('Could not load revenue')).toBeTruthy();
    expect(screen.getByText('Khoảng thời gian tối đa 366 ngày.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(within(kpi('Gross revenue')).getByText('₫113.000')).toBeTruthy());
  });

  it('shows the no-permission state on 403', async () => {
    h.summary.mockRejectedValue(new AdminApiError(403, 'Forbidden'));
    renderAt();
    expect(await screen.findByText('Only administrators can view platform revenue.')).toBeTruthy();
  });
});

describe('RevenueDailyChart', () => {
  it('does not draw bars taller than zero for empty days', () => {
    render(<RevenueDailyChart days={[day('2026-11-01'), day('2026-11-02', 10_000, 1)]} />);
    const bars = screen.getAllByTestId('revenue-bar').map((b) => (b.firstElementChild as HTMLElement).style.height);
    expect(bars).toEqual(['0%', '100%']);
  });
});

describe('revenuePeriod', () => {
  const now = new Date('2026-11-13T03:00:00Z');

  it('resolves presets in ICT', () => {
    const week = resolveRevenueRange('7d', '', '', now);
    expect(week.ok && [week.range.from, week.range.to]).toEqual(['2026-11-07', '2026-11-13']);
  });

  it('explains invalid custom ranges in English', () => {
    expect(resolveRevenueRange('custom', '', '', now)).toMatchObject({ ok: false, error: 'Pick a valid start and end date.' });
    expect(resolveRevenueRange('custom', '2025-01-01', '2026-11-01', now)).toMatchObject({ ok: false, error: 'Pick a range of at most 366 days.' });
  });

  it('computes shares only with a non-zero base', () => {
    expect(share(13_000, 113_000)).toBeCloseTo(11.504, 2);
    expect(share(1, 0)).toBeNull();
  });
});
