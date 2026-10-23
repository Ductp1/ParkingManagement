import React from 'react';

interface KpiTileProps {
  label: string;
  value: string;
  delta?: string;
  /** Tile đang chờ dữ liệu → hiện thanh skeleton thay số. */
  loading?: boolean;
}

/** Shell/KPI tile: surface/card, viền 1px, padding 16, nhãn kpi/label, giá trị kpi/value, ghi chú kpi/delta. */
export const KpiTile: React.FC<KpiTileProps> = ({ label, value, delta, loading }) => (
  <div className="flex min-w-0 flex-1 flex-col gap-1 border border-(--admin-border-default) bg-(--admin-surface-card) p-4">
    <span className="adm-type-kpi-label uppercase text-(--admin-text-secondary)">{label}</span>
    {loading ? (
      <span className="my-1 block h-5 w-24 animate-pulse rounded-[2px] bg-(--admin-surface-selected)" />
    ) : (
      <span className="adm-type-kpi-value text-(--admin-text-primary)">{value}</span>
    )}
    {delta && <span className="adm-type-kpi-delta text-(--admin-text-secondary)">{delta}</span>}
  </div>
);

/** Dải KPI tile nằm ngang, các tile chia đều. */
export const KpiStrip: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="flex flex-wrap gap-0 [&>*+*]:-ml-px">{children}</div>
);
