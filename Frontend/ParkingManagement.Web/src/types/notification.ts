/**
 * Types khớp với DTO của NotificationService (TV6 – port 5106).
 * Nguồn tham chiếu: Services/NotificationService/NotificationService.Application/Features/NotificationFeatures.cs
 * và SharedKernel/Contracts/PagedResult.cs (JSON camelCase do ASP.NET Core serialize).
 */

export type NotificationChannel = 'InApp' | 'Email' | 'Sms' | 'Push';

export type NotificationStatus = 'Pending' | 'Sent' | 'Failed';

export interface NotificationDto {
  id: number;
  channel: NotificationChannel;
  templateKey: string;
  title: string;
  body: string;
  /** Dữ liệu kèm theo để điều hướng, VD {"bookingId":12}. */
  dataJson: string | null;
  status: NotificationStatus;
  isRead: boolean;
  createdAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** GET /api/v1/notifications?userId=5 → { unreadCount, items: PagedResult<NotificationDto> } */
export interface NotificationInboxDto {
  unreadCount: number;
  items: PagedResult<NotificationDto>;
}

/** Body của POST /api/v1/notifications/send (internal API – dùng cho demo đặt chỗ). */
export interface SendNotificationRequest {
  userId: number;
  channel: NotificationChannel;
  templateKey: string;
  title: string;
  body: string;
  dataJson?: string | null;
}

export interface SendNotificationResponse {
  notificationId: number;
  status: NotificationStatus;
  message: string;
}

/**
 * Payload event "ReceiveNotification" từ SignalR hub /hubs/notify
 * (NotificationService.API/Hubs/NotificationHub.cs → NotificationMessage, JSON camelCase).
 */
export interface NotificationMessageDto {
  notificationId: number;
  userId: number;
  channel: NotificationChannel;
  title: string;
  body: string;
  dataJson: string | null;
  createdAtUtc: string;
}