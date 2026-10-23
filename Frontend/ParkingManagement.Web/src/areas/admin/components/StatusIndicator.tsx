import React from 'react';
import { humanizeEnum, statusTone, type StatusTone } from '../utils/enumLabels';

const TONE_COLOR: Record<StatusTone, { dot: string; text: string }> = {
  success: { dot: 'bg-(--admin-status-success)', text: 'text-(--admin-status-success)' },
  warning: { dot: 'bg-(--admin-status-warning)', text: 'text-(--admin-status-warning)' },
  danger: { dot: 'bg-(--admin-status-danger)', text: 'text-(--admin-status-danger)' },
  info: { dot: 'bg-(--admin-status-info)', text: 'text-(--admin-status-info)' },
  neutral: { dot: 'bg-(--admin-text-secondary)', text: 'text-(--admin-text-secondary)' },
};

interface StatusIndicatorProps {
  /** Giá trị enum từ API (UnderReview…) – nhãn và màu tự suy ra. */
  value?: string | null;
  /** Ghi đè nhãn/màu khi trạng thái không phải enum. */
  label?: string;
  tone?: StatusTone;
}

/** Shell/Status (Figma): chấm 8px + nhãn Medium 12 cùng màu trạng thái, không nền pill. */
export const StatusIndicator: React.FC<StatusIndicatorProps> = ({ value, label, tone }) => {
  const color = TONE_COLOR[tone ?? statusTone(value)];
  return (
    <span className={`adm-type-status-label inline-flex items-center gap-1.5 whitespace-nowrap ${color.text}`}>
      <span aria-hidden="true" className={`size-2 shrink-0 rounded-full ${color.dot}`} />
      {label ?? humanizeEnum(value)}
    </span>
  );
};
