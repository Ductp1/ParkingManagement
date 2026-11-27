import { useEffect, useState } from 'react';
import { motionMs } from '../utils/motion';
import { usePrefersReducedMotion } from './usePrefersReducedMotion';

const easeOutCubic = (t: number) => 1 - (1 - t) ** 3;

/**
 * Đếm 0 → target trong --dur-count, CHỈ lần đầu có số (refetch hiện số mới ngay; component tự crossfade).
 * Giảm chuyển động: trả số cuối ngay. null/undefined = chưa có dữ liệu.
 */
export const useCountUp = (target: number | null | undefined): number | null => {
  const reduced = usePrefersReducedMotion();
  const [run, setRun] = useState<number | null>(null);
  const [done, setDone] = useState(false);
  const [frame, setFrame] = useState(0);

  // Điều chỉnh state trong lúc render (không qua effect): bắt đầu đúng một lần khi số đầu tiên xuất hiện.
  if (!done && run === null && target !== null && target !== undefined) {
    if (reduced || target === 0) setDone(true);
    else setRun(target);
  }

  useEffect(() => {
    if (run === null || done) return;
    const duration = motionMs('--dur-count');
    if (duration <= 0) {
      const handle = requestAnimationFrame(() => setDone(true));
      return () => cancelAnimationFrame(handle);
    }
    let handle = 0;
    const start = performance.now();
    const tick = (now: number) => {
      const progress = Math.min(1, (now - start) / duration);
      if (progress >= 1) {
        setDone(true);
        return;
      }
      setFrame(run * easeOutCubic(progress));
      handle = requestAnimationFrame(tick);
    };
    handle = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(handle);
  }, [run, done]);

  if (target === null || target === undefined) return null;
  return done ? target : Math.round(frame);
};
