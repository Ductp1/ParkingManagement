import React from 'react';
import { Link } from 'react-router-dom';
import { StateBlock } from '../../components/StateBlock';
import { CountUpNumber } from '../../components/CountUpNumber';
import type { DashboardWidget } from './dashboardTypes';

/** Câu ngắn (UI tiếng Anh) cho widget không có số: chưa có nguồn hoặc nguồn lỗi. Lý do chi tiết (tiếng Việt) đặt ở tooltip. */
const unavailableLabel = (widget: DashboardWidget<unknown>): string =>
  widget.reason === 'source_error' ? `${widget.source} did not respond` : `No data source yet · needs ${widget.source}`;

/** Ghi chú nhỏ dưới số / dòng: trạng thái nguồn của widget unavailable. */
export const WidgetUnavailableNote: React.FC<{ widget: DashboardWidget<unknown> }> = ({ widget }) => (
  <span
    title={widget.detail ?? undefined}
    className={`adm-type-body-small adm-motion-fade ${widget.reason === 'source_error' ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}
  >
    {unavailableLabel(widget)}
  </span>
);

/** Thân bảng cho widget dạng bảng chưa có nguồn / nguồn lỗi (giữ header theo Figma, không vẽ dữ liệu mẫu). */
export const WidgetTablePlaceholder: React.FC<{ widget: DashboardWidget<unknown> }> = ({ widget }) => (
  <StateBlock
    type={widget.reason === 'source_error' ? 'error' : 'unavailable'}
    bare
    title={widget.reason === 'source_error' ? `${widget.source} did not respond` : 'No data source yet'}
    message={`Needs an admin endpoint in ${widget.source}.${widget.detail ? ` ${widget.detail}` : ''}`}
  />
);

interface QueueRowProps<T> {
  label: string;
  to: string;
  widget: DashboardWidget<T> | null;
  /** Số chính (cần Admin xử lý). */
  count?: (value: T) => number;
  /** Dòng phụ khi có số. */
  detail?: (value: T) => string | null;
}

/**
 * Một dòng hàng đợi / governance (Figma "Work queues"): bấm để mở màn xử lý. Có số → count-up lần đầu; chưa có nguồn / lỗi
 * → "—" + ghi chú nguồn, KHÔNG hiện 0 giả. widget = null khi cả dashboard đang tải.
 */
export const QueueRow = <T,>({ label, to, widget, count, detail }: QueueRowProps<T>) => {
  const value = widget?.status === 'available' ? widget.value : null;
  const sub = value !== null && detail ? detail(value) : null;
  return (
    <Link
      to={to}
      className="adm-tr-colors flex min-h-11 items-center justify-between gap-3 border-b border-(--admin-border-default) px-3 py-1.5 last:border-b-0 hover:bg-(--admin-surface-selected)"
    >
      <span className="flex min-w-0 flex-col">
        <span className="adm-type-body-default text-(--admin-text-primary)">{label}</span>
        {widget === null ? null : value !== null ? (
          sub && <span className="adm-type-body-small text-(--admin-text-secondary)">{sub}</span>
        ) : (
          <WidgetUnavailableNote widget={widget} />
        )}
      </span>
      <span className="adm-type-kpi-value-sm shrink-0 text-(--admin-text-primary) tabular-nums">
        {widget === null ? (
          <span className="adm-skeleton inline-block h-4 w-8 rounded-[2px] align-middle" />
        ) : value !== null && count ? (
          <CountUpNumber value={count(value)} />
        ) : (
          <span className="text-(--admin-text-secondary)">—</span>
        )}
      </span>
    </Link>
  );
};
