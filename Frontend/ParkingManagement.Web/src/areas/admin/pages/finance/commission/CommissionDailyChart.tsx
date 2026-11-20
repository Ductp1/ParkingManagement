import React from 'react';
import type { AdminCommissionDayDto } from '@/types/adminCommission';
import { formatNumber, formatVnd } from '../../../utils/format';
import { displayDate, displayDayMonth } from './commissionPeriod';

/**
 * US-079: hoa hồng theo ngày (ICT) – cột CSS thuần, không thêm thư viện biểu đồ. Ngày còn giao dịch chờ tính có vạch cảnh báo
 * phía trên cột (số chưa đủ, không phải 0). Bảng ẩn (sr-only) cùng số liệu cho trình đọc màn hình.
 * Chiều cao cột đổi có transition chỉ khi người dùng không bật giảm chuyển động (motion-safe).
 */
export const CommissionDailyChart: React.FC<{ days: AdminCommissionDayDto[] }> = ({ days }) => {
  const max = Math.max(0, ...days.map((d) => d.commissionAmount));
  const total = days.reduce((s, d) => s + d.commissionAmount, 0);
  const pendingDays = days.filter((d) => d.pendingCount > 0).length;
  const ticks = days.length <= 1 ? days : [days[0], days[Math.floor((days.length - 1) / 2)], days[days.length - 1]];

  return (
    <figure className="m-0 flex flex-col gap-2">
      <div
        role="img"
        aria-label={`Commission by day, ${days.length} days, total ${formatVnd(total)}${pendingDays > 0 ? `, ${pendingDays} days with pending payments` : ''}`}
        className="flex h-40 items-end gap-px border-b border-(--admin-border-default)"
      >
        {days.map((d) => {
          const pct = max > 0 ? (d.commissionAmount / max) * 100 : 0;
          const title = `${displayDate(d.date)}: ${formatVnd(d.commissionAmount)} · ${formatNumber(d.transactionCount)} payments${
            d.pendingCount > 0 ? ` · ${formatNumber(d.pendingCount)} pending` : ''
          }`;
          return (
            <div key={d.date} title={title} data-testid="commission-bar" className="relative flex h-full min-w-0 flex-1 flex-col justify-end">
              {d.pendingCount > 0 && (
                <span aria-hidden="true" className="absolute inset-x-0 top-0 h-0.5 bg-(--admin-status-warning)" />
              )}
              <span
                aria-hidden="true"
                className="block w-full bg-(--admin-brand-navy-800) motion-safe:transition-[height] motion-safe:duration-300"
                style={{ height: `${pct}%`, minHeight: d.commissionAmount > 0 ? 2 : 0 }}
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
        <caption>Commission by day</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Commission</th>
            <th scope="col">Pending payments</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date}>
              <td>{displayDate(d.date)}</td>
              <td>{formatVnd(d.commissionAmount)}</td>
              <td>{formatNumber(d.pendingCount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
};
