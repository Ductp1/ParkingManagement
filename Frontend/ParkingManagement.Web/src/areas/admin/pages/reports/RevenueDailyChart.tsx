import React from 'react';
import type { AdminCommissionDayDto } from '@/types/adminCommission';
import { formatNumber, formatVnd } from '../../utils/format';
import { displayDate, displayDayMonth } from '../finance/commission/commissionPeriod';

/**
 * P0-5: doanh thu gộp theo ngày (ICT) – cột CSS thuần, đủ mọi ngày của kỳ (API trả cả ngày bằng 0).
 * Bảng sr-only cùng số liệu cho trình đọc màn hình; chiều cao cột chỉ chuyển động khi không bật giảm chuyển động.
 */
export const RevenueDailyChart: React.FC<{ days: AdminCommissionDayDto[] }> = ({ days }) => {
  const max = Math.max(0, ...days.map((d) => d.grossAmount));
  const total = days.reduce((s, d) => s + d.grossAmount, 0);
  const ticks = days.length <= 1 ? days : [days[0], days[Math.floor((days.length - 1) / 2)], days[days.length - 1]];

  return (
    <figure className="m-0 flex flex-col gap-2">
      <div
        role="img"
        aria-label={`Gross revenue by day, ${formatNumber(days.length)} days, total ${formatVnd(total)}`}
        className="flex h-40 items-end gap-px border-b border-(--admin-border-default)"
      >
        {days.map((d) => {
          const pct = max > 0 ? (d.grossAmount / max) * 100 : 0;
          return (
            <div
              key={d.date}
              title={`${displayDate(d.date)}: ${formatVnd(d.grossAmount)} · ${formatNumber(d.transactionCount)} payments`}
              data-testid="revenue-bar"
              className="flex h-full min-w-0 flex-1 flex-col justify-end"
            >
              <span
                aria-hidden="true"
                className="block w-full bg-(--admin-brand-navy-800) motion-safe:transition-[height] motion-safe:duration-300"
                style={{ height: `${pct}%`, minHeight: d.grossAmount > 0 ? 2 : 0 }}
              />
            </div>
          );
        })}
      </div>
      <figcaption className="adm-type-body-regular-12 flex justify-between text-(--admin-text-secondary)">
        {ticks.map((d, i) => (
          <span key={`${d.date}-${i}`}>{displayDayMonth(d.date)}</span>
        ))}
      </figcaption>
      <table className="sr-only">
        <caption>Gross revenue by day</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Gross revenue</th>
            <th scope="col">Payments</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date}>
              <td>{displayDate(d.date)}</td>
              <td>{formatVnd(d.grossAmount)}</td>
              <td>{formatNumber(d.transactionCount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
};
