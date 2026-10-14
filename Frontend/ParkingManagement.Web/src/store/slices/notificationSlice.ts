import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit';
import { notificationApi } from '@/api/notificationApi';
import type { NotificationDto, NotificationMessageDto } from '@/types/notification';

interface NotificationState {
  items: NotificationDto[];
  unreadCount: number;
  isLoading: boolean;
  isSending: boolean;
  error: string | null;
  successMessage: string | null;
}

const initialState: NotificationState = {
  items: [],
  unreadCount: 0,
  isLoading: false,
  isSending: false,
  error: null,
  successMessage: null,
};

export const fetchNotificationInbox = createAsyncThunk(
  'notification/fetchInbox',
  async (userId: number, { rejectWithValue }) => {
    try {
      return await notificationApi.getInbox(userId);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể tải hộp thông báo.');
    }
  }
);

export const markNotificationAsRead = createAsyncThunk(
  'notification/markAsRead',
  async ({ notificationId, userId }: { notificationId: number; userId: number }, { rejectWithValue }) => {
    try {
      await notificationApi.markAsRead(notificationId, userId);
      return notificationId;
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể đánh dấu đã đọc.');
    }
  }
);

/**
 * Gửi thông báo InApp "Đặt chỗ thành công" qua NotificationService.
 * Luồng demo: tài xế bấm "Xác nhận đặt chỗ" → POST /api/v1/notifications/send
 * → thông báo lưu vào pm_notification và hiện ngay tại chuông trên Navbar.
 */
export const sendBookingConfirmedNotification = createAsyncThunk(
  'notification/sendBookingConfirmed',
  async (
    payload: { userId: number; parkingLotName: string; slotCode: string; floor: string; bookingCode: string },
    { rejectWithValue }
  ) => {
    try {
      return await notificationApi.send({
        userId: payload.userId,
        channel: 'InApp',
        templateKey: 'BOOKING_CONFIRMED',
        title: 'Đặt chỗ thành công',
        body: `Booking của bạn tại ${payload.parkingLotName} đã được xác nhận. Vị trí: Ô ${payload.slotCode} (${payload.floor}). Giữ chỗ trong 15 phút.`,
        dataJson: JSON.stringify({
          bookingCode: payload.bookingCode, // Để bấm thông báo điều hướng đúng thông tin đặt chỗ
          parkingLotName: payload.parkingLotName,
          slotCode: payload.slotCode,
          floor: payload.floor,
        }),
      });
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể gửi thông báo.');
    }
  }
);

export const notificationSlice = createSlice({
  name: 'notification',
  initialState,
  reducers: {
    clearMessages: (state) => {
      state.error = null;
      state.successMessage = null;
    },
    /**
     * Nhận thông báo mới đẩy real-time qua SignalR ("ReceiveNotification"):
     * thêm vào đầu danh sách + tăng badge chưa đọc, KHÔNG cần refresh trang.
     */
    notificationReceived: (state, action: PayloadAction<NotificationMessageDto>) => {
      const msg = action.payload;
      if (state.items.some((n) => n.id === msg.notificationId)) return;
      const item: NotificationDto = {
        id: msg.notificationId,
        channel: msg.channel,
        templateKey: '',
        title: msg.title,
        body: msg.body,
        dataJson: msg.dataJson,
        status: 'Sent',
        isRead: false,
        createdAtUtc: msg.createdAtUtc,
      };
      state.items.unshift(item);
      if (state.items.length > 20) state.items.pop();
      state.unreadCount += 1;
    },
  },
  extraReducers: (builder) => {
    // Fetch inbox
    builder
      .addCase(fetchNotificationInbox.pending, (state) => {
        state.isLoading = true;
        state.error = null;
      })
      .addCase(fetchNotificationInbox.fulfilled, (state, action) => {
        state.isLoading = false;
        state.items = action.payload.items.items;
        state.unreadCount = action.payload.unreadCount;
      })
      .addCase(fetchNotificationInbox.rejected, (state, action) => {
        state.isLoading = false;
        state.error = action.payload as string;
      });

    // Mark as read
    builder
      .addCase(markNotificationAsRead.fulfilled, (state, action) => {
        const id = action.payload;
        const target = state.items.find((n) => n.id === id);
        if (target && !target.isRead) {
          target.isRead = true;
          state.unreadCount = Math.max(0, state.unreadCount - 1);
        }
      })
      .addCase(markNotificationAsRead.rejected, (state, action) => {
        state.error = action.payload as string;
      });

    // Send booking-confirmed notification
    builder
      .addCase(sendBookingConfirmedNotification.pending, (state) => {
        state.isSending = true;
        state.error = null;
      })
      .addCase(sendBookingConfirmedNotification.fulfilled, (state) => {
        state.isSending = false;
        state.successMessage = 'Đã gửi thông báo đặt chỗ thành công!';
      })
      .addCase(sendBookingConfirmedNotification.rejected, (state, action) => {
        state.isSending = false;
        state.error = action.payload as string;
      });
  },
});

export const { clearMessages, notificationReceived } = notificationSlice.actions;
export default notificationSlice.reducer;