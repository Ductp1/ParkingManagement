import { adminRequest, cleanParams } from './adminClientApi';
import type { FaqAudience, HelpCenterDto } from '@/types/adminSupport';
import type {
  NotificationInboxDto,
  SendNotificationRequest,
  SendNotificationResponse,
} from '@/types/notification';

export const adminCommunicationsApi = {
  /**
   * SupportService – FAQ đã công khai theo đối tượng (US-092).
   */
  getFaqs: (audience: FaqAudience, category?: string) =>
    adminRequest<HelpCenterDto>({ method: 'GET', url: '/api/v1/faqs', params: cleanParams({ audience, category }) }),

  /**
   * NotificationService – hộp thông báo InApp của admin (chuông trên header).
   */
  getInbox: (userId: number, page = 1, pageSize = 10) =>
    adminRequest<NotificationInboxDto>({
      method: 'GET',
      url: '/api/v1/notifications',
      params: { userId, page, pageSize },
    }),

  /**
   * NotificationService – đánh dấu đã đọc.
   */
  markAsRead: (notificationId: number, userId: number) =>
    adminRequest<void>({ method: 'PUT', url: `/api/v1/notifications/${notificationId}/read`, params: { userId } }),

  /**
   * NotificationService – gửi thông báo cho một người dùng.
   */
  send: (data: SendNotificationRequest) =>
    adminRequest<SendNotificationResponse>({ method: 'POST', url: '/api/v1/notifications/send', data }),
};
