/**
 * Tham chiếu đặt chỗ nhúng trong dataJson của thông báo:
 *  - bookingId   : id đặt chỗ (thông báo seed demo, VD {"bookingId":2})
 *  - bookingCode : mã vé hiển thị (luồng đặt chỗ, VD {"bookingCode":"#PKM-2025-8842"})
 * Dùng chung cho chuông thông báo (MainNavbar) và toast real-time (NotificationToaster)
 * để điều hướng tới trang thông tin đặt chỗ (/driver/history).
 */
export interface NotificationBookingRef {
  bookingId: string | null;
  bookingCode: string | null;
}

export const parseNotificationBookingRef = (dataJson: string | null): NotificationBookingRef => {
  if (!dataJson) return { bookingId: null, bookingCode: null };
  try {
    const data = JSON.parse(dataJson) as Record<string, unknown>;
    const bookingId =
      typeof data.bookingId === 'number' || typeof data.bookingId === 'string'
        ? String(data.bookingId)
        : null;
    const bookingCode = typeof data.bookingCode === 'string' ? data.bookingCode : null;
    return { bookingId, bookingCode };
  } catch {
    return { bookingId: null, bookingCode: null };
  }
};

/** Thông báo có tham chiếu đặt chỗ hay không (quyết định hiển thị hint "Xem đặt chỗ"). */
export const hasBookingRef = (dataJson: string | null): boolean => {
  const ref = parseNotificationBookingRef(dataJson);
  return ref.bookingId !== null || ref.bookingCode !== null;
};

/**
 * Route thông tin đặt chỗ của thông báo:
 *  - có bookingId/bookingCode → /driver/history?booking=<id|code> (BookingHistoryPage highlight đúng vé)
 *  - không có → /driver/history (danh sách đặt chỗ)
 */
export const notificationTargetRoute = (dataJson: string | null): string => {
  const ref = parseNotificationBookingRef(dataJson);
  const booking = ref.bookingId ?? ref.bookingCode;
  return booking ? `/driver/history?booking=${encodeURIComponent(booking)}` : '/driver/history';
};
