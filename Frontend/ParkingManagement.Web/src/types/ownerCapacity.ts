export type IntegrationTier = 'Manual' | 'PmsApi' | 'EdgeIot';

export interface LotCapacityConfigDto {
  parkingLotId: number;
  parkingLotName: string;
  totalSlots: number;
  integrationTier: IntegrationTier;
  onlineQuota: number;
  walkInBufferPercent: number;
  calculatedWalkInBufferSlots: number;
  maxAllowedOnlineQuota: number;
  onlineLockThreshold: number;
  instantBookingPaused: boolean;
  isEmergencyStopped: boolean;
  emergencyReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface UpdateLotCapacityConfigRequest {
  ownerProfileId?: number;
  onlineQuota: number;
  walkInBufferPercent: number;
  onlineLockThreshold: number;
  instantBookingPaused?: boolean;
}

export const INTEGRATION_TIER_META: Record<
  IntegrationTier,
  { label: string; badgeClass: string; description: string; recommendedBuffer: number }
> = {
  Manual: {
    label: 'Mức 0: Bãi thủ công / Truyền thống',
    badgeClass: 'bg-amber-100 text-amber-800 border-amber-300',
    description: 'Chưa lắp barie tự động; nên dự trữ vùng đệm 30%–50% để tránh quá tải tại cổng.',
    recommendedBuffer: 30,
  },
  PmsApi: {
    label: 'Mức 1: Tích hợp phần mềm PMS API',
    badgeClass: 'bg-blue-100 text-blue-800 border-blue-300',
    description: 'Đồng bộ qua API hệ thống quản lý bãi xe; khuyến nghị vùng đệm an toàn 10%–20%.',
    recommendedBuffer: 10,
  },
  EdgeIot: {
    label: 'Mức 2: Thiết bị cổng thông minh Edge IoT / ANPR',
    badgeClass: 'bg-emerald-100 text-emerald-800 border-emerald-300',
    description: 'Nhận diện biển số tự động theo thời gian thực; khuyến nghị vùng đệm 10% để tối đa doanh thu.',
    recommendedBuffer: 10,
  },
};
