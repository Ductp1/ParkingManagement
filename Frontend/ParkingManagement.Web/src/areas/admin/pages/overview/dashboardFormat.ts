import type { DashboardWidget } from './dashboardTypes';

/** Nhãn tiếng Anh cho giá trị enum (chuỗi) trong DTO dashboard US-081; giá trị lạ hiện nguyên văn. */

const DEVICE_TYPES: Record<string, string> = {
  AnprCamera: 'ANPR camera',
  Webcam: 'Webcam',
  Barrier: 'Barrier',
  Kiosk: 'Kiosk',
  QrScanner: 'QR scanner',
  SlotSensor: 'Slot sensor',
};

const PAYMENT_METHODS: Record<string, string> = {
  Vnpay: 'VNPAY',
  Momo: 'MoMo',
  VietQr: 'VietQR',
  Cash: 'Cash',
};

const LOT_STATUSES: Record<string, string> = {
  Active: 'Active',
  Suspended: 'Suspended',
  PendingApproval: 'Pending approval',
};

const INTEGRATION_TIERS: Record<string, string> = {
  Manual: 'Manual',
  PmsApi: 'PMS API',
  EdgeIot: 'Edge IoT',
};

export const deviceTypeLabel = (value: string): string => DEVICE_TYPES[value] ?? value;
export const integrationTierLabel = (value: string): string => INTEGRATION_TIERS[value] ?? value;
export const paymentMethodLabel = (value: string): string => PAYMENT_METHODS[value] ?? value;
export const lotStatusLabel = (value: string): string => LOT_STATUSES[value] ?? value;

/** Ngưỡng cảnh báo lấp đầy (US-081): từ 90% trở lên. */
export const OCCUPANCY_WARNING_PERCENT = 90;

/**
 * Widget chưa có nguồn dữ liệu (unavailable + no_source) thì ẩn hẳn – không "—", không ghi chú.
 * Nguồn có rồi (available) hoặc nguồn lỗi (source_error) thì widget tự hiện lại.
 */
export const isHiddenWidget = (widget: DashboardWidget<unknown> | null): boolean =>
  widget !== null && widget.status === 'unavailable' && widget.reason === 'no_source';
