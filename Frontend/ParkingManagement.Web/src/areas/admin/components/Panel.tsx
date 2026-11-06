import React from 'react';
import { Link } from 'react-router-dom';
import { FutureTag } from './FutureTag';

interface PanelProps {
  title: string;
  /** Dòng mô tả nguồn dữ liệu (description strip nền surface/page). */
  description?: string;
  /** Link bên phải tiêu đề, ví dụ "Open lot →". */
  action?: { label: string; to: string };
  future?: boolean;
  /** false = body không padding (bảng nằm sát mép panel). */
  padded?: boolean;
  className?: string;
  children: React.ReactNode;
}

/** Shell/Panel: surface/card, viền 1px border/default, radius 0; title strip 33 + description strip 29. */
export const Panel: React.FC<PanelProps> = ({ title, description, action, future, padded = true, className, children }) => (
  <section className={`flex flex-col border border-(--admin-border-default) bg-(--admin-surface-card) ${className ?? ''}`}>
    <header className="flex h-[33px] shrink-0 items-center justify-between gap-2 border-b border-(--admin-border-default) px-3">
      <h2 className="adm-type-panel-title m-0 flex items-center gap-2 uppercase text-(--admin-text-secondary)">
        {title}
        {future && <FutureTag />}
      </h2>
      {action && (
        <Link to={action.to} className="adm-type-link-small adm-tr-colors text-(--admin-brand-navy-900) hover:underline">
          {action.label}
        </Link>
      )}
    </header>
    {description && (
      <p className="adm-type-panel-subtitle m-0 flex min-h-[29px] items-center border-b border-(--admin-border-default) bg-(--admin-surface-page) px-3 text-(--admin-text-secondary)">
        {description}
      </p>
    )}
    <div className={padded ? 'p-3' : ''}>{children}</div>
  </section>
);
