import React, { useCallback, useMemo, useState } from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';
import { AdminToastContext, type AdminToastInput } from '../hooks/useAdminToast';

interface ToastItem extends AdminToastInput {
  id: number;
}

const AUTO_HIDE_MS = 6000;

/** Shell/Toast (Figma): 380 rộng, góc trên phải, viền trái 3px success/danger, đổ bóng 12. */
export const AdminToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((t) => t.id !== id)), []);

  const show = useCallback(
    (toast: AdminToastInput) => {
      const id = Date.now() + Math.random();
      setToasts((list) => [...list.slice(-3), { ...toast, id }]);
      window.setTimeout(() => dismiss(id), AUTO_HIDE_MS);
    },
    [dismiss]
  );

  const value = useMemo(() => ({ show }), [show]);

  return (
    <AdminToastContext.Provider value={value}>
      {children}
      <div className="pointer-events-none fixed top-2 right-4 z-[60] flex w-[380px] flex-col gap-2" aria-live="polite">
        {toasts.map((t) => (
          <div
            key={t.id}
            role={t.type === 'error' ? 'alert' : 'status'}
            className={`toast-slide-in pointer-events-auto flex items-start gap-2.5 rounded-[4px] border-l-[3px] bg-(--admin-surface-card) p-3 shadow-[0_4px_12px_rgb(0_0_0/0.12)] ${
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
            <button type="button" aria-label="Dismiss" onClick={() => dismiss(t.id)} className="text-(--admin-text-secondary)">
              <AdminIcon icon={ADMIN_ICONS.x} />
            </button>
          </div>
        ))}
      </div>
    </AdminToastContext.Provider>
  );
};
