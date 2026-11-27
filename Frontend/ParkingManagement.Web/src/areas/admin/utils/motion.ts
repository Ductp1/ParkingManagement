/**
 * Motion helper cho Admin Portal (T-801): JS (Web Animations API) đọc CÙNG token với CSS (theme/motion.css),
 * nên đổi duration / easing chỉ sửa một chỗ. Token đã tự rút ngắn khi người dùng bật prefers-reduced-motion.
 */
const REDUCED_QUERY = '(prefers-reduced-motion: reduce)';

export const prefersReducedMotion = (): boolean =>
  typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia(REDUCED_QUERY).matches;

export const subscribeReducedMotion = (onChange: () => void): (() => void) => {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return () => undefined;
  const media = window.matchMedia(REDUCED_QUERY);
  media.addEventListener('change', onChange);
  return () => media.removeEventListener('change', onChange);
};

/** Phần tử mang token (.admin-portal / .admin-scope); dialog render qua portal nên tự bọc .admin-scope. */
const tokenHost = (el?: Element | null): Element =>
  el?.closest('.admin-portal, .admin-scope') ?? document.querySelector('.admin-portal, .admin-scope') ?? document.documentElement;

/** Giá trị token dạng chuỗi, ví dụ motionVar('--ease-out'). */
export const motionVar = (name: string, el?: Element | null): string => getComputedStyle(tokenHost(el)).getPropertyValue(name).trim();

/** Token thời lượng → mili giây ("200ms" / "0.2s"). */
export const motionMs = (name: string, el?: Element | null): number => {
  const raw = motionVar(name, el);
  const value = Number.parseFloat(raw);
  if (Number.isNaN(value)) return 0;
  return raw.endsWith('ms') ? value : raw.endsWith('s') ? value * 1000 : value;
};

/** Token khoảng cách → px. */
export const motionPx = (name: string, el?: Element | null): number => Number.parseFloat(motionVar(name, el)) || 0;

/** Chạy animation WAAPI nếu trình duyệt hỗ trợ và thời lượng > 0. */
export const animate = (el: Element, keyframes: Keyframe[], options: KeyframeAnimationOptions): Animation | null =>
  typeof el.animate === 'function' && Number(options.duration ?? 0) > 0 ? el.animate(keyframes, options) : null;

/**
 * Hiệu ứng đóng cho lớp phủ render qua portal (dialog): React đã gỡ node khỏi DOM, ta gắn một BẢN SAO tĩnh
 * (inert, aria-hidden, không nhận click) rồi mờ dần nó. Nội dung thật không còn chạy nên không lỗi khi dữ liệu đã null.
 */
export const playGhostExit = (node: HTMLElement, parts: { scrim?: string; panel?: string }): void => {
  if (typeof node.animate !== 'function') return;
  const ghost = node.cloneNode(true) as HTMLElement;
  ghost.setAttribute('aria-hidden', 'true');
  ghost.setAttribute('inert', '');
  ghost.style.pointerEvents = 'none';
  document.body.appendChild(ghost);
  // Node đã gỡ khỏi DOM không có computed style → đọc token từ bản sao sau khi gắn.
  const duration = motionMs('--dur-exit', ghost);
  if (duration <= 0) {
    ghost.remove();
    return;
  }
  const easing = motionVar('--ease-in', ghost);
  const scale = motionVar('--motion-scale-from', ghost) || '1';
  const scrim = parts.scrim ? ghost.querySelector(parts.scrim) ?? ghost : ghost;
  const panel = parts.panel ? ghost.querySelector(parts.panel) : null;
  // Bản sao mang class có animation vào (scale-in, rise…): hủy hết để nó đứng yên ở trạng thái cuối trước khi mờ đi.
  ghost.getAnimations?.({ subtree: true }).forEach((a) => a.cancel());
  const fade = scrim.animate([{ opacity: 1 }, { opacity: 0 }], { duration, easing, fill: 'forwards' });
  panel?.animate([{ opacity: 1, transform: 'none' }, { opacity: 0, transform: `scale(${scale})` }], { duration, easing, fill: 'forwards' });
  fade.finished.finally(() => ghost.remove());
};
