import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useNotificationHub } from './useNotificationHub';

// ---- Mock dùng chung (vi.hoisted để dùng được trong vi.mock factory bị hoisting) ----
const h = vi.hoisted(() => ({
  start: vi.fn(),
  stop: vi.fn(),
  on: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
  withUrl: vi.fn(),
  buildCount: 0,
  dispatch: vi.fn(),
  fetchNotificationInbox: vi.fn(),
  notificationReceived: vi.fn(),
  pushToast: vi.fn(),
}));

vi.mock('@microsoft/signalr', () => ({
  LogLevel: { Critical: 5, Error: 4, Warning: 3, Information: 2, Debug: 1, Trace: 0 },
  HubConnectionBuilder: class {
    withUrl(...args: unknown[]) { h.withUrl(...args); return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      h.buildCount += 1;
      return { start: h.start, stop: h.stop, on: h.on, onreconnected: h.onreconnected, onclose: h.onclose };
    }
  },
}));

vi.mock('@/store', () => ({
  useAppDispatch: () => h.dispatch,
  useAppSelector: () => 5, // auth.user?.id || 5
}));

vi.mock('@/store/slices/notificationSlice', () => ({
  fetchNotificationInbox: h.fetchNotificationInbox,
  notificationReceived: h.notificationReceived,
}));

vi.mock('@/store/slices/toastSlice', () => ({
  pushToast: h.pushToast,
}));

beforeEach(() => {
  [h.start, h.stop, h.on, h.onreconnected, h.onclose, h.withUrl, h.dispatch,
    h.fetchNotificationInbox, h.notificationReceived, h.pushToast].forEach((fn) => fn.mockReset());
  h.buildCount = 0;
  h.fetchNotificationInbox.mockImplementation((userId: number) => ({ type: 'inbox', payload: userId }));
  h.notificationReceived.mockImplementation((msg: unknown) => ({ type: 'received', payload: msg }));
  h.pushToast.mockImplementation((t: unknown) => ({ type: 'toast', payload: t }));
});

describe('useNotificationHub (T-601)', () => {
  it('mount: start 1 connection, withUrl đúng hub + userId, đăng ký handler ReceiveNotification', async () => {
    renderHook(() => useNotificationHub());
    await waitFor(() => expect(h.start).toHaveBeenCalledTimes(1));

    expect(h.buildCount).toBe(1);
    expect(h.withUrl).toHaveBeenCalledTimes(1);
    expect(String(h.withUrl.mock.calls[0][0])).toBe('http://localhost:5106/hubs/notify?userId=5');
    expect(h.on).toHaveBeenCalledWith('ReceiveNotification', expect.any(Function));
  });

  it('re-render với cùng userId: KHÔNG build/start connection thứ 2', async () => {
    const { rerender } = renderHook(() => useNotificationHub());
    await waitFor(() => expect(h.start).toHaveBeenCalledTimes(1));

    rerender();
    rerender();
    await act(async () => {});

    expect(h.buildCount).toBe(1);
    expect(h.start).toHaveBeenCalledTimes(1);
  });

  it('unmount: stop connection', async () => {
    const { unmount } = renderHook(() => useNotificationHub());
    await waitFor(() => expect(h.start).toHaveBeenCalledTimes(1));

    unmount();
    await act(async () => {});

    expect(h.stop).toHaveBeenCalledTimes(1);
  });

  it('nhận ReceiveNotification: dispatch notificationReceived + pushToast đúng payload', async () => {
    renderHook(() => useNotificationHub());
    const handler = h.on.mock.calls.find(([name]) => name === 'ReceiveNotification')![1] as (m: unknown) => void;
    const msg = { notificationId: 9, channel: 'InApp', title: 'Đặt chỗ thành công', body: 'B', dataJson: '{}' };

    await act(async () => { handler(msg); });

    expect(h.notificationReceived).toHaveBeenCalledWith(msg);
    expect(h.pushToast).toHaveBeenCalledWith(expect.objectContaining({ id: 9, title: 'Đặt chỗ thành công', dataJson: '{}' }));
    expect(h.dispatch).toHaveBeenCalledWith({ type: 'received', payload: msg });
    expect(h.dispatch).toHaveBeenCalledWith({ type: 'toast', payload: expect.objectContaining({ id: 9 }) });
  });

  it('sau reconnect: refetch inbox cho đúng userId', async () => {
    renderHook(() => useNotificationHub());
    expect(h.onreconnected).toHaveBeenCalledTimes(1);
    const reconnected = h.onreconnected.mock.calls[0][0] as (id: string) => Promise<void>;

    await act(async () => { await reconnected('new-conn'); });

    expect(h.fetchNotificationInbox).toHaveBeenCalledWith(5);
    expect(h.dispatch).toHaveBeenCalledWith({ type: 'inbox', payload: 5 });
  });

  it('start lỗi: tự thử lại sau 5 giây', async () => {
    vi.useFakeTimers();
    try {
      h.start.mockRejectedValueOnce(new Error('service down')).mockResolvedValue(undefined);
      renderHook(() => useNotificationHub());
      await act(async () => {}); // flush: start lần 1 thất bại → hẹn retry 5s
      expect(h.start).toHaveBeenCalledTimes(1);

      await act(async () => { vi.advanceTimersByTime(5000); });
      expect(h.start).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('unmount sau khi start đã lỗi: KHÔNG còn retry nào được chạy (bug: timer đã hẹn trước unmount vẫn leak 1 lần)', async () => {
    vi.useFakeTimers();
    try {
      h.start.mockRejectedValue(new Error('down'));
      const { unmount } = renderHook(() => useNotificationHub());
      await act(async () => {}); // start lần 1 thất bại → đã hẹn retry lúc chưa unmount
      unmount();

      await act(async () => { vi.advanceTimersByTime(30000); });

      // Kỳ vọng lý tưởng: không còn attempt nào sau unmount.
      expect(h.start).toHaveBeenCalledTimes(1); // BUG THẬT: leak 1 retry đã hẹn (xem báo cáo)
      expect(h.stop).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });
});