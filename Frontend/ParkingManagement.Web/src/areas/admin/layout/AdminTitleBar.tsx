import React from 'react';

interface AdminTitleBarProps {
  title: string;
  subtitle?: string;
  /** Nút hành động bên phải: secondary dùng Button "On navy", primary dùng Button "Primary". */
  actions?: React.ReactNode;
}

/** Shell/TitleBar (Figma 113:681): thanh tiêu đề navy-800 full chiều rộng nội dung, padding 16, radius 0. */
export const AdminTitleBar: React.FC<AdminTitleBarProps> = ({ title, subtitle, actions }) => {
  return (
    <div className="flex items-center justify-between gap-4 bg-(--admin-brand-navy-800) p-4">
      <div className="flex min-w-0 flex-col gap-0.5">
        <h1 className="adm-type-banner-title m-0 text-(--admin-text-on-navy)">{title}</h1>
        {subtitle && <p className="adm-type-banner-subtitle m-0 text-(--admin-text-on-navy-muted)">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-start gap-2">{actions}</div>}
    </div>
  );
};
