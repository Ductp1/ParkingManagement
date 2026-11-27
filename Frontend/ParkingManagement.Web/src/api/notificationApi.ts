import { apiClient } from './client';
import type {
  NotificationInboxDto,
  SendNotificationRequest,
  SendNotificationResponse,
} from '@/types/notification';

/**
 * API hộp thông báo – route qua Gateway :5000 → NotificationService :5106
 * (xem Gateway/ParkingManagement.Gateway/appsettings.json, route "notifications").
 */
export const notificationApi = {
  /**
   * Lấy hộp thông báo: số chưa đọc + danh sách phân trang (US-100, TV6).
   */
  getInbox: async (
    userId: number,
    unreadOnly = false,
    page = 1,
    pageSize = 20
  ): Promise<NotificationInboxDto> => {
    const response = await apiClient.get<NotificationInboxDto>('/api/v1/notifications', {
      params: { userId, unreadOnly, page, pageSize },
    });
    return response.data;
  },

  /**
   * Đánh dấu 1 thông báo đã đọc (404 nếu không thuộc user).
   */
  markAsRead: async (notificationId: number, userId: number): Promise<void> => {
    await apiClient.put(`/api/v1/notifications/${notificationId}/read`, null, {
      params: { userId },
    });
  },

  /**
   * Gửi thông báo qua internal API POST /api/v1/notifications/send.
   * Kênh InApp sẽ được lưu DB + đẩy real-time qua SignalR (/hubs/notify).
   * Demo dùng: khi tài xế xác nhận đặt chỗ → chuông Navbar hiện "Đặt chỗ thành công".
   */
  send: async (data: SendNotificationRequest): Promise<SendNotificationResponse> => {
    const response = await apiClient.post<SendNotificationResponse>(
      '/api/v1/notifications/send',
      data
    );
    return response.data;
  },
};