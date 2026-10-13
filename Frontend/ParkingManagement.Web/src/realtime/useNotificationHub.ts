import { useEffect } from 'react';
import * as SignalR from '@microsoft/signalr';
import { useAppDispatch, useAppSelector } from '@/store';
import { fetchNotificationInbox, notificationReceived } from '@/store/slices/notificationSlice';
import { pushToast } from '@/store/slices/toastSlice';
import type { NotificationMessageDto } from '@/types/notification';

/**
 * Hub SignalR của NotificationService – gọi TRỰC TIẾP :5106 vì Gateway chưa có
 * route cho /hubs/* (xem Services/NotificationService/API_DIAGRAM.md).
 * Hub tự join group "user-{userId}" từ query ?userId= (NotificationHub.OnConnectedAsync).
 */
const HUB_URL = import.meta.env.VITE_NOTIFICATION_HUB_URL || 'http://localhost:5106/hubs/notify';

/**
 * Kết nối real-time nhận thông báo không cần refresh trang:
 *  - Event "ReceiveNotification" → cập nhật chuông (Redux) + bật toast popup.
 *  - Tự reconnect (withAutomaticReconnect); sau khi reconnect sẽ refetch inbox
 *    để không sót thông báo trong thời gian offline.
 */
export const useNotificationHub = (): void => {
  const dispatch = useAppDispatch();
  const userId = useAppSelector((state) => state.auth.user?.id || 5);

  useEffect(() => {
    let stopped = false;

    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(`${HUB_URL}?userId=${userId}`)
      .withAutomaticReconnect([0, 3000, 5000, 10000, 15000])
      .configureLogging(SignalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveNotification', (msg: NotificationMessageDto) => {
      // 1. Cập nhật chuông: thêm vào đầu danh sách + tăng badge chưa đọc.
      dispatch(notificationReceived(msg));
      // 2. Bật toast popup (tự ẩn sau >= 10s, đóng được bằng tay).
      dispatch(pushToast({ id: msg.notificationId, title: msg.title, body: msg.body }));
    });

    connection.onreconnected(() => {
      dispatch(fetchNotificationInbox(userId));
    });

    const start = async (): Promise<void> => {
      try {
        await connection.start();
      } catch {
        // Service chưa chạy / mất mạng → thử lại sau 5s cho tới khi dừng (unmount).
        if (!stopped) window.setTimeout(() => { void start(); }, 5000);
      }
    };
    void start();

    return () => {
      stopped = true;
      void connection.stop();
    };
  }, [dispatch, userId]);
};