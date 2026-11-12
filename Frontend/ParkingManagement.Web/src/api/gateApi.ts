import { apiClient } from './client';
import type { CheckInCommand, CheckOutCommand, ParkingSessionDto, ParkingSessionStatus } from '@/types/gate';

export const gateApi = {
  /** POST /api/v1/gate/check-in – Check-in tại cổng vào (Qr / BookingCode / Manual). */
  checkIn: async (command: CheckInCommand): Promise<ParkingSessionDto> => {
    const response = await apiClient.post<ParkingSessionDto>('/api/v1/gate/check-in', command);
    return response.data;
  },

  /** POST /api/v1/gate/check-out – Check-out tại cổng ra. */
  checkOut: async (command: CheckOutCommand): Promise<ParkingSessionDto> => {
    const response = await apiClient.post<ParkingSessionDto>('/api/v1/gate/check-out', command);
    return response.data;
  },

  /** GET /api/v1/parking-sessions?parkingLotId=&status= – Các lượt xe trong bãi (mặc định: đang đỗ). */
  listSessions: async (parkingLotId: number, status?: ParkingSessionStatus): Promise<ParkingSessionDto[]> => {
    const response = await apiClient.get<ParkingSessionDto[]>('/api/v1/parking-sessions', {
      params: { parkingLotId, status },
    });
    return response.data;
  },

  /** GET /api/v1/parking-sessions/lookup?parkingLotId=&plate= – Tra xe tại cổng ra (204 = chưa vào bãi). */
  lookup: async (parkingLotId: number, plate: string): Promise<ParkingSessionDto | null> => {
    const response = await apiClient.get<ParkingSessionDto>('/api/v1/parking-sessions/lookup', {
      params: { parkingLotId, plate },
      validateStatus: (status) => status === 200 || status === 204,
    });
    return response.status === 204 ? null : response.data;
  },
};
