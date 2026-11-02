import React, { useCallback, useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';
import { motionMs, prefersReducedMotion } from '../utils/motion';

interface AdminDrawerProps {
  open: boolean;
  title: string;
  description?: string;
  /** Nút / link bên phải header (ví dụ "Open full page"). */
  actions?: React.ReactNode;
  width?: number;
  onClose: () => void;
  children: React.ReactNode;
}

/** Chờ animation đóng tối đa --dur-exit + một khoảng an toàn; quá hạn vẫn đóng (tab nền, trình duyệt bỏ animation). */
const CLOSE_GRACE_MS = 60;

/**
 * Slide-over bên phải (cùng Shell/Dialog: surface/card, viền 1px, scrim overlay). Mở: trượt vào --dur-slow; đóng: trượt ra --dur-exit
 * rồi mới gọi onClose. Esc / bấm nền để đóng; focus chuyển vào panel khi mở và trả lại phần tử trước đó khi đóng.
 */
export const AdminDrawer: React.FC<AdminDrawerProps> = ({ open, title, description, actions, width = 520, onClose, children }) => {
  const titleId = useId();
  const panelRef = useRef<HTMLDivElement>(null);
  const [closing, setClosing] = useState(false);
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  /** Kết thúc đóng: trả trạng thái về "open" để lần mở sau (nếu component vẫn mounted) bắt đầu lại từ đầu. */
  const finishClose = useCallback(() => {
    setClosing(false);
    onCloseRef.current();
  }, []);

  const requestClose = useCallback(() => {
    if (prefersReducedMotion()) finishClose();
    else setClosing(true);
  }, [finishClose]);

  useEffect(() => {
    if (!closing) return;
    const handle = window.setTimeout(finishClose, motionMs('--dur-exit', panelRef.current) + CLOSE_GRACE_MS);
    return () => window.clearTimeout(handle);
  }, [closing, finishClose]);

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    panelRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') requestClose();
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      previous?.focus?.();
    };
  }, [open, requestClose]);

  if (!open) return null;
  const state = closing ? 'closing' : 'open';

  return createPortal(
    <div className="admin-scope fixed inset-0 z-50">
      <div className="adm-drawer-scrim absolute inset-0 bg-(--admin-overlay-scrim)" data-state={state} onClick={requestClose} aria-hidden="true" />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        data-state={state}
        onAnimationEnd={(e) => {
          if (closing && e.target === e.currentTarget) finishClose();
        }}
        className="adm-drawer-panel absolute inset-y-0 right-0 flex w-full flex-col border-l border-(--admin-border-default) bg-(--admin-surface-card) outline-none"
        style={{ maxWidth: width }}
      >
        <header className="flex items-start gap-3 border-b border-(--admin-border-default) pt-4 pr-4 pb-3 pl-5">
          <div className="flex min-w-0 flex-1 flex-col gap-1">
            <h2 id={titleId} className="adm-type-heading-dialog m-0 truncate text-(--admin-text-primary)">
              {title}
            </h2>
            {description && <p className="adm-type-body-regular-12 m-0 text-(--admin-text-secondary)">{description}</p>}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
          <button
            type="button"
            aria-label="Close"
            onClick={requestClose}
            className="text-(--admin-text-secondary) adm-tr-colors hover:text-(--admin-text-primary)"
          >
            <AdminIcon icon={ADMIN_ICONS.x} />
          </button>
        </header>
        <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-5 py-4">{children}</div>
      </div>
    </div>,
    document.body
  );
};
