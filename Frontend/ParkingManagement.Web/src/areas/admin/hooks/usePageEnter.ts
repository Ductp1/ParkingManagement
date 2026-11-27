import { useLayoutEffect, useRef, type RefObject } from 'react';
import { animate, motionMs, motionPx, motionVar } from '../utils/motion';

/**
 * Chuyển trang /admin/*: khi pathname đổi, nội dung chính (title bar + màn) fade + dịch lên --motion-distance bằng
 * Web Animations API. Shell (sidebar, header) đứng yên; không remount, không gọi lại API. Đổi query string
 * (lọc, phân trang, ?log=) KHÔNG chạy. Không cần View Transitions nên chạy trên mọi trình duyệt.
 */
export const usePageEnter = (ref: RefObject<HTMLElement | null>, pathname: string): void => {
  const previous = useRef<string | null>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    const first = previous.current === null;
    const changed = previous.current !== pathname;
    previous.current = pathname;
    if (!el || first || !changed) return;
    const distance = motionPx('--motion-distance', el);
    const motion = animate(
      el,
      [
        { opacity: 0, transform: `translateY(${distance}px)` },
        { opacity: 1, transform: 'none' },
      ],
      { duration: motionMs('--dur-page', el), easing: motionVar('--ease-out', el) }
    );
    return () => motion?.cancel();
  }, [ref, pathname]);
};
