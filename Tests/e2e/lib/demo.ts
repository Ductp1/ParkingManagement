// Dữ liệu demo do seeder 9 service tạo trên DB mới (khớp BuildingBlocks/.../DemoIds.cs).

export type Role = 'driver' | 'owner' | 'ownerTsn' | 'staff' | 'admin';

export const ACCOUNTS: Record<Role, { email: string; userId: number; ownerProfileId?: number; landing: string }> = {
  driver: { email: 'driver1@smartparking.vn', userId: 5, landing: '/driver' },
  // Chủ bãi Vincom có cả role Driver → UI đưa về /driver; dùng cho API (OwnerProfileId = 1).
  owner: { email: 'owner.vincom@smartparking.vn', userId: 2, ownerProfileId: 1, landing: '/driver' },
  ownerTsn: { email: 'owner.tsn@smartparking.vn', userId: 3, ownerProfileId: 2, landing: '/owner' },
  staff: { email: 'staff.vincom@smartparking.vn', userId: 4, landing: '/gate' },
  admin: { email: 'admin@smartparking.vn', userId: 1, landing: '/admin' },
};

export const LOT_VINCOM = {
  id: 1,
  name: 'Bãi xe Vincom Đồng Khởi',
  ownerProfileId: 1,
  lat: 10.7781,
  lng: 106.7019,
};
export const LOT_TSN_ID = 2;
export const DRIVER_CAR_ID = 1;

/** Tranh chấp seed CP-0001 thuộc bãi Vincom, đang chờ chủ bãi phản hồi (US-089). */
export const DISPUTE_AWAITING_OWNER = 'CP-0001';
