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

const noSource = <T,>(source: string, detail = `${source} chưa có endpoint (HTTP 404).`): DashboardWidget<T> => ({
  status: 'unavailable',
  value: null,
  asOfUtc: null,
  reason: 'no_source',
  source,
  detail,
});

const sourceError = <T,>(source: string, detail = `${source} trả HTTP 500.`): DashboardWidget<T> => ({
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
      settlementsPendingReview: noSource('PaymentService', 'PaymentService chưa có kỳ quyết toán chờ duyệt (US-080 chưa có).'),
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
  it('available: hiện số thật (vi-VN) + "Cập nhật HH:mm" ICT', async () => {
    renderPage();

    await waitFor(() => expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy());
    const sessions = kpi('Phiên gửi xe đang hoạt động');
    expect(within(sessions).getByText('Bất thường 3')).toBeTruthy();
    expect(within(sessions).getByText(/Vãng lai 342 · Đặt trước 1\.500/)).toBeTruthy();
    expect(within(sessions).getByText('Cập nhật 14:05')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('queue-value-Hồ sơ KYB cần duyệt').textContent).toBe('14'));
    expect(screen.getByText(/Dữ liệu ngày 03\/11\/2026 \(ICT\)/)).toBeTruthy();
  });

  it('"0 thật" là số 0 có nguồn, không phải "—"', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByTestId('queue-value-Hợp đồng bãi chờ ký').textContent).toBe('0'));
  });

  it('no_source: muted "—" + "Chưa có nguồn dữ liệu · {Source}", không bao giờ hiện 0', async () => {
    renderPage();
    await waitFor(() => expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy());

    const revenue = kpi('Doanh thu hôm nay (gộp)');
    expect(within(revenue).getByText('—')).toBeTruthy();
    expect(within(revenue).getByText('Chưa có nguồn dữ liệu · PaymentService')).toBeTruthy();
    expect(within(revenue).queryByText(/₫0|^0$/)).toBeNull();
    expect(within(revenue).queryByRole('button', { name: /Thử lại/ })).toBeNull();

    const settlements = queue('Kỳ quyết toán chờ duyệt');
    expect(screen.getByTestId('queue-value-Kỳ quyết toán chờ duyệt').textContent).toBe('—');
    expect(within(settlements).getByText('Chưa có nguồn dữ liệu · PaymentService')).toBeTruthy();
    expect(within(settlements).getByText(/US-080/)).toBeTruthy(); // Detail mở rộng được
    expect(within(settlements).queryByText('0')).toBeNull();
  });

  it('source_error: nhãn lỗi + "Thử lại" gọi lại toàn bộ overview', async () => {
    h.overview.mockResolvedValueOnce(overview((o) => (o.tables.gateDevices = sourceError('GateService'))));
    renderPage();

    const panel = await screen.findByText('GateService không phản hồi');
    expect(panel).toBeTruthy();
    expect(screen.getByText('GateService trả HTTP 500.')).toBeTruthy();
    expect(h.overview).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));

    await waitFor(() => expect(h.overview).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getByTestId('gate-devices')).toBeTruthy());
  });

  it('source_error ở dòng hàng đợi có nút "Thử lại" riêng, các widget khác vẫn có số', async () => {
    h.overview.mockResolvedValueOnce(overview((o) => (o.queues.kybApplications = sourceError('ParkingService'))));
    renderPage();

    const row = await screen.findByTestId('queue-Hồ sơ KYB cần duyệt');
    await waitFor(() => expect(within(row).getByText('ParkingService không phản hồi')).toBeTruthy());
    expect(within(row).getByRole('button', { name: 'Thử lại – tải lại số liệu từ ParkingService' })).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('queue-value-Hồ sơ đối tác').textContent).toBe('9')); // count-up từ 0
  });
});

describe('DashboardPage (US-081) – bảng lấp đầy', () => {
  it('toggle Theo bãi / Theo quận, ≥ 90% cảnh báo, cờ vận hành', async () => {
    renderPage();
    const byLot = await screen.findByTestId('occupancy-lot');

    expect(within(byLot).getByText('92,4%')).toBeTruthy();
    expect(within(byLot).getByText('Gần đầy')).toBeTruthy(); // chỉ bãi ≥ 90%
    expect(within(byLot).getAllByText('Gần đầy')).toHaveLength(1);
    expect(within(byLot).getByText('Tạm dừng đặt ngay')).toBeTruthy();
    expect(within(byLot).getByText('Dừng khẩn cấp')).toBeTruthy();
    expect(within(byLot).getByText('Tạm khóa')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Theo bãi' }).getAttribute('aria-pressed')).toBe('true');

    fireEvent.click(screen.getByRole('button', { name: 'Theo quận' }));

    const byDistrict = screen.getByTestId('occupancy-district');
    expect(within(byDistrict).getByText('Tân Bình, TP.HCM')).toBeTruthy();
    expect(within(byDistrict).queryByText('Vincom Landmark 81')).toBeNull();
    expect(screen.getByRole('button', { name: 'Theo quận' }).getAttribute('aria-pressed')).toBe('true');
  });

  it('sắp theo % lấp đầy: mặc định giảm dần, bấm để tăng dần', async () => {
    renderPage();
    const table = await screen.findByTestId('occupancy-lot');
    const names = () => within(table).getAllByRole('link').map((a) => a.textContent);

    expect(names()).toEqual(['Bitexco Financial Tower', 'Vincom Landmark 81', 'Tân Sơn Nhất P2']);
    fireEvent.click(screen.getByRole('button', { name: /bấm để sắp tăng dần/ }));
    expect(names()).toEqual(['Tân Sơn Nhất P2', 'Vincom Landmark 81', 'Bitexco Financial Tower']);
  });

  it('thiết bị cổng: Offline > 0 nổi bật', async () => {
    renderPage();
    const devices = await screen.findByTestId('gate-devices');

    expect(within(devices).getByText('Camera ANPR')).toBeTruthy();
    expect(screen.getByText(/1 thiết bị mất kết nối/)).toBeTruthy();
  });
});

describe('DashboardPage (US-081) – drill-down', () => {
  it('link sang trang admin đã có, đúng bộ lọc; trang chưa có thì không link', async () => {
    renderPage();
    await screen.findByTestId('occupancy-lot');

    expect(within(queue('Hồ sơ KYB cần duyệt')).getByRole('link').getAttribute('href')).toBe('/admin/operations/kyb-review?status=Submitted');
    expect(within(queue('Hồ sơ đối tác')).getByRole('link').getAttribute('href')).toBe('/admin/operations/partner-onboarding?status=Submitted');
    expect(within(queue('Hợp đồng bãi chờ ký')).getByRole('link').getAttribute('href')).toBe('/admin/operations/lot-contracts?status=Pending');
    expect(screen.getByRole('link', { name: 'Vincom Landmark 81' }).getAttribute('href')).toBe('/admin/operations/parking-lots/1');
    expect(within(queue('Audit log hôm nay')).getByRole('link', { name: 'Audit log hôm nay' }).getAttribute('href')).toBe(
      '/admin/access/audit-log?from=2026-11-03&to=2026-11-03'
    );
    expect(screen.getByRole('link', { name: /Audit log hôm nay của GateService/ }).getAttribute('href')).toBe(
      '/admin/access/audit-log?from=2026-11-03&to=2026-11-03&service=GateService'
    );
    expect(within(queue('Thay đổi cấu hình sắp hiệu lực')).getByRole('link').getAttribute('href')).toBe(
      '/admin/access/system-settings/HOLD_TTL_MINUTES'
    );
    // Hoàn tiền, quyết toán, tranh chấp, risk flag, chế tài, dữ liệu cá nhân: màn admin chưa có dữ liệu → không link.
    for (const label of ['Yêu cầu hoàn tiền chờ duyệt', 'Kỳ quyết toán chờ duyệt', 'Tranh chấp chờ chủ bãi quá hạn 48 giờ', 'Risk flag chưa xử lý',
      'Yêu cầu dữ liệu cá nhân', 'Chế tài đang hiệu lực']) {
      expect(within(queue(label)).queryByRole('link')).toBeNull();
    }
  });
});

describe('DashboardPage (US-081) – tải & làm mới', () => {
  it('lần đầu: skeleton theo section; lỗi toàn request → StateBlock + Thử lại', async () => {
    const first = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(first.promise);
    renderPage();

    expect(screen.getAllByLabelText('Loading').length).toBeGreaterThan(0);
    await act(async () => first.reject(new AdminApiError(500, 'AdminService lỗi nội bộ')));

    expect(screen.getByText('Không tải được dashboard')).toBeTruthy();
    expect(screen.getByText('AdminService lỗi nội bộ (HTTP 500).')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    await waitFor(() => expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy());
  });

  it('403 → không có quyền', async () => {
    h.overview.mockRejectedValueOnce(new AdminApiError(403, 'Tài khoản không có quyền.'));
    renderPage();

    expect(await screen.findByText('Không có quyền xem dashboard')).toBeTruthy();
  });

  it('làm mới: giữ số cũ (không nháy skeleton); làm mới lỗi → báo lỗi nhỏ, vẫn giữ số', async () => {
    renderPage();
    await waitFor(() => expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy());

    const second = deferred<DashboardOverviewDto>();
    h.overview.mockReturnValueOnce(second.promise);
    fireEvent.click(screen.getByRole('button', { name: 'Làm mới số liệu dashboard' }));

    expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy();
    expect(screen.queryAllByLabelText('Loading')).toHaveLength(0);
    expect((screen.getByRole('button', { name: 'Làm mới số liệu dashboard' }) as HTMLButtonElement).disabled).toBe(true);

    await act(async () => second.reject(new AdminApiError(0, 'Network Error')));

    expect(screen.getByRole('alert').textContent).toContain('Không làm mới được');
    expect(within(kpi('Phiên gửi xe đang hoạt động')).getByText('1.842')).toBeTruthy();
  });

  it('báo aria-live khi làm mới xong', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByTestId('dashboard-live').textContent).toBe('Đã cập nhật số liệu lúc 14:05'));
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
