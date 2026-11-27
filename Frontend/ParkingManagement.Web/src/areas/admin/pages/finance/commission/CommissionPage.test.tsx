import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminCommissionSummaryDto, CommissionRateListDto } from '@/types/adminCommission';
import { CommissionPage } from './CommissionPage';

// ---- Mock tầng API (không có số mẫu trong component: mọi số trong test đến từ fixture này) ----
const h = vi.hoisted(() => ({ allRates: vi.fn(), summary: vi.fn() }));
vi.mock('@/api/adminCommissionApi', () => ({ adminCommissionApi: { allRates: h.allRates, summary: h.summary } }));

const NOW = new Date('2026-11-06T03:00:00Z'); // 10:00 ICT ngày 06/11/2026

const rates = (patch: Partial<CommissionRateListDto> = {}): CommissionRateListDto => ({
  asOfUtc: '2026-11-06T03:00:00Z',
  defaultRate: 0.1,
  defaultEffectiveFromUtc: null,
  defaultNextChange: { rate: 0.12, effectiveFromUtc: '2026-11-20T01:00:00Z' },
  partial: false,
  partialReason: null,
  lots: {
    items: [
      {
        parkingLotId: 11, lotName: 'Bãi Xanh Ba Đình', district: 'Ba Đình', city: 'Hà Nội', lotStatus: 'Active', ownerProfileId: 30,
        ownerName: 'Công ty Bãi Xanh', currentRate: 0.075, source: 'LotContract', reason: null, contractId: 5, contractNumber: 'HD-SP-2026-0011',
        contractVersion: 2, effectiveFrom: '2026-10-01', effectiveTo: '2027-09-30',
      },
      {
        parkingLotId: 12, lotName: 'Vincom Bà Triệu', district: 'Hai Bà Trưng', city: 'Hà Nội', lotStatus: 'Active', ownerProfileId: null,
        ownerName: null, currentRate: 0.1, source: 'DefaultConfig', reason: 'NoSignedContract', contractId: null, contractNumber: null,
        contractVersion: null, effectiveFrom: null, effectiveTo: null,
      },
    ],
    page: 1, pageSize: 2, totalCount: 2, totalPages: 1,
  },
  ...patch,
});

const totals = (gross: number, refunded: number, commission: number, count: number, pending: number) => ({
  grossAmount: gross, refundedAmount: refunded, netAmount: gross - refunded, commissionAmount: commission,
  effectiveRate: gross - refunded > 0 ? Math.round((commission / (gross - refunded)) * 1e6) / 1e6 : null,
  transactionCount: count, pendingCount: pending, status: pending === 0 ? 'Calculated' : pending >= count ? 'Pending' : 'Partial',
} as const);

const summary = (patch: Partial<AdminCommissionSummaryDto> = {}): AdminCommissionSummaryDto => ({
  asOfUtc: '2026-11-06T03:00:00Z',
  fromUtc: '2026-10-31T17:00:00.000Z',
  toUtc: '2026-11-06T17:00:00.000Z',
  currency: 'VND',
  totals: totals(250_000, 20_000, 13_750, 4, 1),
  byLot: [
    { parkingLotId: 11, ownerProfileId: 30, rateSources: ['LotContract'], ...totals(150_000, 20_000, 9_750, 2, 0) },
    { parkingLotId: 12, ownerProfileId: 31, rateSources: ['DefaultConfig'], ...totals(100_000, 0, 4_000, 2, 1) },
  ],
  byOwner: [
    { ownerProfileId: 30, lotCount: 1, ...totals(150_000, 20_000, 9_750, 2, 0) },
    { ownerProfileId: 31, lotCount: 1, ...totals(100_000, 0, 4_000, 2, 1) },
  ],
  byRateSource: [
    { source: 'DefaultConfig', transactionCount: 1, baseAmount: 40_000, refundedAmount: 0, commissionAmount: 4_000, effectiveRate: 0.1 },
    { source: 'LotContract', transactionCount: 2, baseAmount: 150_000, refundedAmount: 20_000, commissionAmount: 9_750, effectiveRate: 0.075 },
  ],
  series: [
    { date: '2026-11-01', grossAmount: 150_000, netAmount: 130_000, commissionAmount: 9_750, transactionCount: 2, pendingCount: 0 },
    { date: '2026-11-02', grossAmount: 100_000, netAmount: 100_000, commissionAmount: 4_000, transactionCount: 2, pendingCount: 1 },
  ],
  ...patch,
});

const LocationProbe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

const renderAt = (url = '/admin/finance/commission') =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        {(['lots', 'owners', 'sources'] as const).map((tab) => (
          <Route
            key={tab}
            path={`/admin/finance/commission${tab === 'lots' ? '' : `/${tab}`}`}
            element={
              <>
                <CommissionPage tab={tab} />
                <LocationProbe />
              </>
            }
          />
        ))}
      </Routes>
    </MemoryRouter>
  );

const location = () => screen.getByTestId('location').textContent ?? '';
const kpi = (label: string) => screen.getByText(label, { selector: 'span' }).closest('div') as HTMLElement;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(NOW);
  h.allRates.mockReset().mockResolvedValue(rates());
  h.summary.mockReset().mockResolvedValue(summary());
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('CommissionPage (US-079)', () => {
  it('gọi PaymentService theo kỳ "Tháng này" (ICT) và hiển thị KPI từ API', async () => {
    renderAt();

    await waitFor(() => expect(h.summary).toHaveBeenCalled());
    const [fromUtc, toUtc, signal] = h.summary.mock.calls[0];
    expect([fromUtc, toUtc]).toEqual(['2026-10-31T17:00:00.000Z', '2026-11-06T17:00:00.000Z']);   // 01/11 → hết 06/11 ICT
    expect(signal).toBeInstanceOf(AbortSignal);

    await waitFor(() => expect(within(kpi('GMV')).getByText('₫250.000')).toBeTruthy());
    expect(within(kpi('Đã hoàn')).getByText('₫20.000')).toBeTruthy();
    expect(within(kpi('Doanh thu thuần')).getByText('₫230.000')).toBeTruthy();
    await waitFor(() => expect(within(kpi('Hoa hồng')).getByText('₫13.750')).toBeTruthy());
    expect(within(kpi('Tỷ lệ hiệu dụng')).getByText('5,98%')).toBeTruthy();
    expect(screen.getByText(/01\/11\/2026 – 06\/11\/2026/)).toBeTruthy();
  });

  it('giao dịch chờ tính > 0 hiện cảnh báo, không hiển thị như 0', async () => {
    renderAt();

    const tile = await screen.findByTestId('kpi-pending');
    await waitFor(() => expect(tile.getAttribute('data-tone')).toBe('warning'));
    expect(tile.getAttribute('title')).toBe('Đang chờ job tính, không phải 0');
    await waitFor(() => expect(within(tile).getByText('1')).toBeTruthy());           // đếm lên xong (count-up)
    expect(within(kpi('Hoa hồng')).getByText(/Chưa đủ · còn 1 giao dịch chờ tính/)).toBeTruthy();
    const vincom = (await screen.findByText('Vincom Bà Triệu')).closest('tr') as HTMLElement;
    expect(within(vincom).getByText('1 chờ tính')).toBeTruthy();
  });

  it('bảng theo bãi ghép tỷ lệ (AdminService) với số hoa hồng (PaymentService), link hợp đồng đúng URL', async () => {
    renderAt();

    const row = (await screen.findByText('Bãi Xanh Ba Đình')).closest('tr') as HTMLElement;
    expect(within(row).getAllByText('7,5%')).toHaveLength(2);                          // tỷ lệ hiện tại + tỷ lệ hiệu dụng
    expect(within(row).getByText('Hợp đồng')).toBeTruthy();
    await waitFor(() => expect(within(row).getByText('₫9.750')).toBeTruthy());
    expect(within(row).getByText('Công ty Bãi Xanh')).toBeTruthy();
    const link = within(row).getByRole('link', { name: /HD-SP-2026-0011 · v2/ });
    expect(link.getAttribute('href')).toBe('/admin/operations/lot-contracts?q=HD-SP-2026-0011');

    const vincom = screen.getByText('Vincom Bà Triệu').closest('tr') as HTMLElement;
    expect(within(vincom).getByText('Mặc định')).toBeTruthy();
    expect(within(vincom).getByText('Chưa có hợp đồng đã ký')).toBeTruthy();
    expect(within(vincom).getByText('OwnerProfile #31')).toBeTruthy();                 // chủ bãi lấy từ sổ hoa hồng
  });

  it('banner tỷ lệ mặc định + thay đổi đã lên lịch + link đổi tỷ lệ', async () => {
    renderAt();

    const banner = (await screen.findByText(/Tỷ lệ mặc định hiện tại/)).closest('[role="status"]') as HTMLElement;
    await waitFor(() => expect(within(banner).getByText('10%')).toBeTruthy());
    expect(within(banner).getByText('12%')).toBeTruthy();
    expect(banner.textContent).toContain('20/11/2026 08:00 ICT');
    expect(within(banner).getByRole('link', { name: 'Đổi tỷ lệ mặc định' }).getAttribute('href')).toBe(
      '/admin/access/system-settings/DEFAULT_COMMISSION_RATE'
    );
  });

  it('đổi kỳ → URL đổi và gọi lại PaymentService với kỳ mới', async () => {
    renderAt();
    await waitFor(() => expect(h.summary).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText('Kỳ'), { target: { value: 'prev-month' } });

    await waitFor(() => expect(location()).toContain('period=prev-month'));
    await waitFor(() => expect(h.summary).toHaveBeenCalledTimes(2));
    expect(h.summary.mock.calls[1].slice(0, 2)).toEqual(['2026-09-30T17:00:00.000Z', '2026-10-31T17:00:00.000Z']);
  });

  it('khôi phục bộ lọc từ URL (chia sẻ / F5) và gọi AdminService với tìm kiếm + nguồn tỷ lệ', async () => {
    renderAt('/admin/finance/commission?period=7d&q=xanh&source=LotContract');

    await waitFor(() => expect(h.allRates).toHaveBeenCalled());
    expect(h.allRates.mock.calls[0][0]).toEqual({ search: 'xanh', source: 'LotContract' });
    expect(h.summary.mock.calls[0].slice(0, 2)).toEqual(['2026-10-30T17:00:00.000Z', '2026-11-06T17:00:00.000Z']);   // 31/10 → 06/11
  });

  it('đổi nguồn tỷ lệ → URL đổi và gọi lại AdminService', async () => {
    renderAt();
    await waitFor(() => expect(h.allRates).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText('Nguồn tỷ lệ'), { target: { value: 'DefaultConfig' } });

    await waitFor(() => expect(location()).toContain('source=DefaultConfig'));
    await waitFor(() => expect(h.allRates).toHaveBeenCalledTimes(2));
    expect(h.allRates.mock.calls[1][0]).toEqual({ search: undefined, source: 'DefaultConfig' });
  });

  it('kỳ tùy chọn sai → báo lỗi, không gọi PaymentService', async () => {
    renderAt('/admin/finance/commission?period=custom&from=2026-11-05&to=2026-11-01');

    expect(await screen.findByText(/Ngày bắt đầu phải trước hoặc bằng ngày kết thúc/)).toBeTruthy();
    await waitFor(() => expect(h.allRates).toHaveBeenCalled());
    expect(h.summary).not.toHaveBeenCalled();
  });

  it('PaymentService lỗi: vẫn hiện bảng tỷ lệ, cột tiền "—", có nút Thử lại', async () => {
    h.summary.mockRejectedValueOnce(new AdminApiError(500, 'PaymentService lỗi nội bộ.')).mockResolvedValue(summary());
    renderAt();

    const banner = (await screen.findByText(/Không tải được số hoa hồng \(PaymentService\)/)).closest('[role="alert"]') as HTMLElement;
    const row = screen.getByText('Bãi Xanh Ba Đình').closest('tr') as HTMLElement;
    expect(within(row).getByText('7,5%')).toBeTruthy();                                // chỉ tỷ lệ hiện tại – hiệu dụng là "—"
    expect(within(row).getAllByText('—').length).toBeGreaterThan(0);

    fireEvent.click(within(banner).getByRole('button', { name: 'Thử lại' }));
    await waitFor(() => expect(h.summary).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(within(screen.getByText('Bãi Xanh Ba Đình').closest('tr') as HTMLElement).getByText('₫9.750')).toBeTruthy());
  });

  it('AdminService lỗi: vẫn hiện số hoa hồng theo bãi ("Bãi #id"), có nút Thử lại', async () => {
    h.allRates.mockRejectedValue(new AdminApiError(503, 'AdminService chưa sẵn sàng.'));
    renderAt();

    expect(await screen.findByText(/Không tải được tỷ lệ hoa hồng \(AdminService\)/)).toBeTruthy();
    const row = (await screen.findByText('Bãi #11')).closest('tr') as HTMLElement;
    await waitFor(() => expect(within(row).getByText('₫9.750')).toBeTruthy());
    await waitFor(() => expect(within(kpi('Hoa hồng')).getByText('₫13.750')).toBeTruthy());
  });

  it('bãi có giao dịch nhưng không có trong danh sách tỷ lệ vẫn hiện, không ẩn', async () => {
    h.summary.mockResolvedValue(summary({
      byLot: [...summary().byLot, { parkingLotId: 99, ownerProfileId: 40, rateSources: ['DefaultConfig'], ...totals(10_000, 0, 1_000, 1, 0) }],
    }));
    renderAt();

    const row = (await screen.findByText('Bãi #99')).closest('tr') as HTMLElement;
    expect(within(row).getByText('Không có trong danh sách tỷ lệ')).toBeTruthy();
    expect(within(row).getByText('Mặc định')).toBeTruthy();
  });

  it('ParkingService thiếu → banner partial', async () => {
    h.allRates.mockResolvedValue(rates({ partial: true, partialReason: 'ParkingService không phản hồi kịp (3 giây).' }));
    renderAt();
    expect(await screen.findByText(/Thiếu thông tin một số bãi từ ParkingService/)).toBeTruthy();
  });

  it('kỳ không có giao dịch → empty state', async () => {
    h.summary.mockResolvedValue(summary({ totals: totals(0, 0, 0, 0, 0), byLot: [], byOwner: [], byRateSource: [], series: [] }));
    renderAt();
    expect(await screen.findByText('Không có giao dịch trong kỳ')).toBeTruthy();
  });

  it('tab Theo chủ bãi: link đối tác đúng URL, đổi tab giữ bộ lọc', async () => {
    renderAt('/admin/finance/commission?period=7d');

    fireEvent.click(await screen.findByRole('link', { name: 'Theo chủ bãi' }));
    await waitFor(() => expect(location()).toBe('/admin/finance/commission/owners?period=7d'));

    const link = await screen.findByRole('link', { name: 'Công ty Bãi Xanh' });
    expect(link.getAttribute('href')).toBe('/admin/operations/partner-onboarding?q=C%C3%B4ng%20ty%20B%C3%A3i%20Xanh');
    await waitFor(() => expect(within(link.closest('tr') as HTMLElement).getByText('₫9.750')).toBeTruthy());
  });

  it('tab Theo nguồn tỷ lệ: Hợp đồng và Mặc định', async () => {
    renderAt('/admin/finance/commission/sources');

    const contract = (await screen.findByText('LotContract')).closest('tr') as HTMLElement;
    await waitFor(() => expect(within(contract).getByText('₫9.750')).toBeTruthy());
    expect(within(contract).getByText('7,5%')).toBeTruthy();
    const fallback = screen.getByText('DefaultConfig').closest('tr') as HTMLElement;
    expect(within(fallback).getByText('₫4.000')).toBeTruthy();
  });

  it('xuất CSV phía client: UTF-8 BOM, đúng cột, tên file theo kỳ', async () => {
    const blobs: Blob[] = [];
    let fileName = '';
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: (b: Blob) => (blobs.push(b), 'blob:commission') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: () => undefined });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      fileName = this.download;
    });
    renderAt();
    await waitFor(() => expect(within(screen.getByText('Bãi Xanh Ba Đình').closest('tr') as HTMLElement).getByText('₫9.750')).toBeTruthy());

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Xuất CSV dữ liệu tổng hợp đang hiển thị' }));
    });

    expect(fileName).toBe('commission_2026-11-01_2026-11-06.csv');
    const bytes = new Uint8Array(await blobs[0].arrayBuffer());
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf]);
    const lines = new TextDecoder().decode(bytes.slice(3)).split('\r\n');
    expect(lines[0]).toBe(
      'ParkingLotId,Lot,District,OwnerProfileId,Owner,CurrentRate,RateSource,RateReason,ContractNumber,ContractVersion,Gross,Refunded,Net,Commission,EffectiveRate,Transactions,Pending'
    );
    expect(lines[1]).toBe('11,Bãi Xanh Ba Đình,Ba Đình,30,Công ty Bãi Xanh,0.075,LotContract,,HD-SP-2026-0011,2,150000,20000,130000,9750,0.075,2,0');
    expect(lines[2]).toBe('12,Vincom Bà Triệu,Hai Bà Trưng,31,,0.1,DefaultConfig,NoSignedContract,,,100000,0,100000,4000,0.04,2,1');
    expect(lines.join('\n')).not.toMatch(/userId|code/i);                                 // không có dữ liệu từng khách
  });
});
