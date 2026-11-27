import React from 'react';
import type { LotCapacityConfigDto } from '@/api/capacityApi';
import { Panel } from '../../../components/Panel';
import { StateBlock } from '../../../components/StateBlock';
import { formatNumber, formatPercent } from '../../../utils/format';
import { allocation, type AllocationPart } from './capacityCopy';

const unit = (n: number, one: string, many: string) => `${formatNumber(n)} ${n === 1 ? one : many}`;

/** Một ô số: nhãn rõ nghĩa + giá trị + đơn vị (thay KPI ký hiệu T/V/A cũ). */
const Stat: React.FC<{ label: string; value: string; note?: string }> = ({ label, value, note }) => (
  <div className="flex min-w-[140px] flex-1 flex-col gap-1 border border-(--admin-border-default) bg-(--admin-surface-card) p-3">
    <span className="adm-type-kpi-label uppercase text-(--admin-text-secondary)">{label}</span>
    <span className="adm-type-kpi-value tabular-nums text-(--admin-text-primary)">{value}</span>
    {note && <span className="adm-type-body-small text-(--admin-text-secondary)">{note}</span>}
  </div>
);

/** Ba màu phân biệt rõ (status-info trùng navy-800 nên không dùng). */
const SEGMENT: Record<AllocationPart['key'], string> = {
  walkIn: 'bg-(--admin-brand-navy-800)',
  monthlyPass: 'bg-(--admin-status-success)',
  shared: 'bg-(--admin-chart-neutral)',
};

/** Thanh phân bổ sức chứa (walk-in / monthly pass / shared) + chú thích có số chỗ và %, kèm bảng sr-only. */
export const AllocationBar: React.FC<{ capacity: number; walkIn: number; monthlyPass: number }> = ({ capacity, walkIn, monthlyPass }) => {
  const parts = allocation(capacity, walkIn, monthlyPass);
  if (parts.length === 0) return null;
  return (
    <figure className="m-0 flex flex-col gap-2">
      <figcaption className="adm-type-body-strong text-(--admin-text-primary)">{`How the ${formatNumber(capacity)} spots are split`}</figcaption>
      <div aria-hidden="true" className="flex h-3 w-full overflow-hidden rounded-[2px] bg-(--admin-surface-page)" data-testid="allocation-bar">
        {parts.map((p) => (
          <span key={p.key} className={SEGMENT[p.key]} style={{ width: `${p.percent}%` }} />
        ))}
      </div>
      <ul aria-hidden="true" className="m-0 flex list-none flex-wrap gap-x-4 gap-y-1 p-0">
        {parts.map((p) => (
          <li key={p.key} className="adm-type-body-regular-12 flex items-center gap-1.5 text-(--admin-text-secondary)">
            <span className={`inline-block size-2.5 rounded-[1px] ${SEGMENT[p.key]}`} />
            <span className="text-(--admin-text-primary)">{p.label}</span>
            <span>{`${unit(p.spots, 'spot', 'spots')} · ${formatPercent(p.percent)}`}</span>
          </li>
        ))}
      </ul>
      <table className="sr-only">
        <caption>Capacity split</caption>
        <thead>
          <tr>
            <th scope="col">Use</th>
            <th scope="col">Spots</th>
            <th scope="col">Share of total</th>
          </tr>
        </thead>
        <tbody>
          {parts.map((p) => (
            <tr key={p.key}>
              <td>{p.label}</td>
              <td>{formatNumber(p.spots)}</td>
              <td>{formatPercent(p.percent)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
};

/** Cột "Live usage": xe đang đỗ và chỗ còn trống theo cấu hình hiện tại (GateService, cập nhật realtime). */
export const CapacityUsagePanel: React.FC<{ live: LotCapacityConfigDto }> = ({ live }) => (
  <Panel title="Live usage">
    {!live.configured ? (
      <StateBlock
        type="empty"
        bare
        title="Capacity not configured yet"
        message="Set total spots and limits under Configuration to start counting vehicles at the gate."
      />
    ) : (
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap gap-2">
          <Stat
            label="Occupied now"
            value={unit(live.totalParked, 'vehicle', 'vehicles')}
            note={`of ${unit(live.capacity, 'spot', 'spots')}`}
          />
          <Stat label="Walk-in vehicles" value={unit(live.transientParked, 'vehicle', 'vehicles')} />
          <Stat label="Active monthly passes" value={formatNumber(live.activeSubscribers)} note={unit(live.subscriberParked, 'vehicle', 'vehicles') + ' parked'} />
          <Stat label="Walk-in spots left" value={unit(live.availableForTransient, 'spot', 'spots')} />
          {/* Server: chỗ trống cho thuê bao = C − T (thuê bao đỗ được mọi chỗ trống; Q chỉ giới hạn số vé tháng bán ra). */}
          <Stat
            label="Free spots for monthly passes"
            value={unit(live.availableForSubscriber, 'spot', 'spots')}
            note="Pass holders can use any free spot"
          />
        </div>
        <AllocationBar capacity={live.capacity} walkIn={live.transientBuffer} monthlyPass={live.subscriberQuota} />
      </div>
    )}
  </Panel>
);
