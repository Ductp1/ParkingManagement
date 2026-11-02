import React from 'react';
import { useCountUp } from '../hooks/useCountUp';
import { formatNumber } from '../utils/format';

interface CountUpNumberProps {
  value: number | null | undefined;
  /** Định dạng số (mặc định vi-VN); số cuối luôn hiện đúng bằng format(value). */
  format?: (n: number) => string;
  /** Hiện khi chưa có số. */
  fallback?: string;
}

/** Số liệu đếm từ 0 lên ở lần đầu có dữ liệu (--dur-count); giảm chuyển động hoặc refetch thì hiện số ngay. */
export const CountUpNumber: React.FC<CountUpNumberProps> = ({ value, format = formatNumber, fallback = '—' }) => {
  const current = useCountUp(value);
  return <>{current === null ? fallback : format(current)}</>;
};
