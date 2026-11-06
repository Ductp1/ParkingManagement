import React, { useEffect, useRef } from 'react';
import { CountUpNumber } from './CountUpNumber';
import { animate, motionMs, motionVar } from '../utils/motion';

interface KpiTileProps {
  label: string;
  value: string;
  delta?: string;
  /** Tile đang chờ dữ liệu → hiện thanh skeleton thay số. */
  loading?: boolean;
  /**
   * Số thô của value (khi value là số đã định dạng): lần đầu đếm từ 0 lên trong --dur-count, refetch chỉ crossfade.
   * format phải cho ra đúng chuỗi value khi nhận lại chính số này.
   */
  count?: { value: number | null | undefined; format: (n: number) => string };
  /** Chưa có số liệu (chưa có nguồn / nguồn lỗi): giá trị hiện màu phụ, không phải số 0. */
  muted?: boolean;
  /** Thay dòng delta (ví dụ ghi chú nguồn dữ liệu). */
  note?: React.ReactNode;
}

/** Shell/KPI tile: surface/card, viền 1px, padding 16, nhãn kpi/label, giá trị kpi/value, ghi chú kpi/delta. */
export const KpiTile: React.FC<KpiTileProps> = ({ label, value, delta, loading, count, muted, note }) => {
  const valueRef = useRef<HTMLSpanElement>(null);
  const shown = useRef<string | null>(null);

  // Refetch đổi số → crossfade ngắn (không đếm lại). Lần hiện đầu tiên không chạy.
  useEffect(() => {
    const el = valueRef.current;
    if (loading || !el) return;
    const previous = shown.current;
    shown.current = value;
    if (previous === null || previous === value) return;
    animate(el, [{ opacity: 0.25 }, { opacity: 1 }], { duration: motionMs('--dur-fast', el), easing: motionVar('--ease-out', el) });
  }, [value, loading]);

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-1 border border-(--admin-border-default) bg-(--admin-surface-card) p-4">
      <span className="adm-type-kpi-label uppercase text-(--admin-text-secondary)">{label}</span>
      {loading ? (
        <span className="adm-skeleton my-1 block h-5 w-24 rounded-[2px]" />
      ) : (
        // tabular-nums: chữ số rộng bằng nhau nên khi đếm không xô layout.
        <span
          ref={valueRef}
          className={`adm-type-kpi-value adm-motion-fade tabular-nums ${muted ? 'text-(--admin-text-secondary)' : 'text-(--admin-text-primary)'}`}
        >
          {count ? <CountUpNumber value={count.value} format={count.format} fallback={value} /> : value}
        </span>
      )}
      {note ?? (delta && <span className="adm-type-kpi-delta text-(--admin-text-secondary)">{delta}</span>)}
    </div>
  );
};

/** Dải KPI tile nằm ngang, các tile chia đều; lần đầu hiện so le. */
export const KpiStrip: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="adm-stagger-children flex flex-wrap gap-0 [&>*+*]:-ml-px">{children}</div>
);
