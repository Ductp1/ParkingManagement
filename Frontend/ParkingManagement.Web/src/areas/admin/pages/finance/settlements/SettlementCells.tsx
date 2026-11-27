import React from 'react';
import { CellAmount } from '../../../components/DataTable';
import { formatSignedVnd } from './settlementFormat';

/** Số tiền VND trong bảng: âm hiện màu đỏ (chủ bãi nợ / bị trừ / bị giữ). */
export const Money: React.FC<{ value: number }> = ({ value }) => (
  <CellAmount>
    <span className={value < 0 ? 'text-(--admin-status-danger)' : undefined}>{formatSignedVnd(value)}</span>
  </CellAmount>
);
