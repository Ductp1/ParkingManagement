import type { ModeratedReviewDto } from '@/types/adminReviews';

/** US-095: lọc phía client hàng chờ đánh giá bị khiếu nại (API không có tham số lọc, trả tối đa 100 dòng). */
export interface ReviewFilters {
  /** Tìm trong người đánh giá, nội dung, lý do khiếu nại, phản hồi chủ bãi, tên bãi, mã đánh giá. */
  search: string;
  /** 1–5 hoặc null = mọi mức sao. */
  rating: number | null;
  lotId: number | null;
}

export const RATINGS = [5, 4, 3, 2, 1] as const;

export const parseRating = (value: string): number | null => {
  const n = Number(value);
  return Number.isInteger(n) && n >= 1 && n <= 5 ? n : null;
};

export const parseId = (value: string): number | null => {
  const n = Number(value);
  return Number.isInteger(n) && n > 0 ? n : null;
};

export const reviewCode = (id: number) => `Review #${id}`;

export const lotLabel = (id: number, name: string | undefined) => name ?? `Lot #${id}`;

/** "4 ★" – kèm aria-label để trình đọc màn hình đọc "4 out of 5 stars". */
export const ratingText = (rating: number) => `${rating} ★`;

const matchesSearch = (r: ModeratedReviewDto, q: string, lotName: (id: number) => string | undefined) => {
  if (!q) return true;
  const needle = q.toLocaleLowerCase('vi-VN');
  return [r.reviewerName, r.comment, r.reportReason, r.ownerReply, lotName(r.parkingLotId), reviewCode(r.id), `#${r.id}`]
    .some((v) => v?.toLocaleLowerCase('vi-VN').includes(needle));
};

/** Lọc theo search + bãi (không tính sao) – nền để đếm số dòng mỗi mức sao. */
export const filterBase = (rows: ModeratedReviewDto[], f: ReviewFilters, lotName: (id: number) => string | undefined) =>
  rows.filter((r) => (f.lotId === null || r.parkingLotId === f.lotId) && matchesSearch(r, f.search, lotName));

export const filterReviews = (rows: ModeratedReviewDto[], f: ReviewFilters, lotName: (id: number) => string | undefined) =>
  filterBase(rows, f, lotName).filter((r) => f.rating === null || r.rating === f.rating);

export const ratingCounts = (rows: ModeratedReviewDto[]) => {
  const counts = new Map<number, number>(RATINGS.map((n) => [n, 0]));
  rows.forEach((r) => counts.set(r.rating, (counts.get(r.rating) ?? 0) + 1));
  return counts;
};

/** Bãi có trong hàng chờ, sắp theo tên hiển thị. */
export const lotsInQueue = (rows: ModeratedReviewDto[], lotName: (id: number) => string | undefined) =>
  [...new Set(rows.map((r) => r.parkingLotId))]
    .map((id) => ({ id, label: lotLabel(id, lotName(id)) }))
    .sort((a, b) => a.label.localeCompare(b.label, 'vi-VN'));
