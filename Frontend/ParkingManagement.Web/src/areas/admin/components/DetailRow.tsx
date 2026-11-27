import React from 'react';
import { FutureTag } from './FutureTag';

interface DetailRowProps {
  label: string;
  children: React.ReactNode;
  future?: boolean;
}

/** Shell/Detail row: nhãn 200px (body/regular-12, text/secondary) + giá trị body/strong; viền dưới. */
export const DetailRow: React.FC<DetailRowProps> = ({ label, children, future }) => (
  <div className="flex items-start gap-3 border-b border-(--admin-border-default) px-3 py-1.5 last:border-b-0">
    <div className="flex min-h-5 w-[200px] shrink-0 items-center gap-1">
      <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{label}</span>
      {future && <FutureTag />}
    </div>
    <div className="adm-type-body-strong flex min-h-5 min-w-0 flex-1 items-center break-words text-(--admin-text-primary)">
      {children}
    </div>
  </div>
);

/** Danh sách Detail row nằm sát mép panel (body không padding). */
export const DetailList: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="-m-3 flex flex-col">{children}</div>
);
