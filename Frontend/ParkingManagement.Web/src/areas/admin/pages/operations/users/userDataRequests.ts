import { adminDataRequestsApi } from '@/api/adminIdentityApi';
import { DataRequestStatus } from '@/types/adminEnums';
import { IDENTITY_PAGE_SIZE, type AdminDataRequestDto } from '@/types/adminIdentity';

/**
 * US-096 + US-007: yêu cầu dữ liệu cá nhân của MỘT người dùng cho trang User detail.
 * UserService chỉ có danh sách admin theo status / type / page (20 mục, không lọc userId – handoff Lê Quang Trường),
 * nên đọc lần lượt tối đa MAX_PAGES trang rồi lọc phía client.
 */
export const MAX_PAGES = 5;

export interface UserDataRequestsResult {
  items: AdminDataRequestDto[];
  /** Đã đọc đủ MAX_PAGES trang mà vẫn còn → có thể còn yêu cầu cũ hơn chưa xét. */
  truncated: boolean;
  /** Số yêu cầu đã xét (để ghi chú khi truncated). */
  scanned: number;
}

export const loadUserDataRequests = async (userId: number): Promise<UserDataRequestsResult> => {
  const items: AdminDataRequestDto[] = [];
  let scanned = 0;
  for (let page = 1; page <= MAX_PAGES; page++) {
    const rows = await adminDataRequestsApi.list({ page });
    scanned += rows.length;
    items.push(...rows.filter((r) => r.userId === userId));
    if (rows.length < IDENTITY_PAGE_SIZE) return { items, truncated: false, scanned };
  }
  return { items, truncated: true, scanned };
};

export const requestCode = (id: number) => `DSR-${String(id).padStart(5, '0')}`;

export const isOpenRequest = (r: AdminDataRequestDto) => r.status === DataRequestStatus.Submitted || r.status === DataRequestStatus.InProgress;
