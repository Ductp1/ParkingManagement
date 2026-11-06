import { adminRequest, cleanParams } from './adminClientApi';
import type {
  AdminBookingDetailDto,
  AdminBookingSummaryDto,
  AdminGateDeviceDto,
  AdminParkingLotDto,
  AdminParkingSessionDto,
  AdminPaymentDto,
  AdminVehicleDto,
  OwnerParkingLotItemDto,
  ParkingLotHierarchyDto,
} from '@/types/adminOperations';
import type { ParkingSessionStatus } from '@/types/adminEnums';

/** API đang có của các service khác, dùng ở tab Operations của Admin Portal (đều đi qua Gateway). */
export const adminOperationsApi = {
  /** ParkingService – chi tiết bãi (US-018). */
  getParkingLot: (id: number) => adminRequest<AdminParkingLotDto>({ method: 'GET', url: `/api/v1/parking-lots/${id}` }),

  /** ParkingService – cây Zone/Floor/Slot của bãi. */
  getParkingLotHierarchy: (id: number) =>
    adminRequest<ParkingLotHierarchyDto>({ method: 'GET', url: `/api/v1/parking-lots/${id}/hierarchy` }),

  /** ParkingService – các bãi của một chủ bãi. */
  getOwnerParkingLots: (ownerProfileId: number) =>
    adminRequest<OwnerParkingLotItemDto[]>({ method: 'GET', url: `/api/v1/parking-lots/owner/${ownerProfileId}` }),

  /** BookingService – chi tiết booking theo mã. */
  getBooking: (code: string) =>
    adminRequest<AdminBookingDetailDto>({ method: 'GET', url: `/api/v1/bookings/${encodeURIComponent(code)}` }),

  /** BookingService – booking của một người dùng. */
  getUserBookings: (userId: number) =>
    adminRequest<AdminBookingSummaryDto[]>({ method: 'GET', url: '/api/v1/bookings', params: { userId } }),

  /** PaymentService – các lần thanh toán của một booking. */
  getBookingPayments: (bookingId: number) =>
    adminRequest<AdminPaymentDto[]>({ method: 'GET', url: '/api/v1/payments', params: { bookingId } }),

  /** GateService – lượt gửi xe của một bãi. */
  getLotSessions: (parkingLotId: number, status?: ParkingSessionStatus) =>
    adminRequest<AdminParkingSessionDto[]>({
      method: 'GET',
      url: '/api/v1/parking-sessions',
      params: cleanParams({ parkingLotId, status }),
    }),

  /** GateService – thiết bị cổng (Gateway hiện chưa có route /api/v1/gate-devices/**). */
  getGateDevices: () => adminRequest<AdminGateDeviceDto[]>({ method: 'GET', url: '/api/v1/gate-devices' }),

  /** VehicleService – xe của một người dùng. */
  getUserVehicles: (userId: number) =>
    adminRequest<AdminVehicleDto[]>({ method: 'GET', url: '/api/v1/vehicles', params: { userId } }),
};
