/**
 * Types & Data Contracts cho Task UI-19: Lịch hoạt động + Tiện ích bãi xe
 * Phục vụ EP09 / US-062 (Lịch hoạt động 7 ngày) và US-064 (Tiện ích & Thuộc tính bãi xe)
 */

export type DayOfWeekNumber = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export const DAY_OF_WEEK_NAMES: Record<number, string> = {
  1: 'Thứ Hai',
  2: 'Thứ Ba',
  3: 'Thứ Tư',
  4: 'Thứ Năm',
  5: 'Thứ Sáu',
  6: 'Thứ Bảy',
  0: 'Chủ Nhật',
};

// Thứ tự hiển thị chuẩn từ Thứ Hai (1) đến Chủ Nhật (0)
export const WEEK_DAYS_ORDER: DayOfWeekNumber[] = [1, 2, 3, 4, 5, 6, 0];

// ==================== OPERATING HOURS DTOs ====================

export interface LotOperatingHourItemDto {
  dayOfWeek: number | string; // 0 = Sunday, 1 = Monday ... 6 = Saturday
  dayOfWeekNumber: number; // 1 = Monday ... 7 = Sunday
  dayName: string; // "Thứ Hai", "Thứ Ba"...
  openTime: string; // "HH:mm"
  closeTime: string; // "HH:mm"
  isClosed: boolean;
  isOvernight: boolean; // Tự động true nếu OpenTime > CloseTime
  is24Hours: boolean; // Tự động true nếu 00:00 - 00:00 hoặc 24/7
  isDefault: boolean;
  isOverride?: boolean;
  reason?: string | null;
}

export interface LotOperatingHoursWeeklyDto {
  parkingLotId: number;
  parkingLotName: string;
  defaultOpenTime: string;
  defaultCloseTime: string;
  schedule: LotOperatingHourItemDto[];
  year?: number | null;
  weekOfYear?: number | null;
  isCustomWeek?: boolean;
  weekStartDate?: string | null;
  weekEndDate?: string | null;
}

export interface UpdateLotOperatingHourItemRequest {
  dayOfWeek: number;
  openTime: string; // "HH:mm"
  closeTime: string; // "HH:mm"
  isClosed: boolean;
}

export interface UpdateLotOperatingHoursRequest {
  ownerProfileId?: number;
  schedule: UpdateLotOperatingHourItemRequest[];
  year?: number | null;
  weekOfYear?: number | null;
  reason?: string | null;
}

// ==================== AMENITIES DTOs ====================

export interface AmenityCatalogItemDto {
  code: string;
  name: string;
  description: string;
}

export interface LotAmenityDto {
  id: number;
  parkingLotId: number;
  code: string;
  name: string;
  note?: string | null;
}

export interface LotAmenityInputDto {
  code: string;
  name?: string | null;
  note?: string | null;
}

export interface UpdateLotAmenitiesRequest {
  amenities: LotAmenityInputDto[];
}

// ==================== LOT ATTRIBUTES & PHOTOS DTOs ====================

export interface LotPhotoDto {
  id: number;
  parkingLotId: number;
  url: string;
  caption?: string | null;
  sortOrder: number;
  isCover: boolean;
}

export interface LotAttributesDto {
  parkingLotId: number;
  name: string;
  description?: string | null;
  hotlinePhone?: string | null;
  maxHeightCm: number;
  coverImageUrl?: string | null;
  amenities: LotAmenityDto[];
  photos: LotPhotoDto[];
}

export interface UpdateLotAttributesRequest {
  description?: string | null;
  hotlinePhone?: string | null;
  maxHeightCm?: number | null;
}

// ==================== STANDARD CATALOG METADATA ====================

export interface AmenityMeta {
  code: string;
  name: string;
  description: string;
  iconName: string;
  badgeClass: string;
  iconBgClass: string;
  iconColorClass: string;
}

export const STANDARD_AMENITIES_CATALOG: AmenityMeta[] = [
  {
    code: 'COVERED',
    name: 'Bãi có mái che',
    description: 'Hầm đỗ xe hoặc bãi có mái che chắn nắng mưa an toàn',
    iconName: 'Warehouse',
    badgeClass: 'bg-blue-50 text-blue-700 border-blue-200',
    iconBgClass: 'bg-blue-100',
    iconColorClass: 'text-blue-600',
  },
  {
    code: 'CCTV',
    name: 'Camera giám sát 24/7',
    description: 'Hệ thống camera an ninh giám sát liên tục toàn bộ khuôn viên',
    iconName: 'Video',
    badgeClass: 'bg-slate-50 text-slate-700 border-slate-200',
    iconBgClass: 'bg-slate-100',
    iconColorClass: 'text-slate-600',
  },
  {
    code: 'SECURITY_24H',
    name: 'Bảo vệ túc trực 24/7',
    description: 'Có nhân viên bảo vệ điều phối, tuần tra và kiểm soát cổng',
    iconName: 'ShieldCheck',
    badgeClass: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    iconBgClass: 'bg-emerald-100',
    iconColorClass: 'text-emerald-600',
  },
  {
    code: 'EV_CHARGER',
    name: 'Trụ sạc xe điện',
    description: 'Có trang bị trụ sạc AC / DC cho xe ô tô điện VinFast, Tesla...',
    iconName: 'Zap',
    badgeClass: 'bg-amber-50 text-amber-700 border-amber-200',
    iconBgClass: 'bg-amber-100',
    iconColorClass: 'text-amber-600',
  },
  {
    code: 'CAR_WASH',
    name: 'Dịch vụ rửa xe',
    description: 'Có dịch vụ rửa xe bọt tuyết, chăm sóc nội thất tại chỗ',
    iconName: 'Sparkles',
    badgeClass: 'bg-cyan-50 text-cyan-700 border-cyan-200',
    iconBgClass: 'bg-cyan-100',
    iconColorClass: 'text-cyan-600',
  },
  {
    code: 'OVERSIZED',
    name: 'Nhận xe quá khổ',
    description: 'Có ô đỗ kích thước lớn cho xe bán tải, xe van, xe 16 chỗ',
    iconName: 'Truck',
    badgeClass: 'bg-purple-50 text-purple-700 border-purple-200',
    iconBgClass: 'bg-purple-100',
    iconColorClass: 'text-purple-600',
  },
  {
    code: 'DISABLED_ACCESS',
    name: 'Lối đi người khuyết tật',
    description: 'Lối lên xuống dốc thoai thoải và ô đỗ ưu tiên gần thang máy',
    iconName: 'Accessibility',
    badgeClass: 'bg-sky-50 text-sky-700 border-sky-200',
    iconBgClass: 'bg-sky-100',
    iconColorClass: 'text-sky-600',
  },
  {
    code: 'VALET',
    name: 'Dịch vụ đỗ xe hộ',
    description: 'Có nhân sự tiếp nhận và đánh xe vào bãi cho tài xế vội',
    iconName: 'KeyRound',
    badgeClass: 'bg-indigo-50 text-indigo-700 border-indigo-200',
    iconBgClass: 'bg-indigo-100',
    iconColorClass: 'text-indigo-600',
  },
  {
    code: 'SHUTTLE',
    name: 'Xe đưa đón',
    description: 'Xe điện hoặc shuttle bus đưa đón ra sân bay, trung tâm thương mại',
    iconName: 'Bus',
    badgeClass: 'bg-orange-50 text-orange-700 border-orange-200',
    iconBgClass: 'bg-orange-100',
    iconColorClass: 'text-orange-600',
  },
  {
    code: 'TOILET',
    name: 'Nhà vệ sinh công cộng',
    description: 'Khu vệ sinh sạch sẽ, tiện nghi dành cho khách gửi xe',
    iconName: 'DoorClosed',
    badgeClass: 'bg-teal-50 text-teal-700 border-teal-200',
    iconBgClass: 'bg-teal-100',
    iconColorClass: 'text-teal-600',
  },
  {
    code: 'ELEVATOR',
    name: 'Thang máy kết nối',
    description: 'Có thang máy di chuyển nhanh giữa các tầng hầm và mặt đất',
    iconName: 'ArrowUpDown',
    badgeClass: 'bg-violet-50 text-violet-700 border-violet-200',
    iconBgClass: 'bg-violet-100',
    iconColorClass: 'text-violet-600',
  },
  {
    code: 'TIRE_INFLATION',
    name: 'Bơm lốp / Cứu hộ lốp',
    description: 'Trang bị máy bơm hơi điện tử miễn phí và dụng cụ đo áp suất lốp',
    iconName: 'Gauge',
    badgeClass: 'bg-yellow-50 text-yellow-700 border-yellow-200',
    iconBgClass: 'bg-yellow-100',
    iconColorClass: 'text-yellow-600',
  },
];
