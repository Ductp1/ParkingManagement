import React from 'react';
import type { AdminIconAsset } from '../navigation';

interface AdminIconProps {
  icon: AdminIconAsset;
  className?: string;
}

/** Hiển thị icon SVG xuất từ Figma đúng kích thước gốc, màu theo currentColor của phần tử cha. */
export const AdminIcon: React.FC<AdminIconProps> = ({ icon, className }) => {
  const mask = `url("${icon.src}")`;
  return (
    <span
      aria-hidden="true"
      className={`adm-icon ${className ?? ''}`}
      style={{ width: icon.width, height: icon.height, WebkitMaskImage: mask, maskImage: mask }}
    />
  );
};
