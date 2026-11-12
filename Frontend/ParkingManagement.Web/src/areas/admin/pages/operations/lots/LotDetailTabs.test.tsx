import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { LotCapacityConfigDto } from '@/api/capacityApi';
import type { AdminParkingLotDto } from '@/types/adminOperations';
import type { LotAttributesDto, LotClosureDto, LotOperatingHourItemDto, LotOperatingHoursWeeklyDto, RateCardDto } from '@/types/adminLots';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { AmenitiesTab, CapacityTab, HoursTab, PricingTab } from './LotDetailTabs';
import { ParkingLotDetailPage } from './ParkingLotDetailPage';
import { band, hoursText, multiplier, sortClosures } from './lotDetailFormat';

const h = vi.hoisted(() => ({
  capacity: vi.fn(), operatingHours: vi.fn(), closures: vi.fn(), attributes: vi.fn(), rateCards: vi.fn(),
  getParkingLot: vi.fn(), getParkingLotHierarchy: vi.fn(), lotState: vi.fn(), list: vi.fn(),
}));
vi.mock('@/api/adminLotsApi', () => ({
  adminLotsApi: { capacity: h.capacity, operatingHours: h.operatingHours, closures: h.closures, attributes: h.attributes, rateCards: h.rateCards },
}));
vi.mock('@/api/adminOperationsApi', () => ({ adminOperationsApi: { getParkingLot: h.getParkingLot, getParkingLotHierarchy: h.getParkingLotHierarchy } }));
vi.mock('@/api/adminSanctionsApi', () => ({ adminSanctionsApi: { lotState: h.lotState, list: h.list } }));

const renderTab = (node: React.ReactNode) => render(<MemoryRouter>{node}</MemoryRouter>);
/** Giá trị của một Detail row theo nhãn. */
const valueOf = (label: string) => screen.getByText(label).parentElement?.nextElementSibling?.textContent;

const capacity = (patch: Partial<LotCapacityConfigDto> = {}): LotCapacityConfigDto => ({
  configured: true, capacity: 60, transientBuffer: 15, subscriberQuota: 5, totalParked: 12, transientParked: 10, subscriberParked: 2,
  activeSubscribers: 4, availableForTransient: 5, availableForSubscriber: 3, version: 3, updatedAtUtc: '2026-11-10T03:00:00Z', ...patch,
});

const day = (dayOfWeek: string, n: number, patch: Partial<LotOperatingHourItemDto> = {}): LotOperatingHourItemDto => ({
  dayOfWeek, dayOfWeekNumber: n, dayName: dayOfWeek, openTime: '07:00:00', closeTime: '22:00:00', isClosed: false, isOvernight: false,
  is24Hours: false, isDefault: true, isOverride: false, reason: null, ...patch,
});

const week = (): LotOperatingHoursWeeklyDto => ({
  parkingLotId: 1, parkingLotName: 'Bãi Xanh', defaultOpenTime: '07:00:00', defaultCloseTime: '22:00:00',
  schedule: [
    day('Sunday', 0, { isClosed: true }),
    day('Monday', 1),
    day('Tuesday', 2, { is24Hours: true }),
    day('Wednesday', 3, { closeTime: '02:00:00', isOvernight: true, isOverride: true, isDefault: false, reason: 'Sự kiện ca nhạc' }),
    day('Thursday', 4), day('Friday', 5), day('Saturday', 6),
  ],
  year: 2026, weekOfYear: 46, isCustomWeek: true, weekStartDate: '2026-11-09', weekEndDate: '2026-11-15',
});

const closure = (id: number, status: string, startsAtUtc: string, patch: Partial<LotClosureDto> = {}): LotClosureDto => ({
  id, parkingLotId: 1, targetType: 'Floor', targetId: 2, targetName: 'Tầng B1', startsAtUtc, endsAtUtc: null, reason: 'Bảo trì', isEmergency: false,
  affectedBookingsNotified: false, createdByUserId: 9, cancelledAtUtc: null, status, affectedSlotsCount: 12, createdAtUtc: startsAtUtc, ...patch,
});

const attributes = (patch: Partial<LotAttributesDto> = {}): LotAttributesDto => ({
  parkingLotId: 1, name: 'Bãi Xanh', description: null, hotlinePhone: null, maxHeightCm: 210, coverImageUrl: null,
  amenities: [
    { id: 1, parkingLotId: 1, code: 'CCTV', name: 'Camera giám sát 24/7', note: null },
    { id: 2, parkingLotId: 1, code: 'EV_CHARGER', name: 'Trụ sạc xe điện', note: '2 trụ DC' },
  ],
  photos: [
    { id: 11, parkingLotId: 1, url: 'https://cdn.example/lot1-b.jpg', caption: 'Lối vào', sortOrder: 2, isCover: false },
    { id: 10, parkingLotId: 1, url: 'https://cdn.example/lot1-a.jpg', caption: null, sortOrder: 1, isCover: true },
  ],
  ...patch,
});

const card = (patch: Partial<RateCardDto> = {}): RateCardDto => ({
  id: 1, parkingLotId: 1, ownerProfileId: 1, name: 'Bảng giá 2026', effectiveFromUtc: '2026-01-01T00:00:00Z', effectiveToUtc: null, isActive: true,
  overnightSurcharge: 20_000, weekendMultiplier: 1.2, holidayMultiplier: 1.5, oversizedMultiplier: 1.3, overstayMultiplier: 2, maxDailyAmount: null,
  rules: [
    { id: 2, vehicleType: 'Sedan', fromMinute: 60, toMinute: null, blockMinutes: 60, pricePerBlock: 20_000, sortOrder: 2 },
    { id: 1, vehicleType: 'Sedan', fromMinute: 0, toMinute: 60, blockMinutes: 60, pricePerBlock: 30_000, sortOrder: 1 },
  ],
  ...patch,
});

beforeEach(() => Object.values(h).forEach((fn) => fn.mockReset()));
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('CapacityTab', () => {
  it('shows gate capacity and what is parked right now', async () => {
    h.capacity.mockResolvedValue(capacity());
    renderTab(<CapacityTab lotId={1} />);

    expect(await screen.findByText('12 of 60')).toBeTruthy();
    expect(h.capacity).toHaveBeenCalledWith(1);
    expect(valueOf('Kept for walk-in')).toBe('15');
    expect(valueOf('Free for subscribers')).toBe('3');
  });

  it('says capacity is not set up instead of showing zeros', async () => {
    h.capacity.mockResolvedValue(capacity({ configured: false, capacity: 0, totalParked: 0 }));
    renderTab(<CapacityTab lotId={1} />);

    expect(await screen.findByText('Capacity not set up')).toBeTruthy();
    expect(screen.queryByText('0 of 0')).toBeNull();
  });

  it('shows the server message on error and retries', async () => {
    h.capacity.mockRejectedValueOnce(new AdminApiError(500, 'Lỗi hệ thống', { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' }));
    h.capacity.mockResolvedValue(capacity());
    renderTab(<CapacityTab lotId={1} />);

    expect(await screen.findByText('Could not load capacity')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('12 of 60')).toBeTruthy();
  });
});

describe('HoursTab', () => {
  it('lists the week Monday first with closed, 24h, overnight and changed days', async () => {
    h.operatingHours.mockResolvedValue(week());
    h.closures.mockResolvedValue([]);
    renderTab(<HoursTab lotId={1} />);

    expect(await screen.findByText('Week of 09/11/2026 – 15/11/2026')).toBeTruthy();
    const table = screen.getByText('Opening hours this week').closest('section') as HTMLElement;
    const days = within(table).getAllByRole('row').slice(1).map((r) => r.querySelector('td')?.textContent);
    expect(days).toEqual(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']);
    expect(within(table).getByText('Open 24 hours')).toBeTruthy();
    expect(within(table).getByText('07:00 – 02:00 (next day)')).toBeTruthy();
    expect(within(table).getByText('Changed for this week')).toBeTruthy();
    expect(within(table).getByText('Sự kiện ca nhạc')).toBeTruthy();
    expect(within(table).getByText('Closed')).toBeTruthy();
    expect(await screen.findByText('No closures')).toBeTruthy();
  });

  it('lists closures in effect first with operator labels', async () => {
    h.operatingHours.mockResolvedValue(week());
    h.closures.mockResolvedValue([
      closure(1, 'Past', '2026-10-01T01:00:00Z', { endsAtUtc: '2026-10-02T01:00:00Z' }),
      closure(2, 'Active', '2026-11-12T01:00:00Z', { isEmergency: true, targetType: 'Lot', targetName: 'Bãi Xanh' }),
      closure(3, 'Upcoming', '2026-11-20T01:00:00Z'),
    ]);
    renderTab(<HoursTab lotId={1} />);

    const panel = (await screen.findByText('In effect')).closest('section') as HTMLElement;
    const statuses = within(panel).getAllByRole('row').slice(1).map((r) => r.lastElementChild?.textContent);
    expect(statuses).toEqual(['In effect', 'Upcoming', 'Ended']);
    expect(within(panel).getByText('Emergency')).toBeTruthy();
    expect(within(panel).getAllByText('Until reopened').length).toBe(2);
  });
});

describe('AmenitiesTab', () => {
  it('lists amenities with notes and shows the cover photo first', async () => {
    h.attributes.mockResolvedValue(attributes());
    renderTab(<AmenitiesTab lotId={1} />);

    expect(await screen.findByText('Camera giám sát 24/7')).toBeTruthy();
    expect(screen.getByText('2 trụ DC')).toBeTruthy();
    const images = screen.getAllByRole('img') as HTMLImageElement[];
    expect(images.map((i) => i.getAttribute('src'))).toEqual(['https://cdn.example/lot1-a.jpg', 'https://cdn.example/lot1-b.jpg']);
    expect(images[0].alt).toBe('Bãi Xanh photo');
    expect(screen.getByText('Cover photo')).toBeTruthy();
  });

  it('replaces a photo that cannot be loaded with a labelled placeholder', async () => {
    h.attributes.mockResolvedValue(attributes());
    renderTab(<AmenitiesTab lotId={1} />);

    const cover = (await screen.findAllByRole('img'))[0];
    fireEvent.error(cover);
    expect(await screen.findByText('Photo unavailable')).toBeTruthy();
    expect(screen.getByRole('img', { name: 'Bãi Xanh photo' }).tagName).toBe('DIV');
  });

  it('shows empty states when nothing is listed', async () => {
    h.attributes.mockResolvedValue(attributes({ amenities: [], photos: [] }));
    renderTab(<AmenitiesTab lotId={1} />);

    expect(await screen.findByText('No amenities listed')).toBeTruthy();
    expect(screen.getByText('No photos')).toBeTruthy();
  });
});

describe('PricingTab', () => {
  it('shows the active price list first with multipliers and ordered rules', async () => {
    h.rateCards.mockResolvedValue([card({ id: 9, name: 'Bảng giá 2025', isActive: false, effectiveFromUtc: '2025-01-01T00:00:00Z' }), card()]);
    renderTab(<PricingTab lotId={1} />);

    const titles = (await screen.findAllByRole('heading', { level: 2 })).map((t) => t.textContent);
    expect(titles).toEqual(['Bảng giá 2026', 'Bảng giá 2025']);
    expect(screen.getByText('Active · from 01/01/2026')).toBeTruthy();
    const active = screen.getByText('Bảng giá 2026').closest('section') as HTMLElement;
    expect(within(active).getByText('×1,2')).toBeTruthy();
    expect(within(active).getByText('×1,5')).toBeTruthy();
    expect(within(active).getByText('No cap')).toBeTruthy();
    const bands = within(active).getAllByRole('row').slice(1).map((r) => r.children[1]?.textContent);
    expect(bands).toEqual(['0–60 min', 'from 60 min']);
  });

  it('shows an empty state when the owner has no prices', async () => {
    h.rateCards.mockResolvedValue([]);
    renderTab(<PricingTab lotId={1} />);
    expect(await screen.findByText('No price list yet')).toBeTruthy();
  });
});

describe('ParkingLotDetailPage – sub-tabs', () => {
  const lot: AdminParkingLotDto = {
    id: 1, name: 'Bãi Xanh', address: '1 Lê Lợi, Quận 1', latitude: 10.77, longitude: 106.7, totalSlots: 50, availableSlots: 30, occupancyRate: 0.4,
    maxHeightCm: 210, openingHours: '07:00 - 22:00', isOpenNow: true, status: 'Active', description: null, hotlinePhone: null, coverImageUrl: null,
    ratingAverage: 0, ratingCount: 0, canBook: true, amenities: null, photos: null,
  };

  const renderPage = (url: string) =>
    render(
      <AdminToastProvider>
        <MemoryRouter initialEntries={[url]}>
          <Routes>
            {/* AdminLayout cung cấp context breadcrumb cho màn chi tiết. */}
            <Route element={<Outlet context={{ setDetailLabel: () => {} }} />}>
              <Route path="/admin/operations/parking-lots/:id/:tab" element={<ParkingLotDetailPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AdminToastProvider>
    );

  beforeEach(() => {
    h.getParkingLot.mockResolvedValue(lot);
    h.lotState.mockReturnValue(new Promise(() => {}));
    h.list.mockReturnValue(new Promise(() => {}));
  });

  it.each([
    ['capacity', () => h.capacity.mockResolvedValue(capacity()), '12 of 60'],
    ['hours', () => { h.operatingHours.mockResolvedValue(week()); h.closures.mockResolvedValue([]); }, 'Opening hours this week'],
    ['amenities', () => h.attributes.mockResolvedValue(attributes()), 'Camera giám sát 24/7'],
    ['pricing', () => h.rateCards.mockResolvedValue([card()]), 'Bảng giá 2026'],
  ])('renders the %s tab from real data instead of "no endpoint"', async (tab, arrange, expected) => {
    arrange();
    renderPage(`/admin/operations/parking-lots/1/${tab}`);

    expect(await screen.findByText(expected)).toBeTruthy();
    expect(screen.queryByText(/has no endpoint/)).toBeNull();
  });
});

describe('lotDetailFormat', () => {
  it('formats hours, bands and multipliers', () => {
    expect(hoursText(day('Monday', 1))).toBe('07:00 – 22:00');
    expect(band(0, 60)).toBe('0–60 min');
    expect(band(120, null)).toBe('from 120 min');
    expect(multiplier(1.25)).toBe('×1,25');
  });

  it('orders closures: in effect, upcoming, ended, cancelled; newest start first within a group', () => {
    const ordered = sortClosures([
      closure(1, 'Cancelled', '2026-11-01T00:00:00Z'),
      closure(2, 'Past', '2026-10-01T00:00:00Z'),
      closure(3, 'Past', '2026-10-05T00:00:00Z'),
      closure(4, 'Upcoming', '2026-12-01T00:00:00Z'),
      closure(5, 'Active', '2026-11-11T00:00:00Z'),
    ]).map((c) => c.id);
    expect(ordered).toEqual([5, 4, 3, 2, 1]);
  });
});
