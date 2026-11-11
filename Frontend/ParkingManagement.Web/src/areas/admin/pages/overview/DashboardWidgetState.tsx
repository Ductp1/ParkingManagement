import React, { useContext } from 'react';
import { Link } from 'react-router-dom';
import { StateBlock } from '../../components/StateBlock';
import { CountUpNumber } from '../../components/CountUpNumber';
import { KpiTile } from '../../components/KpiTile';
import { formatClockIct, formatNumber } from '../../utils/format';
import { DashboardRetryContext } from './dashboardRetry';
import type { DashboardWidget } from './dashboardTypes';

/** Widget lỗi nguồn: nút "Thử lại" (dùng cho ghi chú dòng / KPI / bảng). */
const RetryButton: React.FC<{ source: string }> = ({ source }) => {
  const retry = useContext(DashboardRetryContext);
  return (
    <button
      type="button"
      onClick={retry}
      aria-label={`Thử lại – tải lại số liệu từ ${source}`}
      className="adm-type-link-small adm-tr-colors rounded-[2px] px-1 text-(--admin-brand-navy-900) underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--admin-brand-navy-900)"
    >
      Thử lại
    </button>
  );
};

/**
 * Ghi chú cho widget không có số. no_source: "Chưa có nguồn dữ liệu · {Source}" (màu phụ); source_error: "{Source} không phản hồi"
 * (màu lỗi) + "Thử lại". Lý do chi tiết (Detail) ở tooltip và mở rộng được bằng bàn phím ("Chi tiết").
 */
export const WidgetUnavailableNote: React.FC<{ widget: DashboardWidget<unknown> }> = ({ widget }) => {
  const isError = widget.reason === 'source_error';
  return (
    <span className="adm-motion-fade flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5" data-widget-state={widget.reason ?? 'unavailable'}>
      <span
        title={widget.detail ?? undefined}
        className={`adm-type-body-small ${isError ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}
      >
        {isError ? `${widget.source} không phản hồi` : `Chưa có nguồn dữ liệu · ${widget.source}`}
      </span>
      {widget.detail && (
        <details className="adm-type-body-small text-(--admin-text-secondary)">
          <summary className="cursor-pointer rounded-[2px] focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)">Chi tiết</summary>
          <span className="block max-w-[420px]">{widget.detail}</span>
        </details>
      )}
      {isError && <RetryButton source={widget.source} />}
    </span>
  );
};

/** "Cập nhật 14:02" theo giờ ICT của số liệu (AsOfUtc của nguồn). */
export const AsOfNote: React.FC<{ asOfUtc: string | null }> = ({ asOfUtc }) =>
  asOfUtc ? <span className="adm-type-body-small text-(--admin-text-secondary)">Cập nhật {formatClockIct(asOfUtc)}</span> : null;

/** Thân bảng / panel cho widget chưa có nguồn hoặc nguồn lỗi (không vẽ dữ liệu mẫu, không hiện 0). */
export const WidgetTablePlaceholder: React.FC<{ widget: DashboardWidget<unknown> }> = ({ widget }) => {
  const retry = useContext(DashboardRetryContext);
  const isError = widget.reason === 'source_error';
  return (
    <div data-widget-state={widget.reason ?? 'unavailable'}>
      <StateBlock
        type={isError ? 'error' : 'unavailable'}
        bare
        title={isError ? `${widget.source} không phản hồi` : `Chưa có nguồn dữ liệu · ${widget.source}`}
        message={widget.detail ?? undefined}
        onRetry={isError ? retry : undefined}
        retryLabel="Thử lại"
      />
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

/** KPI tile theo 3 trạng thái: có số (count-up + "Cập nhật HH:mm") / chưa có nguồn / lỗi nguồn (muted "—", không bao giờ 0). */
export const KpiWidget = <T,>({ label, widget, count, format = formatNumber, detail, to }: KpiWidgetProps<T>) => {
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
              Xem chi tiết →
            </Link>
          )}
        </span>
      </span>
    ) : (
      <WidgetUnavailableNote widget={widget} />
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
 * Một dòng hàng đợi / governance (Figma "Work queues"). Có số → count-up lần đầu + giờ cập nhật; chưa có nguồn / lỗi → "—" + ghi chú
 * nguồn (+ "Thử lại" khi lỗi), KHÔNG hiện 0 giả. widget = null khi cả dashboard đang tải lần đầu.
 */
export const QueueRow = <T,>({ label, to, widget, count, detail, urgent }: QueueRowProps<T>) => {
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
          <WidgetUnavailableNote widget={widget} />
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
