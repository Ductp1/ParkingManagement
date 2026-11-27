/* Fixture + render helper dùng chung cho test màn bãi / Gate Sessions (US-081). Mọi số trong test đến từ đây – component không có số mẫu. */
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import type React from 'react';
import type { AdminGateOccupancyDto, AdminLotSummaryRow, AdminLotsSummaryDto } from '@/types/adminLots';
import { AdminToastProvider } from '../../../components/AdminToastProvider';

export const lot = (id: number, patch: Partial<AdminLotSummaryRow> = {}): AdminLotSummaryRow => ({
  parkingLotId: id,
  name: `Bãi ${id}`,
  city: 'Hồ Chí Minh',
  district: 'Quận 1',
  status: 'Active',
  integrationTier: 'Manual',
  totalSlots: 100,
  isEmergencyStopped: false,
  instantBookingPaused: false,
  ...patch,
});

/** 3 bãi: Bãi Xanh (Q1, đang mở), Bãi Đỏ (Đà Nẵng, bị tạm dừng + dừng khẩn cấp), Bãi Vàng (chờ duyệt, PMS API, tạm dừng đặt nhanh). */
export const LOTS: AdminLotSummaryRow[] = [
  lot(1, { name: 'Bãi Xanh', totalSlots: 50 }),
  lot(2, { name: 'Bãi Đỏ', city: 'Đà Nẵng', district: 'Hải Châu', status: 'Suspended', isEmergencyStopped: true, totalSlots: 80 }),
  lot(3, { name: 'Bãi Vàng', district: null, status: 'PendingApproval', integrationTier: 'PmsApi', instantBookingPaused: true, totalSlots: 40 }),
];

export const summary = (lots: AdminLotSummaryRow[] = LOTS): AdminLotsSummaryDto => ({ asOfUtc: '2026-11-12T03:42:00Z', lots });

/** Bãi 1: 3 xe, sức chứa GateService 60; bãi 2: chưa cấu hình sức chứa (dùng tổng số chỗ); bãi 3: không có trong danh sách = 0 xe. */
export const occupancy = (): AdminGateOccupancyDto => ({
  asOfUtc: '2026-11-12T03:42:00Z',
  total: 7,
  walkIn: 4,
  booked: 3,
  abnormal: 0,
  lots: [
    { parkingLotId: 1, activeSessions: 3, walkIn: 2, booked: 1, capacity: 60 },
    { parkingLotId: 2, activeSessions: 4, walkIn: 2, booked: 2, capacity: null },
  ],
});

// Component chỉ dùng trong test (không có fast refresh).
// oxlint-disable-next-line react/only-export-components
const LocationProbe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

/** Render một trang admin tại URL, có toast provider và probe để đọc URL hiện tại; route chi tiết bãi để kiểm tra điều hướng. */
export const renderAt = (path: string, url: string, element: React.ReactNode) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path={path} element={<>{element}<LocationProbe /></>} />
          <Route path="/admin/operations/parking-lots/:id" element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );
