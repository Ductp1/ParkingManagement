import React, { useCallback, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';
import { AdminToastContext, type AdminToastInput } from '../hooks/useAdminToast';
import { animate, motionMs, motionVar } from '../utils/motion';

interface ToastItem extends AdminToastInput {
  id: number;
  leaving?: boolean;
}

const AUTO_HIDE_MS = 6000;

/**
 * Shell/Toast (Figma): 380 rộng, góc trên phải, viền trái 3px success/danger, đổ bóng 12.
 * Stack nằm ngoài .admin-portal nên tự mang .admin-scope (token màu + motion).
 * Motion: trượt vào + fade; thanh tiến trình đếm ngược tới lúc tự đóng; đóng = trượt ra (--dur-exit)
 * rồi mới gỡ; các toast còn lại dời chỗ mượt (FLIP bằng transform, không animate top/height).
 */
export const AdminToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const stackRef = useRef<HTMLDivElement>(null);
  const positions = useRef(new Map<string, number>());

  const remove = useCallback((id: number) => setToasts((list) => list.filter((t) => t.id !== id)), []);

  const dismiss = useCallback(
    (id: number) => {
      setToasts((list) => list.map((t) => (t.id === id ? { ...t, leaving: true } : t)));
      window.setTimeout(() => remove(id), motionMs('--dur-exit', stackRef.current));
    },
    [remove]
  );

  const show = useCallback(
    (toast: AdminToastInput) => {
      const id = Date.now() + Math.random();
      setToasts((list) => [...list.slice(-3), { ...toast, id }]);
      window.setTimeout(() => dismiss(id), AUTO_HIDE_MS);
    },
    [dismiss]
  );

  // FLIP: toast nào đổi vị trí (toast phía trên vừa gỡ) thì trượt từ chỗ cũ về chỗ mới.
  useLayoutEffect(() => {
    const stack = stackRef.current;
    if (!stack) return;
    const next = new Map<string, number>();
    for (const el of Array.from(stack.children) as HTMLElement[]) {
      const key = el.dataset.toastId;
      if (!key) continue;
      const top = el.offsetTop;
      next.set(key, top);
      const before = positions.current.get(key);
      if (before !== undefined && before !== top) {
        animate(el, [{ transform: `translateY(${before - top}px)` }, { transform: 'none' }], {
          duration: motionMs('--dur-base', el),
          easing: motionVar('--ease-out', el),
        });
      }
    }
    positions.current = next;
  }, [toasts]);

  const value = useMemo(() => ({ show }), [show]);

  return (
    <AdminToastContext.Provider value={value}>
      {children}
      <div ref={stackRef} className="admin-scope pointer-events-none fixed top-2 right-4 z-[60] flex w-[380px] flex-col gap-2" aria-live="polite">
        {toasts.map((t) => (
          <div
            key={t.id}
            data-toast-id={t.id}
            data-state={t.leaving ? 'closing' : 'open'}
            role={t.type === 'error' ? 'alert' : 'status'}
            style={{ '--toast-life': `${AUTO_HIDE_MS}ms` } as React.CSSProperties}
            className={`adm-toast pointer-events-auto flex items-start gap-2.5 rounded-[4px] border-l-[3px] bg-(--admin-surface-card) p-3 shadow-[0_4px_12px_rgb(0_0_0/0.12)] ${
              t.type === 'success' ? 'border-(--admin-status-success)' : 'border-(--admin-status-danger)'
            }`}
          >
            <span className={t.type === 'success' ? 'text-(--admin-status-success)' : 'text-(--admin-status-danger)'}>
              <AdminIcon icon={t.type === 'success' ? ADMIN_ICONS.check : ADMIN_ICONS.alert} />
            </span>
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="adm-type-body-strong text-(--admin-text-primary)">{t.title}</span>
              {t.message && <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{t.message}</span>}
            </div>
            <button
              type="button"
              aria-label="Dismiss"
              onClick={() => dismiss(t.id)}
              className="adm-press text-(--admin-text-secondary) hover:text-(--admin-text-primary)"
            >
              <AdminIcon icon={ADMIN_ICONS.x} />
            </button>
            <span
              aria-hidden="true"
              className={`adm-toast-progress ${t.type === 'success' ? 'bg-(--admin-status-success)' : 'bg-(--admin-status-danger)'} opacity-40`}
            />
          </div>
        ))}
      </div>
    </AdminToastContext.Provider>
  );
};
