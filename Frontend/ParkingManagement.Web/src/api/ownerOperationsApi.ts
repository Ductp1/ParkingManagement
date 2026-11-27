import { apiClient } from './client';
import type {
  OperationsOverviewDto,
  HeartbeatResultDto,
  EmergencyStopResultDto,
  EmergencyResumeResultDto,
  EmergencyStopLogDto,
  DeliveryStatusDto,
} from '@/types/ownerOperations';

/**
 * API vận hành bãi (US-067/068/069) – route qua Gateway :5000 → ParkingService :5103
 * (Gateway route "parking-lots"). Backend: ParkingService/LotOperationsController.
 */
export const ownerOperationsApi = {
  /** US-067: tổng quan vận hành real-time (occupancy, booking counts, heartbeat/stale, emergency state). */
  getDashboard: async (lotId: number): Promise<OperationsOverviewDto> => {
    const response = await apiClient.get<OperationsOverviewDto>(`/api/v1/parking-lots/${lotId}/dashboard`);
    return response.data;
  },

  /** US-068: thiết bị/bãi báo còn sống (demo: nút "Giả lập heartbeat"). */
  sendHeartbeat: async (lotId: number): Promise<HeartbeatResultDto> => {
    const response = await apiClient.post<HeartbeatResultDto>(`/api/v1/parking-lots/${lotId}/heartbeat`);
    return response.data;
  },

  /** US-069: đóng bãi khẩn cấp (reason ≥ 5 ký tự; confirm=true bắt buộc). */
  emergencyStop: async (lotId: number, reason: string): Promise<EmergencyStopResultDto> => {
    const response = await apiClient.post<EmergencyStopResultDto>(`/api/v1/parking-lots/${lotId}/emergency-stop`, {
      reason,
      confirm: true,
    });
    return response.data;
  },

  /** US-069: gỡ đóng bãi khẩn cấp (bãi nhận booking/check-in trở lại). */
  emergencyResume: async (lotId: number, reason: string): Promise<EmergencyResumeResultDto> => {
    const response = await apiClient.post<EmergencyResumeResultDto>(`/api/v1/parking-lots/${lotId}/emergency-resume`, {
      reason,
    });
    return response.data;
  },

  /** US-069: lịch sử audit Emergency Stop. */
  getEmergencyLogs: async (lotId: number, take = 10): Promise<EmergencyStopLogDto[]> => {
    const response = await apiClient.get<EmergencyStopLogDto[]>(`/api/v1/parking-lots/${lotId}/emergency-logs`, {
      params: { take },
    });
    return response.data;
  },

  /** US-086: theo dõi trạng thái gửi đa kênh của 1 event khẩn cấp (Sent/Pending/Failed theo từng kênh). */
  getDeliveryStatus: async (eventId: string): Promise<DeliveryStatusDto> => {
    const response = await apiClient.get<DeliveryStatusDto>('/api/v1/notifications/delivery-status', {
      params: { eventId },
    });
    return response.data;
  },
};