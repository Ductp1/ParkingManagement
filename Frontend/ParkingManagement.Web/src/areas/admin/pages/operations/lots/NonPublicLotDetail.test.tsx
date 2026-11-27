import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { LotAttributesDto } from '@/types/adminLots';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { ParkingLotDetailPage } from './ParkingLotDetailPage';
import { hiddenLotNotice } from './lotDetailFormat';
import { LOTS, summary } from './lotTestFixtures';

const h = vi.hoisted(() => ({
  lotsSummary: vi.fn(), occupancy: vi.fn(), attributes: vi.fn(), capacity: vi.fn(), operatingHours: vi.fn(), closures: vi.fn(), rateCards: vi.fn(),
  getParkingLot: vi.fn(), getParkingLotHierarchy: vi.fn(), lotState: vi.fn(), list: vi.fn(),
}));
vi.mock('@/api/adminLotsApi', () => ({
  adminLotsApi: {
    lotsSummary: h.lotsSummary, occupancy: h.occupancy, attributes: h.attributes, capacity: h.capacity,
    operatingHours: h.operatingHours, closures: h.closures, rateCards: h.rateCards,
  },
}));
vi.mock('@/api/adminOperationsApi', () => ({ adminOperationsApi: { getParkingLot: h.getParkingLot, getParkingLotHierarchy: h.getParkingLotHierarchy } }));
vi.mock('@/api/adminSanctionsApi', () => ({ adminSanctionsApi: { lotState: h.lotState, list: h.list } }));

const notFound = () => new AdminApiError(404, 'Không tìm thấy Bãi đỗ với id 2.', { title: 'Not found', detail: 'Không tìm thấy Bãi đỗ với id 2.' });

const attributes = (): LotAttributesDto => ({
  parkingLotId: 2, name: 'Bãi Đỏ', description: 'Bãi ngầm hai tầng', hotlinePhone: '0236 123 456', maxHeightCm: 200, coverImageUrl: null,
  amenities: [], photos: [],
});

const renderAt = (url: string) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route element={<Outlet context={{ setDetailLabel: () => {} }} />}>
            <Route path="/admin/operations/parking-lots/:id" element={<ParkingLotDetailPage />} />
            <Route path="/admin/operations/parking-lots/:id/:tab" element={<ParkingLotDetailPage />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.getParkingLot.mockRejectedValue(notFound());
  h.lotsSummary.mockResolvedValue(summary());
  h.attributes.mockResolvedValue(attributes());
  h.lotState.mockReturnValue(new Promise(() => {}));
  h.list.mockReturnValue(new Promise(() => {}));
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('ParkingLotDetailPage – lot hidden from drivers (pending / suspended)', () => {
  it('opens a suspended lot from the admin lot list instead of showing "not available"', async () => {
    renderAt('/admin/operations/parking-lots/2');

    expect(await screen.findByRole('heading', { level: 1, name: 'Bãi Đỏ' })).toBeTruthy();
    expect(screen.getAllByText('Hải Châu, Đà Nẵng')).toHaveLength(2); // subtitle + dòng City / district
    expect(screen.getByText("Drivers can't see or book this lot while it is suspended.")).toBeTruthy();
    expect(screen.getByText('Suspended')).toBeTruthy();
    expect(screen.getAllByText('Emergency stopped').length).toBeGreaterThan(0);
    expect(await screen.findByText('Bãi ngầm hai tầng')).toBeTruthy();
    expect(screen.getByText('0236 123 456')).toBeTruthy();
    expect(screen.queryByText(/has no endpoint/)).toBeNull();
    expect(screen.queryByText('Address')).toBeNull(); // không có nguồn → không hiện dòng rỗng
    expect(h.attributes).toHaveBeenCalledWith(2);
  });

  it('shows the same sub-tabs, which load from their own sources', async () => {
    h.capacity.mockResolvedValue({
      configured: true, capacity: 80, transientBuffer: 20, subscriberQuota: 0, totalParked: 4, transientParked: 4, subscriberParked: 0,
      activeSubscribers: 0, availableForTransient: 16, availableForSubscriber: 0, version: 1, updatedAtUtc: null,
    });
    renderAt('/admin/operations/parking-lots/2/capacity');

    expect(await screen.findByText('4 of 80')).toBeTruthy();
    for (const label of ['Information', 'Layout', 'Capacity', 'Hours & Closures', 'Amenities & Photos', 'Pricing']) {
      expect(screen.getByRole('link', { name: label })).toBeTruthy();
    }
    expect(h.capacity).toHaveBeenCalledWith(2);
  });

  it('explains a lot pending approval', async () => {
    renderAt('/admin/operations/parking-lots/3');
    expect(await screen.findByText("Drivers can't see this lot until its KYB review is approved.")).toBeTruthy();
  });

  it('shows "Parking lot not found" with a way back when the lot does not exist', async () => {
    renderAt('/admin/operations/parking-lots/99');

    expect(await screen.findByText('Parking lot not found')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Back to parking lots' }).getAttribute('href')).toBe('/admin/operations/parking-lots');
    expect(screen.queryByText(/has no endpoint/)).toBeNull();
  });

  it('keeps the normal error path for errors other than 404', async () => {
    h.getParkingLot.mockRejectedValue(new AdminApiError(500, 'Lỗi hệ thống', { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' }));
    renderAt('/admin/operations/parking-lots/2');

    expect(await screen.findByText('Could not load the parking lot')).toBeTruthy();
    expect(h.lotsSummary).not.toHaveBeenCalled();
  });

  it('uses the public detail when the lot is public', async () => {
    h.getParkingLot.mockResolvedValue({
      id: 1, name: 'Bãi Xanh', address: '1 Lê Lợi, Quận 1', latitude: 10.77, longitude: 106.7, totalSlots: 50, availableSlots: 30, occupancyRate: 0.4,
      maxHeightCm: 210, openingHours: '07:00 - 22:00', isOpenNow: true, status: 'Active', description: null, hotlinePhone: null, coverImageUrl: null,
      ratingAverage: 0, ratingCount: 0, canBook: true, amenities: null, photos: null,
    });
    renderAt('/admin/operations/parking-lots/1');

    expect(await screen.findByText('1 Lê Lợi, Quận 1')).toBeTruthy();
    expect(h.lotsSummary).not.toHaveBeenCalled();
    expect(LOTS[0].name).toBe('Bãi Xanh');
  });
});

describe('hiddenLotNotice', () => {
  it('words the notice by lot status', () => {
    expect(hiddenLotNotice('Suspended')).toContain('suspended');
    expect(hiddenLotNotice('PendingApproval')).toContain('KYB review');
    expect(hiddenLotNotice('Active')).toBe("Drivers can't see this lot right now.");
  });
});
