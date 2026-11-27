import React, { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { AdminButton } from '../../../components/AdminButton';

interface ContractViewerProps {
  title: string;
  /** HTML hợp đồng do AdminService sinh (đã escape giá trị, CSS khổ A4). */
  html: string;
  onClose: () => void;
}

/**
 * Xem hợp đồng toàn màn hình + lưu PDF bằng hộp thoại in của trình duyệt (chọn "Save as PDF").
 * iframe không có allow-scripts nên HTML hợp đồng không chạy được script; allow-same-origin để gọi print() từ trang cha.
 */
export const ContractViewer: React.FC<ContractViewerProps> = ({ title, html, onClose }) => {
  const rootRef = useRef<HTMLDivElement>(null);
  const frameRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    rootRef.current?.focus();
    return () => {
      if (previous?.isConnected) previous.focus();
    };
  }, []);

  // Chặn Esc / Tab tại đây để dialog nằm dưới không đóng theo và không giành lại focus.
  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key !== 'Escape' && e.key !== 'Tab') return;
    e.stopPropagation();
    if (e.key === 'Escape') onClose();
  };

  // Phím bấm trong iframe không nổi bọt ra trang cha nên phải nghe riêng Esc.
  const listenInsideFrame = () => {
    frameRef.current?.contentDocument?.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') onClose();
    });
  };

  const print = () => {
    frameRef.current?.contentWindow?.focus();
    frameRef.current?.contentWindow?.print();
  };

  return createPortal(
    <div
      ref={rootRef}
      role="dialog"
      aria-modal="true"
      aria-label={title}
      tabIndex={-1}
      onKeyDown={onKeyDown}
      className="admin-scope fixed inset-0 z-[60] flex flex-col bg-(--admin-surface-page) outline-none"
    >
      <header className="flex items-center gap-3 border-b border-(--admin-border-default) bg-(--admin-surface-card) px-4 py-2">
        <h2 className="adm-type-heading-dialog m-0 min-w-0 flex-1 truncate text-(--admin-text-primary)">{title}</h2>
        <span className="adm-type-body-small text-(--admin-text-secondary)">In the print dialog choose “Save as PDF”.</span>
        <AdminButton icon="download" onClick={print}>
          Save as PDF
        </AdminButton>
        <AdminButton variant="secondary" onClick={onClose}>
          Close
        </AdminButton>
      </header>
      <iframe
        ref={frameRef}
        title={title}
        sandbox="allow-same-origin allow-modals"
        srcDoc={html}
        onLoad={listenInsideFrame}
        className="min-h-0 w-full flex-1 bg-white"
      />
    </div>,
    document.body
  );
};
