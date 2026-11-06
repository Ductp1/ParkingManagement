import React, { useEffect, useId, useLayoutEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { AdminButton } from './AdminButton';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';
import { useFocusTrap } from '../hooks/useFocusTrap';
import { playGhostExit } from '../utils/motion';

interface AdminDialogProps {
  open: boolean;
  title: string;
  description?: string;
  size?: 520 | 640 | 960;
  onClose: () => void;
  onConfirm?: () => void;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  confirmDisabled?: boolean;
  submitting?: boolean;
  /** Ghi chú trái footer (body/small) – ví dụ khi endpoint chưa có. */
  footerNote?: string;
  /** Lỗi từ API (400/409…) hiển thị trên footer. */
  error?: string | null;
  children?: React.ReactNode;
}

/**
 * Shell/Dialog (Figma): surface/card, viền 1px, radius 4; header 16/48/12/20 (heading/dialog + mô tả),
 * body 16/20 gap 16, footer nền surface/page (Cancel secondary + Primary/Destructive). Nền mờ overlay/scrim.
 * Motion: nền fade, hộp scale 0.96 → 1 (--ease-spring); đóng nhanh hơn bằng bản sao tĩnh mờ dần (--dur-exit).
 * Focus bị giữ trong hộp khi mở (Tab xoay vòng) và trả về nút đã mở dialog khi đóng.
 */
export const AdminDialog: React.FC<AdminDialogProps> = ({
  open,
  title,
  description,
  size = 520,
  onClose,
  onConfirm,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive,
  confirmDisabled,
  submitting,
  footerNote,
  error,
  children,
}) => {
  const titleId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  useFocusTrap(panelRef, open);

  // Khi đóng (open → false hoặc component bị gỡ), React xóa DOM ngay; phát hiệu ứng đóng trên bản sao của node vừa gỡ.
  // StrictMode chạy lại effect nhưng không gỡ DOM → node vẫn gắn → bỏ qua.
  useLayoutEffect(() => {
    if (!open) return;
    const node = rootRef.current;
    return () => {
      if (!node) return;
      queueMicrotask(() => {
        if (!node.isConnected) playGhostExit(node, { scrim: '.adm-dialog-scrim', panel: '.adm-dialog-panel' });
      });
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !submitting) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, submitting, onClose]);

  if (!open) return null;

  return createPortal(
    <div ref={rootRef} className="admin-scope fixed inset-0 z-50">
      <div className="adm-dialog-scrim absolute inset-0 bg-(--admin-overlay-scrim)" aria-hidden="true" />
      <div className="relative flex h-full items-center justify-center p-4">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="adm-dialog-panel flex max-h-[calc(100vh-32px)] w-full flex-col rounded-[4px] border border-(--admin-border-default) bg-(--admin-surface-card) outline-none"
          style={{ maxWidth: size }}
        >
          <header className="flex items-start gap-3 border-b border-(--admin-border-default) pt-4 pr-4 pb-3 pl-5">
            <div className="flex min-w-0 flex-1 flex-col gap-1">
              <h2 id={titleId} className="adm-type-heading-dialog m-0 text-(--admin-text-primary)">
                {title}
              </h2>
              {description && <p className="adm-type-body-regular-12 m-0 text-(--admin-text-secondary)">{description}</p>}
            </div>
            <button
              type="button"
              aria-label="Close"
              onClick={onClose}
              disabled={submitting}
              className="adm-tr-colors text-(--admin-text-secondary) hover:text-(--admin-text-primary)"
            >
              <AdminIcon icon={ADMIN_ICONS.x} />
            </button>
          </header>
          <div className="flex flex-col gap-4 overflow-y-auto px-5 py-4">{children}</div>
          <footer className="flex flex-col gap-2 border-t border-(--admin-border-default) bg-(--admin-surface-page) px-5 py-3">
            {error && (
              <p key={error} role="alert" className="adm-type-body-small adm-motion-drop m-0 text-(--admin-status-danger)">
                {error}
              </p>
            )}
            <div className="flex items-center gap-2">
              <span className="adm-type-body-small min-w-0 flex-1 text-(--admin-text-secondary)">{footerNote}</span>
              <AdminButton variant="secondary" onClick={onClose} disabled={submitting}>
                {cancelLabel}
              </AdminButton>
              {onConfirm && (
                <AdminButton
                  variant={destructive ? 'destructive' : 'primary'}
                  onClick={onConfirm}
                  disabled={confirmDisabled || submitting}
                  loading={submitting}
                  loadingLabel="Saving…"
                >
                  {confirmLabel}
                </AdminButton>
              )}
            </div>
          </footer>
        </div>
      </div>
    </div>,
    document.body
  );
};

/** Khối tóm tắt đầu dialog (Figma "Summary"): nền surface/selected, viền 1px, radius 2. */
export const DialogSummary: React.FC<{ title: React.ReactNode; subtitle?: React.ReactNode }> = ({ title, subtitle }) => (
  <div className="flex flex-col gap-0.5 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-selected) px-3 py-2">
    <span className="adm-type-body-strong text-(--admin-text-primary)">{title}</span>
    {subtitle && <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{subtitle}</span>}
  </div>
);
