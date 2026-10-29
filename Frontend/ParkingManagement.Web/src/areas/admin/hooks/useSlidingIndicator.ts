import { useLayoutEffect, type RefObject } from 'react';

type Axis = 'x' | 'y';

/**
 * Indicator dùng chung (sidebar, tab bar, sub-tab, cột lọc): một phần tử .adm-indicator đặt tuyệt đối trong container,
 * trượt tới phần tử đang active (tìm theo selector, mặc định aria-current="page") bằng transform – không đổi layout.
 * - axis 'x': gạch chân 1px rộng → scaleX theo độ rộng tab (tab active đổi Regular → Bold nên đo lại sau mỗi render).
 * - axis 'y': khối nền cao theo mục active, dịch translateY.
 * Lần đặt đầu tiên không có transition (data-ready bật ở frame sau) để không trượt từ góc 0,0.
 * Đo lại khi container đổi kích thước và khi font web tải xong.
 */
export const useSlidingIndicator = (
  containerRef: RefObject<HTMLElement | null>,
  indicatorRef: RefObject<HTMLElement | null>,
  axis: Axis,
  activeKey: unknown,
  selector = '[aria-current="page"]'
): void => {
  useLayoutEffect(() => {
    const container = containerRef.current;
    const indicator = indicatorRef.current;
    if (!container || !indicator) return;

    const place = () => {
      const active = container.querySelector<HTMLElement>(selector);
      if (!active) {
        indicator.dataset.visible = 'false';
        return;
      }
      const box = container.getBoundingClientRect();
      const rect = active.getBoundingClientRect();
      const left = rect.left - box.left + container.scrollLeft;
      const top = rect.top - box.top + container.scrollTop;
      indicator.style.transform =
        axis === 'x'
          ? `translate(${left}px, ${top + rect.height - indicator.offsetHeight}px) scaleX(${rect.width})`
          : `translate(${left}px, ${top}px)`;
      if (axis === 'y') {
        indicator.style.width = `${rect.width}px`;
        indicator.style.height = `${rect.height}px`;
      }
      indicator.dataset.visible = 'true';
    };

    place();
    const frame = requestAnimationFrame(() => {
      indicator.dataset.ready = 'true';
    });
    // Đo lại khi container HOẶC từng mục đổi kích thước: font web (Be Vietnam Pro đậm) tải xong sau lần đo đầu làm tab
    // active rộng / hẹp đi mà container không đổi → trước đây gạch chân giữ độ rộng cũ và bị lệch.
    const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(place) : null;
    if (observer) {
      observer.observe(container);
      for (const child of Array.from(container.children)) if (child !== indicator) observer.observe(child);
    }
    let cancelled = false;
    const onFonts = () => {
      if (!cancelled) place();
    };
    document.fonts?.ready.then(onFonts);
    document.fonts?.addEventListener('loadingdone', onFonts);
    return () => {
      cancelled = true;
      cancelAnimationFrame(frame);
      observer?.disconnect();
      document.fonts?.removeEventListener('loadingdone', onFonts);
    };
  }, [containerRef, indicatorRef, axis, activeKey, selector]);
};
