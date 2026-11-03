import { useSyncExternalStore } from 'react';
import { prefersReducedMotion, subscribeReducedMotion } from '../utils/motion';

/** true khi người dùng bật "giảm chuyển động" (OS / trình duyệt); tự cập nhật khi đổi cài đặt. */
export const usePrefersReducedMotion = (): boolean => useSyncExternalStore(subscribeReducedMotion, prefersReducedMotion, () => false);
