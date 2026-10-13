import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/** 1 toast popup thông báo real-time hiển thị góc phải màn hình. */
export interface NotificationToast {
  /** Trùng notificationId từ SignalR → auto-dismiss/đóng theo id. */
  id: number;
  title: string;
  body: string;
  receivedAt: number;
}

interface ToastState {
  toasts: NotificationToast[];
}

const initialState: ToastState = {
  toasts: [],
};

export const toastSlice = createSlice({
  name: 'toast',
  initialState,
  reducers: {
    /** Bật popup khi nhận thông báo mới qua SignalR (giữ tối đa 5 toast cùng lúc). */
    pushToast: (state, action: PayloadAction<{ id: number; title: string; body: string }>) => {
      // Trùng id (thông báo đã đọc/refetch) → không bật lại.
      if (state.toasts.some((t) => t.id === action.payload.id)) return;
      state.toasts.push({ ...action.payload, receivedAt: Date.now() });
      if (state.toasts.length > 5) state.toasts.shift();
    },
    dismissToast: (state, action: PayloadAction<number>) => {
      state.toasts = state.toasts.filter((t) => t.id !== action.payload);
    },
  },
});

export const { pushToast, dismissToast } = toastSlice.actions;
export default toastSlice.reducer;