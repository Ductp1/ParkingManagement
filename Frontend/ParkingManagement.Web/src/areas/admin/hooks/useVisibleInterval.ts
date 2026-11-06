import { useEffect, useRef } from 'react';

/**
 * Gọi callback mỗi intervalMs CHỈ khi tab đang hiển thị (không gọi API khi tab ẩn). Quay lại tab sau khi đã quá intervalMs
 * thì gọi ngay một lần. Dùng cho dashboard tự làm mới (US-081: 60 giây).
 */
export const useVisibleInterval = (callback: () => void, intervalMs: number): void => {
  const callbackRef = useRef(callback);
  const lastRun = useRef(0);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(() => {
    lastRun.current = Date.now();
    const run = () => {
      lastRun.current = Date.now();
      callbackRef.current();
    };
    const tick = window.setInterval(() => {
      if (document.visibilityState === 'visible') run();
    }, intervalMs);
    const onVisibility = () => {
      if (document.visibilityState === 'visible' && Date.now() - lastRun.current >= intervalMs) run();
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      window.clearInterval(tick);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [intervalMs]);
};
