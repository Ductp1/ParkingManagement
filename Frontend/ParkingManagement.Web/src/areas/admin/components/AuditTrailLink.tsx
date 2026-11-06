import React from 'react';
import { Link } from 'react-router-dom';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';

/**
 * Nút "Audit log" trên title bar các màn chi tiết (US-100): mở danh sách Audit Log đã lọc sẵn theo bản ghi / người dùng.
 * Cùng kích thước + kiểu với AdminButton variant onNavy nhưng là Link (mở tab mới được, giữ lịch sử Back).
 */
export const AuditTrailLink: React.FC<{ to: string; label?: string }> = ({ to, label = 'Audit log' }) => (
  <Link
    to={to}
    className="adm-type-body-strong inline-flex h-8 shrink-0 items-center justify-center gap-1.5 rounded-[2px] border border-(--admin-text-on-navy) px-3 whitespace-nowrap text-(--admin-text-on-navy) adm-tr-colors hover:bg-(--admin-brand-navy-900)"
  >
    <AdminIcon icon={ADMIN_ICONS.clock} />
    {label}
  </Link>
);
