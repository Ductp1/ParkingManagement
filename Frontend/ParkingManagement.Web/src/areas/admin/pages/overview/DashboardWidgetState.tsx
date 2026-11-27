import React, { useContext } from 'react';
import { Link } from 'react-router-dom';
import { StateBlock } from '../../components/StateBlock';
import { CountUpNumber } from '../../components/CountUpNumber';
import { KpiTile } from '../../components/KpiTile';
import { formatClockIct, formatNumber } from '../../utils/format';
import { DashboardRetryContext } from './dashboardRetry';
import type { DashboardWidget } from './dashboardTypes';
import { isHiddenWidget } from './dashboardFormat';

/** Nút "Retry" của widget lỗi nguồn – gọi lại toàn bộ overview. */
const RetryButton: React.FC<{ label: string }> = ({ label }) => {
  const retry = useContext(DashboardRetryContext);
  return (
    <button
      type="button"
      onClick={retry}
      aria-label={`Retry loading ${label}`}
      className="adm-type-link-small adm-tr-colors rounded-[2px] px-1 text-(--admin-brand-navy-900) underline-offset-2 hover:underline"
    >
      Retry
    </button>
  );
};

/** Ghi chú cho widget nguồn lỗi: "Couldn't load" (màu lỗi) + "Retry". Widget chưa có nguồn đã bị ẩn trước khi tới đây. */
export const WidgetUnavailableNote: React.FC<{ widget: DashboardWidget<unknown>; label: string }> = ({ widget, label }) => (
  <span className="adm-motion-fade flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5" data-widget-state={widget.reason ?? 'unavailable'}>
    <span className="adm-type-body-small text-(--admin-status-danger)">Couldn&apos;t load</span>
    <RetryButton label={label} />
  </span>
);

/** "Updated 14:02" theo giờ ICT của số liệu (AsOfUtc của nguồn). */
export const AsOfNote: React.FC<{ asOfUtc: string | null }> = ({ asOfUtc }) =>
  asOfUtc ? <span className="adm-type-body-small text-(--admin-text-secondary)">Updated {formatClockIct(asOfUtc)}</span> : null;

/** Thân bảng / panel khi nguồn lỗi: ErrorState + Retry (không vẽ dữ liệu mẫu, không hiện 0). */
export const WidgetTablePlaceholder: React.FC<{ widget: DashboardWidget<unknown>; label: string }> = ({ widget, label }) => {
  const retry = useContext(DashboardRetryContext);
  return (
    <div data-widget-state={widget.reason ?? 'unavailable'}>
      <StateBlock type="error" bare title={`Couldn't load ${label.toLowerCase()}`} message="The data source did not respond. Try again." onRetry={retry} />
    </div>
  );
};

interface KpiWidgetProps<T> {
  label: string;
  widget: DashboardWidget<T> | null;
  /** Số chính (count-up lần đầu). */
  count: (value: T) => number;
  format?: (n: number) => string;
  /** Dòng phụ khi có số (breakdown, badge…). */
  detail?: (value: T) => React.ReactNode;
  /** Link drill-down (chỉ trang admin đã có). */
  to?: string;
}

/** KPI tile: có số (count-up + "Updated HH:mm") / lỗi nguồn (muted "—" + Retry, không bao giờ 0) / chưa có nguồn (ẩn). */
export const KpiWidget = <T,>({ label, widget, count, format = formatNumber, detail, to }: KpiWidgetProps<T>) => {
  if (isHiddenWidget(widget)) return null;
  const value = widget?.status === 'available' ? widget.value : null;
  const number = value !== null ? count(value) : null;
  const note =
    widget === null ? undefined : value !== null ? (
      <span className="flex min-w-0 flex-col gap-0.5">
        {detail && <span className="adm-type-body-small text-(--admin-text-secondary)">{detail(value)}</span>}
        <span className="flex flex-wrap items-center gap-2">
          <AsOfNote asOfUtc={widget.asOfUtc} />
          {to && (
            <Link to={to} className="adm-type-link-small text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)">
              View details →
            </Link>
          )}
        </span>
      </span>
    ) : (
      <WidgetUnavailableNote widget={widget} label={label} />
    );
  return (
    <div className="flex min-w-[220px] flex-1" data-testid={`kpi-${label}`}>
      <KpiTile
        label={label}
        value={number === null ? '—' : format(number)}
        count={number === null ? undefined : { value: number, format }}
        loading={widget === null}
        muted={widget !== null && value === null}
        note={note}
      />
    </div>
  );
};

interface QueueRowProps<T> {
  label: string;
  /** Trang admin đã có để xử lý (kèm bộ lọc); trang chưa có thì không truyền – dòng không phải link. */
  to?: string;
  widget: DashboardWidget<T> | null;
  /** Số chính (cần Admin xử lý). */
  count?: (value: T) => number;
  /** Dòng phụ khi có số. */
  detail?: (value: T) => React.ReactNode;
  /** Số chính tô màu lỗi khi cần chú ý (quá hạn, mức Critical…). */
  urgent?: (value: T) => boolean;
}

/**
 * Một dòng hàng đợi / governance (Figma "Work queues"). Có số → count-up lần đầu + giờ cập nhật; nguồn lỗi → "—" + "Retry",
 * KHÔNG hiện 0 giả; chưa có nguồn → ẩn dòng. widget = null khi cả dashboard đang tải lần đầu.
 */
export const QueueRow = <T,>({ label, to, widget, count, detail, urgent }: QueueRowProps<T>) => {
  if (isHiddenWidget(widget)) return null;
  const value = widget?.status === 'available' ? widget.value : null;
  const sub = value !== null && detail ? detail(value) : null;
  const isUrgent = value !== null && urgent?.(value) === true;
  const title = to ? (
    <Link
      to={to}
      className="adm-type-body-default rounded-[2px] text-(--admin-text-primary) hover:underline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--admin-brand-navy-900)"
    >
      {label}
    </Link>
  ) : (
    <span className="adm-type-body-default text-(--admin-text-primary)">{label}</span>
  );

  return (
    <div
      className="adm-tr-colors flex min-h-11 items-center justify-between gap-3 border-b border-(--admin-border-default) px-3 py-1.5 last:border-b-0 hover:bg-(--admin-surface-selected)"
      data-testid={`queue-${label}`}
    >
      <span className="flex min-w-0 flex-col gap-0.5">
        {title}
        {widget === null ? null : value !== null ? (
          <span className="flex flex-wrap items-center gap-x-2">
            {sub && <span className="adm-type-body-small text-(--admin-text-secondary)">{sub}</span>}
            <AsOfNote asOfUtc={widget.asOfUtc} />
          </span>
        ) : (
          <WidgetUnavailableNote widget={widget} label={label} />
        )}
      </span>
      <span
        className={`adm-type-kpi-value-sm shrink-0 tabular-nums ${isUrgent ? 'text-(--admin-status-danger)' : 'text-(--admin-text-primary)'}`}
        data-testid={`queue-value-${label}`}
      >
        {widget === null ? (
          <span className="adm-skeleton inline-block h-4 w-8 rounded-[2px] align-middle" />
        ) : value !== null && count ? (
          <CountUpNumber value={count(value)} />
        ) : (
          <span className="text-(--admin-text-secondary)">—</span>
        )}
      </span>
    </div>
  );
};
