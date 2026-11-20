import React from 'react';
import type { AdminLotSummaryRow } from '@/types/adminLots';
import { ToolbarSelect } from './Toolbar';

interface LotPickerProps {
  /** Id bãi đang chọn dạng chuỗi ('' = chưa chọn). */
  value: string;
  lots: AdminLotSummaryRow[];
  loading?: boolean;
  onChange: (value: string) => void;
  width?: number;
}

/**
 * Bộ chọn bãi (thay ô gõ Lot ID): dropdown tên bãi · thành phố, nguồn lots-summary qua useLotOptions.
 * Đang tải → khóa với nhãn "Loading…". Id trên URL không có trong danh sách vẫn được giữ để không mất bộ lọc.
 */
export const LotPicker: React.FC<LotPickerProps> = ({ value, lots, loading, onChange, width = 260 }) => {
  const known = value === '' || lots.some((l) => String(l.parkingLotId) === value);
  const options = [
    { value: '', label: loading ? 'Loading…' : 'Choose a lot' },
    ...lots.map((l) => ({ value: String(l.parkingLotId), label: `${l.name} · ${l.city}` })),
    ...(known || loading ? [] : [{ value, label: 'Unknown lot' }]),
  ];
  return <ToolbarSelect label="Lot" value={value} options={options} onChange={onChange} width={width} disabled={loading} />;
};
