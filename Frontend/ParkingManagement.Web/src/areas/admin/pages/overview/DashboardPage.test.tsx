import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { DashboardPage } from './DashboardPage';
import type { DashboardOverviewDto, DashboardWidget } from './dashboardTypes';

// ---- Mock API (không có số mẫu trong UI: mọi số trong test đến từ fixture này) ----
const h = vi.hoisted(() => ({ overview: vi.fn() }));
vi.mock('./adminDashboardApi', () => ({ adminDashboardApi: { overview: h.overview } }));

const AS_OF = '2026-11-03T07:05:00Z'; // 14:05 ICT

const ok = <T,>(value: T, source: string): DashboardWidget<T> => ({
  status: 'available',
  value,
  asOfUtc: AS_OF,
  reason: null,
  source,
  detail: null,
});

const noSource = <T,>(source: string, detail = `${source} has no source yet.`): DashboardWidget<T> => ({
  status: 'unavailable',
  value: null,
  asOfUtc: null,
  reason: 'no_source',
  source,
  detail,
});

const sourceError = <T,>(source: string, detail = `${source} returned HTTP 500.`): DashboardWidget<T> => ({
  status: 'unavailable',
  value: null,
  asOfUtc: null,
  reason: 'source_error',
  source,
  detail,
});

const overview = (patch: (o: DashboardOverviewDto) => void = () => undefined): DashboardOverviewDto => {
  const o: DashboardOverviewDto = {
    generatedAtUtc: '2026-11-03T07:05:30Z',
    timeZone: 'Asia/Ho_Chi_Minh',
    todayFromUtc: '2026-11-02T17:00:00Z',
    todayToUtc: '2026-11-03T17:00:00Z',
    kpis: {
      revenueToday: noSource('PaymentService'),
      activeSessions: ok({ total: 1842, walkIn: 342, booked: 1500, abnormal: 3 }, 'GateService'),
      bookingsToday: noSource('BookingService'),
      openDisputes: ok({ open: 2, awaitingOwner: 3, ownerResponded: 1, escalated: 1, unresolved: 7 }, 'SupportService'),
    },
    queues: {
      kybApplications: ok({ submitted: 12, underReview: 2, needsMoreInfo: 5, toReview: 14 }, 'ParkingService'),
      partnerApplications: ok({ submitted: 7, underReview: 2, needsMoreInfo: 1, accountIssued: 4, toReview: 9 }, 'AdminService'),
      lotContractsToSign: ok({ pending: 0 }, 'AdminService'),
      riskFlags: ok({ open: 2, investigating: 1, critical: 1, warning: 1, info: 1, overdue: 0, unresolved: 3 }, 'AdminService'),
      refundsRequested: noSource('PaymentService'),
      settlementsPendingReview: noSource('PaymentService'),
      disputesAwaitingOwner: ok({ overdue: 2, awaitingOwner: 3 }, 'SupportService'),
      dataSubjectRequestsDue: noSource('UserService'),
    },
    governance: {
      activeSanctions: ok({ total: 3, warning: 1, rankingPenalty: 0, temporarySuspension: 1, permanentBan: 1 }, 'AdminService'),
      scheduledConfigChanges: ok({ count: 1, nextKey: 'HOLD_TTL_MINUTES', nextEffectiveFromUtc: '2026-11-04T01:00:00Z' }, 'AdminService'),
      auditLogsToday: ok({ total: 9, bySource: [{ sourceService: 'AdminService', count: 6 }, { sourceService: 'GateService', count: 3 }] }, 'AdminService'),
    },
    tables: {
      lotOccupancy: ok(
        {
          totalActive: 920,
          totalCapacity: 1920,
          occupancyPercent: 47.9,
          lots: [
            {
              parkingLotId: 2, name: 'Bitexco Financial Tower', city: 'TP.HCM', district: 'Quận 1', lotStatus: 'Active', integrationTier: 'PmsApi',
              activeSessions: 388, capacity: 420, capacitySource: 'gate', occupancyPercent: 92.4, isEmergencyStopped: false, instantBookingPaused: true,
            },
            {
              parkingLotId: 1, name: 'Vincom Landmark 81', city: 'TP.HCM', district: 'Bình Thạnh', lotStatus: 'Active', integrationTier: 'EdgeIot',
              activeSessions: 412, capacity: 500, capacitySource: 'gate', occupancyPercent: 82.4, isEmergencyStopped: false, instantBookingPaused: false,
            },
            {
              parkingLotId: 5, name: 'Tân Sơn Nhất P2', city: 'TP.HCM', district: 'Tân Bình', lotStatus: 'Suspended', integrationTier: 'EdgeIot',
              activeSessions: 120, capacity: 1000, capacitySource: 'parking', occupancyPercent: 12, isEmergencyStopped: true, instantBookingPaused: false,
            },
          ],
          byDistrict: [
            { city: 'TP.HCM', district: 'Quận 1', lotCount: 1, activeSessions: 388, capacity: 420, occupancyPercent: 92.4 },
            { city: 'TP.HCM', district: 'Bình Thạnh', lotCount: 1, activeSessions: 412, capacity: 500, occupancyPercent: 82.4 },
            { city: 'TP.HCM', district: 'Tân Bình', lotCount: 1, activeSessions: 120, capacity: 1000, occupancyPercent: 12 },
          ],
        },
        'GateService + ParkingService'
      ),
      gateDevices: ok(
        {
          online: 86, offline: 1, maintenance: 1, total: 88,
          byType: [
            { deviceType: 'AnprCamera', online: 46, offline: 1, maintenance: 0, total: 47 },
            { deviceType: 'Barrier', online: 40, offline: 0, maintenance: 1, total: 41 },
          ],
        },
        'GateService'
      ),
      paymentsByMethodToday: noSource('PaymentService'),
    },
  };
  patch(o);
  return o;
};

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};

const renderPage = () =>
  render(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>
  );

const kpi = (label: string) => screen.getByTestId(`kpi-${label}`);
const queue = (label: string) => screen.getByTestId(`queue-${label}`);

beforeEach(() => {
  vi.clearAllMocks();
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' });
  h.overview.mockResolvedValue(overview());
});

// RTL auto-cleanup chỉ hoạt động với test globals → chủ động dọn DOM giữa các test.
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe('DashboardPage (US-081) – trạng thái từng widget', () => {
  it('available: hiện số thật (vi-VN) + "Updated HH:mm" ICT', async () => {
    renderPage();

    await waitFor(() => expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy());
    const sessions = kpi('Active parking sessions');
    expect(within(sessions).getByText('Abnormal 3')).toBeTruthy();
    expect(within(sessions).getByText(/Walk-in 342 · Booked 1\.500/)).toBeTruthy();
    expect(within(sessions).getByText('Updated 14:05')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('queue-value-KYB applications to review').textContent).toBe('14'));
    expect(screen.getByText(/03\/11\/2026 \(ICT\)/)).toBeTruthy();
  });

  it('"0 thật" là số 0 có nguồn, không phải "—"', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByTestId('queue-value-Lot contracts to sign').textContent).toBe('0'));
  });

  it('no_source: widget bị ẩn hẳn – không "—", không 0, không ghi chú nguồn', async () => {
    renderPage();
    await waitFor(() => expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy());

    expect(screen.queryByTestId('kpi-Revenue today (gross)')).toBeNull();
    expect(screen.queryByTestId('kpi-Bookings created today')).toBeNull();
    expect(screen.queryByTestId('queue-Settlements awaiting review')).toBeNull();
    expect(screen.queryByTestId('queue-Refunds awaiting approval')).toBeNull();
    expect(screen.queryByTestId('queue-Personal data requests')).toBeNull();
    // Bảng chưa có nguồn: cả panel bị ẩn.
    expect(screen.queryByText("Today's payments by method")).toBeNull();
    // Không lộ tên service / lý do kỹ thuật.
    expect(screen.queryByText(/PaymentService/)).toBeNull();
    expect(screen.queryByText(/has no source/)).toBeNull();
  });

  it('no_source cho cả nhóm → ẩn luôn khung chứa (KPI strip, panel hàng đợi)', async () => {
    h.overview.mockResolvedValueOnce(
      overview((o) => {
        o.kpis = {
          revenueToday: noSource('PaymentService'),
          activeSessions: noSource('GateService'),
          bookingsToday: noSource('BookingService'),
          openDisputes: noSource('SupportService'),
        };
        (Object.keys(o.queues) as Array<keyof typeof o.queues>).forEach((k) => {
          (o.queues as Record<string, DashboardWidget<unknown>>)[k] = noSource('AdminService');
        });
      })
    );
    renderPage();

    await screen.findByTestId('occupancy-lot');
    // Bốn KPI của kpis.* đều chưa có nguồn → ẩn; còn ô lấp đầy (nguồn bảng lấp đầy vẫn có).
    expect(screen.getAllByTestId(/^kpi-/).map((el) => el.getAttribute('data-testid'))).toEqual(['kpi-Current occupancy']);
    expect(screen.queryByText('Work queues')).toBeNull();
    expect(screen.getByText('Platform governance')).toBeTruthy();
  });

  it('source_error: vẫn hiện lỗi + "Retry" gọi lại toàn bộ overview (không bị ẩn)', async () => {
    h.overview.mockResolvedValueOnce(overview((o) => (o.tables.gateDevices = sourceError('GateService'))));
    renderPage();

    expect(await screen.findByText("Couldn't load gate devices")).toBeTruthy();
    expect(screen.queryByText('GateService returned HTTP 500.')).toBeNull();
    expect(h.overview).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(h.overview).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getByTestId('gate-devices')).toBeTruthy());
  });

  it('source_error ở dòng hàng đợi có nút "Retry" riêng, các widget khác vẫn có số', async () => {
    h.overview.mockResolvedValueOnce(overview((o) => (o.queues.kybApplications = sourceError('ParkingService'))));
    renderPage();

    const row = await screen.findByTestId('queue-KYB applications to review');
    await waitFor(() => expect(within(row).getByText("Couldn't load")).toBeTruthy());
    expect(screen.getByTestId('queue-value-KYB applications to review').textContent).toBe('—');
    expect(within(row).getByRole('button', { name: 'Retry loading KYB applications to review' })).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('queue-value-Partner applications').textContent).toBe('9')); // count-up từ 0
  });

  it('nguồn có rồi → widget đang ẩn tự hiện lại sau lần làm mới', async () => {
    renderPage();
    await waitFor(() => expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy());
    expect(screen.queryByTestId('kpi-Revenue today (gross)')).toBeNull();

    h.overview.mockResolvedValueOnce(
      overview((o) => (o.kpis.revenueToday = ok({ gross: 1_250_000, net: 1_200_000, refunded: 50_000, succeededCount: 12 }, 'PaymentService')))
    );
    fireEvent.click(screen.getByRole('button', { name: 'Refresh dashboard figures' }));

    await waitFor(() => expect(within(kpi('Revenue today (gross)')).getByText(/1\.250\.000/)).toBeTruthy());
    expect(within(kpi('Revenue today (gross)')).getByText(/12 payments/)).toBeTruthy();
  });
});

describe('DashboardPage (US-081) – bảng lấp đầy', () => {
  it('toggle By lot / By district, ≥ 90% cảnh báo, cờ vận hành', async () => {
    renderPage();
    const byLot = await screen.findByTestId('occupancy-lot');

    expect(within(byLot).getByText('92,4%')).toBeTruthy();
    expect(within(byLot).getByText('Near capacity')).toBeTruthy(); // chỉ bãi ≥ 90%
    expect(within(byLot).getAllByText('Near capacity')).toHaveLength(1);
    expect(within(byLot).getByText('Normal flow')).toBeTruthy(); // 82,4%
    expect(within(byLot).getByText('Instant booking paused')).toBeTruthy();
    expect(within(byLot).getByText('Emergency stop')).toBeTruthy();
    expect(within(byLot).getByText('Suspended')).toBeTruthy();
    expect(within(byLot).getByText(/388 \/ 420 slots/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'By lot' }).getAttribute('aria-pressed')).toBe('true');

    fireEvent.click(screen.getByRole('button', { name: 'By district' }));

    const byDistrict = screen.getByTestId('occupancy-district');
    expect(within(byDistrict).getByText('Tân Bình, TP.HCM')).toBeTruthy();
    expect(within(byDistrict).queryByText('Vincom Landmark 81')).toBeNull();
    expect(screen.getByRole('button', { name: 'By district' }).getAttribute('aria-pressed')).toBe('true');
  });

  it('sắp theo % lấp đầy: mặc định giảm dần, bấm để tăng dần', async () => {
    renderPage();
    const table = await screen.findByTestId('occupancy-lot');
    const names = () => within(table).getAllByRole('link').map((a) => a.textContent);

    expect(names()).toEqual(['Bitexco Financial Tower', 'Vincom Landmark 81', 'Tân Sơn Nhất P2']);
    fireEvent.click(screen.getByRole('button', { name: /Sort lowest first/ }));
    expect(names()).toEqual(['Tân Sơn Nhất P2', 'Vincom Landmark 81', 'Bitexco Financial Tower']);
  });

  it('cột tên bãi có độ rộng tối thiểu, không bị các cột cố định ép hẹp', async () => {
    renderPage();
    const table = await screen.findByTestId('occupancy-lot');
    const link = within(table).getByRole('link', { name: 'Bitexco Financial Tower' });
    expect(link.parentElement?.className).toContain('min-w-[200px]');
  });

  it('thiết bị cổng: Offline > 0 nổi bật', async () => {
    renderPage();
    const devices = await screen.findByTestId('gate-devices');

    expect(within(devices).getByText('ANPR camera')).toBeTruthy();
    expect(within(devices).getByText('46/47 online')).toBeTruthy();
    expect(within(devices).getByText('1 offline')).toBeTruthy(); // ANPR có 1 thiết bị mất kết nối
    expect(within(devices).getByText('1 maintenance')).toBeTruthy(); // Barrier có 1 thiết bị bảo trì
    expect(screen.getByText(/1 offline · total 88/)).toBeTruthy();
  });
});

describe('DashboardPage (US-081) – KPI lấp đầy và lối tắt', () => {
  it('KPI "Current occupancy": đang đỗ / sức chứa toàn sàn, % và chỗ còn trống từ cùng nguồn bảng lấp đầy', async () => {
    renderPage();
    const tile = await screen.findByTestId('kpi-Current occupancy');

    await waitFor(() => expect(within(tile).getByText('920 / 1.920')).toBeTruthy());
    expect(within(tile).getByText(/47,9% full · 1\.000 slots free/)).toBeTruthy();
    expect(within(tile).getByRole('link', { name: /View details/ }).getAttribute('href')).toBe('/admin/operations/parking-lots');
  });

  it('Quick actions chỉ dẫn tới trang admin đã có', async () => {
    renderPage();
    await screen.findByTestId('occupancy-lot');

    const hrefs = within(screen.getByRole('heading', { name: 'Quick actions' }).closest('section') as HTMLElement)
      .getAllByRole('link')
      .map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual([
      '/admin/operations/kyb-review?status=Submitted',
      '/admin/operations/partner-onboarding?status=Submitted',
      '/admin/support/sanctions',
      '/admin/communications/send-notification',
      '/admin/operations/gate-activity',
    ]);
  });
});

describe('DashboardPage (US-081) – drill-down', () => {
  it('link sang trang admin đã có, đúng bộ lọc; trang chưa có thì không link', async () => {
    renderPage();
    await screen.findByTestId('occupancy-lot');

    expect(within(queue('KYB applications to review')).getByRole('link').getAttribute('href')).toBe('/admin/operations/kyb-review?status=Submitted');
    expect(within(queue('Partner applications')).getByRole('link').getAttribute('href')).toBe('/admin/operations/partner-onboarding?status=Submitted');
    expect(within(queue('Lot contracts to sign')).getByRole('link').getAttribute('href')).toBe('/admin/operations/lot-contracts?status=Pending');
    expect(screen.getByRole('link', { name: 'Vincom Landmark 81' }).getAttribute('href')).toBe('/admin/operations/parking-lots/1');
    expect(within(queue('Audit log today')).getByRole('link', { name: 'Audit log today' }).getAttribute('href')).toBe(
      '/admin/access/audit-log?from=2026-11-03&to=2026-11-03'
    );
    expect(screen.getByRole('link', { name: /Today's audit log from GateService/ }).getAttribute('href')).toBe(
      '/admin/access/audit-log?from=2026-11-03&to=2026-11-03&service=GateService'
    );
    expect(within(queue('Scheduled setting changes')).getByRole('link').getAttribute('href')).toBe(
      '/admin/access/system-settings/HOLD_TTL_MINUTES'
    );
    // US-097: màn Sanctions đã có → lọc sẵn chế tài đang hiệu lực.
    expect(within(queue('Active sanctions')).getByRole('link').getAttribute('href')).toBe('/admin/support/sanctions?status=Active');
    // Tranh chấp, risk flag: màn admin chưa có dữ liệu → không link.
    for (const label of ['Disputes with owner reply over 48 h', 'Unresolved risk flags']) {
      expect(within(queue(label)).queryByRole('link')).toBeNull();
    }
    // Không còn link nào trỏ tới tab đã ẩn.
    const hrefs = screen.getAllByRole('link').map((a) => a.getAttribute('href') ?? '');
    expect(hrefs.some((href) => /\/admin\/(finance\/(transactions|refunds)|support\/(disputes|risk-flags|tickets|vouchers))/.test(href))).toBe(false);
  });
});

describe('DashboardPage (US-081) – tải & làm mới', () => {
  it('lần đầu: skeleton theo section; lỗi toàn request → StateBlock + Retry', async () => {
    const first = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(first.promise);
    renderPage();

    expect(screen.getAllByLabelText('Loading').length).toBeGreaterThan(0);
    await act(async () => first.reject(new AdminApiError(500, 'HTTP 500')));

    expect(screen.getByText("Couldn't load the dashboard")).toBeTruthy();
    expect(screen.getByText('The server did not respond. Try again.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy());
  });

  it('403 → không có quyền', async () => {
    h.overview.mockRejectedValueOnce(new AdminApiError(403, 'Forbidden'));
    renderPage();

    expect(await screen.findByText('You cannot view the dashboard')).toBeTruthy();
  });

  it('làm mới: giữ số cũ (không nháy skeleton); làm mới lỗi → báo lỗi nhỏ, vẫn giữ số', async () => {
    renderPage();
    await waitFor(() => expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy());

    const second = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(second.promise);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh dashboard figures' }));

    expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy();
    expect(screen.queryAllByLabelText('Loading')).toHaveLength(0);
    expect((screen.getByRole('button', { name: 'Refresh dashboard figures' }) as HTMLButtonElement).disabled).toBe(true);

    await act(async () => second.reject(new AdminApiError(0, 'Network Error')));

    expect(screen.getByRole('alert').textContent).toContain("Couldn't refresh");
    expect(within(kpi('Active parking sessions')).getByText('1.842')).toBeTruthy();
  });

  it('báo aria-live khi làm mới xong', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByTestId('dashboard-live').textContent).toBe('Figures updated at 14:05'));
  });

  it('tự làm mới 60 giây khi tab hiển thị, không gửi request chồng nhau', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const first = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(first.promise);
    renderPage();
    expect(h.overview).toHaveBeenCalledTimes(1);

    // Request đầu chưa xong: 2 nhịp 60 giây không gửi thêm request nào.
    await act(async () => vi.advanceTimersByTime(120_000));
    expect(h.overview).toHaveBeenCalledTimes(1);

    await act(async () => first.resolve(overview()));
    await act(async () => vi.advanceTimersByTime(60_000));
    expect(h.overview).toHaveBeenCalledTimes(2);

    // Tab ẩn: không gọi API.
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
    await act(async () => vi.advanceTimersByTime(180_000));
    expect(h.overview).toHaveBeenCalledTimes(2);
  });

  it('rời trang → hủy request đang chạy (AbortController)', async () => {
    const pending = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(pending.promise);
    const view = renderPage();
    const signal = h.overview.mock.calls[0][0] as AbortSignal;

    expect(signal.aborted).toBe(false);
    view.unmount();
    expect(signal.aborted).toBe(true);
  });
});
