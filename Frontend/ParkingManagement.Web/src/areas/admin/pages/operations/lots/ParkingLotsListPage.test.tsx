import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { ParkingLotsListPage } from './ParkingLotsListPage';
import { countBy, filterLots, type LotListFilters } from './lotListFilters';
import { LOTS, lot, occupancy, renderAt, summary } from './lotTestFixtures';

const h = vi.hoisted(() => ({ lotsSummary: vi.fn(), occupancy: vi.fn() }));
vi.mock('@/api/adminLotsApi', () => ({ adminLotsApi: h }));

const PATH = '/admin/operations/parking-lots';
const location = () => screen.getByTestId('location').textContent ?? '';
const rowOf = (name: string) => screen.getByRole('link', { name }).closest('tr') as HTMLElement;
const filterButton = (name: RegExp) => screen.getByRole('button', { name });

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.lotsSummary.mockResolvedValue(summary());
  h.occupancy.mockResolvedValue(occupancy());
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('ParkingLotsListPage (US-081)', () => {
  it('lists every lot with real occupancy, status and flags, and a real lot count in the header', async () => {
    renderAt(PATH, PATH, <ParkingLotsListPage />);

    await screen.findByRole('link', { name: 'Bãi Xanh' });
    expect(screen.getByText('3 lots · updated 10:42 ICT')).toBeTruthy();

    const green = rowOf('Bãi Xanh');
    expect(within(green).getByText('3 / 60')).toBeTruthy(); // sức chứa GateService
    expect(within(green).getByText('5% full')).toBeTruthy();
    expect(within(green).getByText('Quận 1')).toBeTruthy();

    const red = rowOf('Bãi Đỏ');
    expect(within(red).getByText('4 / 80')).toBeTruthy(); // chưa cấu hình sức chứa → tổng số chỗ
    expect(within(red).getByText('Suspended')).toBeTruthy();
    expect(within(red).getByText('Emergency stopped')).toBeTruthy();

    const yellow = rowOf('Bãi Vàng');
    expect(within(yellow).getByText('0 / 40')).toBeTruthy(); // không có trong occupancy = 0 xe thật
    expect(within(yellow).getByText('PMS API')).toBeTruthy();
    expect(within(yellow).getByText('Instant booking paused')).toBeTruthy();
    expect(within(yellow).getByText('Pending approval')).toBeTruthy();
  });

  it('shows a count for every status and flag option', async () => {
    renderAt(PATH, PATH, <ParkingLotsListPage />);
    await screen.findByRole('link', { name: 'Bãi Xanh' });

    expect(filterButton(/^All/).textContent).toBe('All3');
    expect(filterButton(/^Pending approval/).textContent).toBe('Pending approval1');
    expect(filterButton(/^Active/).textContent).toBe('Active1');
    expect(filterButton(/^Suspended/).textContent).toBe('Suspended1');
    expect(filterButton(/^Any/).textContent).toBe('Any3');
    expect(filterButton(/^Emergency stopped/).textContent).toBe('Emergency stopped1');
    expect(filterButton(/^Instant booking paused/).textContent).toBe('Instant booking paused1');
  });

  it('filters by status on the URL and keeps the other counts in sync', async () => {
    renderAt(PATH, PATH, <ParkingLotsListPage />);
    await screen.findByRole('link', { name: 'Bãi Xanh' });

    fireEvent.click(filterButton(/^Suspended/));

    await waitFor(() => expect(location()).toBe(`${PATH}?status=Suspended`));
    expect(screen.getByRole('link', { name: 'Bãi Đỏ' })).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Bãi Xanh' })).toBeNull();
    expect(filterButton(/^Any/).textContent).toBe('Any1');
    expect(filterButton(/^Instant booking paused/).textContent).toBe('Instant booking paused0');
  });

  it('filters by city, integration and search, then clears all filters', async () => {
    renderAt(PATH, `${PATH}?city=${encodeURIComponent('Hồ Chí Minh')}&tier=PmsApi`, <ParkingLotsListPage />);

    await screen.findByRole('link', { name: 'Bãi Vàng' });
    expect(screen.queryByRole('link', { name: 'Bãi Xanh' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Bãi Đỏ' })).toBeNull();
    expect(screen.getByLabelText('City')).toHaveProperty('value', 'Hồ Chí Minh');

    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }));
    await waitFor(() => expect(location()).toBe(PATH));
    expect(screen.getByRole('link', { name: 'Bãi Xanh' })).toBeTruthy();

    fireEvent.change(screen.getByLabelText('Search lot, city or district…'), { target: { value: 'hải châu' } });
    await waitFor(() => expect(location()).toBe(`${PATH}?q=h%E1%BA%A3i+ch%C3%A2u`));
    expect(screen.getByRole('link', { name: 'Bãi Đỏ' })).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Bãi Xanh' })).toBeNull();
  });

  it('opens the lot detail from the row and from the name link', async () => {
    renderAt(PATH, PATH, <ParkingLotsListPage />);
    await screen.findByRole('link', { name: 'Bãi Xanh' });

    expect(screen.getByRole('link', { name: 'Bãi Đỏ' }).getAttribute('href')).toBe(`${PATH}/2`);
    fireEvent.click(within(rowOf('Bãi Xanh')).getByText('3 / 60'));
    await waitFor(() => expect(location()).toBe(`${PATH}/1`));
  });

  it('hides the occupancy column with a notice when live occupancy fails, never showing 0', async () => {
    h.occupancy.mockRejectedValue(new AdminApiError(500, 'Lỗi hệ thống', { title: 'Lỗi hệ thống' }));
    renderAt(PATH, PATH, <ParkingLotsListPage />);

    await screen.findByText('Live occupancy could not be loaded, so the parked / capacity column is hidden.');
    expect(screen.queryByRole('columnheader', { name: 'Parked / capacity' })).toBeNull();
    expect(screen.queryByText(/^0 \//)).toBeNull();
    expect(screen.getByRole('link', { name: 'Bãi Vàng' })).toBeTruthy();
  });

  it('shows an empty state when there are no lots and no count in the header', async () => {
    h.lotsSummary.mockResolvedValue(summary([]));
    renderAt(PATH, PATH, <ParkingLotsListPage />);

    expect(await screen.findByText('No parking lots yet')).toBeTruthy();
    expect(screen.queryByText(/lots? · updated/)).toBeNull();
  });

  it('separates "no match" from "no data"', async () => {
    renderAt(PATH, `${PATH}?q=khong-co`, <ParkingLotsListPage />);
    expect(await screen.findByText('No lots match these filters')).toBeTruthy();
  });

  it('shows the server message on error and retries', async () => {
    h.lotsSummary.mockRejectedValueOnce(new AdminApiError(503, 'Dịch vụ tạm thời không khả dụng', { title: 'x', detail: 'Dịch vụ tạm thời không khả dụng' }));
    renderAt(PATH, PATH, <ParkingLotsListPage />);

    expect(await screen.findByText('Could not load parking lots')).toBeTruthy();
    expect(screen.getByText('Dịch vụ tạm thời không khả dụng')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByRole('link', { name: 'Bãi Xanh' })).toBeTruthy();
  });

  it('shows the no-permission state on 403', async () => {
    h.lotsSummary.mockRejectedValue(new AdminApiError(403, 'Forbidden'));
    renderAt(PATH, PATH, <ParkingLotsListPage />);
    expect(await screen.findByText('Only administrators can view all parking lots.')).toBeTruthy();
  });

  it('paginates 20 lots per page on the client', async () => {
    const many = Array.from({ length: 25 }, (_, i) => lot(i + 1, { name: `Lot ${String(i + 1).padStart(2, '0')}` }));
    h.lotsSummary.mockResolvedValue(summary(many));
    renderAt(PATH, `${PATH}?page=2`, <ParkingLotsListPage />);

    expect(await screen.findByRole('link', { name: 'Lot 21' })).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Lot 20' })).toBeNull();
    expect(screen.getAllByRole('link', { name: /^Lot \d+$/ })).toHaveLength(5);
  });
});

describe('lotListFilters', () => {
  const none: LotListFilters = { status: '', flag: '', city: '', tier: '', search: '' };

  it('counts each value while other groups stay applied', () => {
    expect(countBy(LOTS, { ...none, city: 'Hồ Chí Minh' }, 'status', ['Active', 'Suspended', 'PendingApproval'])).toEqual({
      Active: 1,
      Suspended: 0,
      PendingApproval: 1,
    });
    expect(countBy(LOTS, { ...none, status: 'Suspended' }, 'flag', ['emergency', 'paused'])).toEqual({ emergency: 1, paused: 0 });
  });

  it('searches name, city and district case-insensitively', () => {
    expect(filterLots(LOTS, { ...none, search: 'ĐÀ' }).map((l) => l.parkingLotId)).toEqual([2]);
    expect(filterLots(LOTS, { ...none, search: 'quận 1' }).map((l) => l.parkingLotId)).toEqual([1]);
  });
});
