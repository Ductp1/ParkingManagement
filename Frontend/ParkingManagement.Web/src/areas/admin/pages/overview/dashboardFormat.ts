/** Nhãn tiếng Việt cho giá trị enum (chuỗi) trong DTO dashboard US-081; giá trị lạ hiện nguyên văn. */

const DEVICE_TYPES: Record<string, string> = {
  AnprCamera: 'Camera ANPR',
  Webcam: 'Webcam',
  Barrier: 'Barie',
  Kiosk: 'Kiosk',
  QrScanner: 'Máy quét QR',
  SlotSensor: 'Cảm biến ô đỗ',
};

const PAYMENT_METHODS: Record<string, string> = {
  Vnpay: 'VNPAY',
  Momo: 'MoMo',
  VietQr: 'VietQR',
  Cash: 'Tiền mặt',
};

const LOT_STATUSES: Record<string, string> = {
  Active: 'Đang hoạt động',
  Suspended: 'Tạm khóa',
  PendingApproval: 'Chờ duyệt',
};

const INTEGRATION_TIERS: Record<string, string> = {
  Manual: 'Thủ công',
  PmsApi: 'PMS API',
  EdgeIot: 'Edge IoT',
};

export const deviceTypeLabel = (value: string): string => DEVICE_TYPES[value] ?? value;
export const integrationTierLabel = (value: string): string => INTEGRATION_TIERS[value] ?? value;
export const paymentMethodLabel = (value: string): string => PAYMENT_METHODS[value] ?? value;
export const lotStatusLabel = (value: string): string => LOT_STATUSES[value] ?? value;

/** Ngưỡng cảnh báo lấp đầy (US-081): từ 90% trở lên. */
export const OCCUPANCY_WARNING_PERCENT = 90;
