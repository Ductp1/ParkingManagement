import { useState } from 'react';

/**
 * true chỉ cho LẦN HIỂN THỊ ĐẦU của một danh sách (chữ ký = khóa các dòng). Khi danh sách đổi (refetch có dữ liệu mới,
 * phân trang, lọc, sort) trả false vĩnh viễn → màn hình chỉ crossfade, không chạy lại stagger.
 * Dùng điều chỉnh state trong lúc render (không qua effect) nên không có frame trung gian.
 */
export const useStaggerOnce = (signature: string): boolean => {
  const [first, setFirst] = useState<string | null>(null);
  const [over, setOver] = useState(false);

  if (first === null && signature !== '') setFirst(signature);
  if (!over && first !== null && signature !== first) setOver(true);

  return !over && (first === null ? signature !== '' : signature === first);
};
