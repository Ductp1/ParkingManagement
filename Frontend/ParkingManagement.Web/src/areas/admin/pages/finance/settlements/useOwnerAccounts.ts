import { useMemo } from 'react';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type { OwnerPayoutAccountDto } from '@/types/adminSettlements';
import { useAdminQuery } from '../../../hooks/useAdminQuery';

/**
 * Tên chủ bãi + tài khoản nhận tiền (đã che) cho các chủ bãi đang hiển thị – một lần gọi AdminService.
 * AdminService lỗi → map rỗng, màn hình hiện "OwnerProfile #id" (không chặn danh sách quyết toán).
 */
export const useOwnerAccounts = (ownerProfileIds: number[]) => {
  const ids = useMemo(() => [...new Set(ownerProfileIds.filter((id) => id > 0))].sort((a, b) => a - b), [ownerProfileIds]);
  const key = ids.join(',');
  const query = useAdminQuery(ids.length === 0 ? null : () => adminSettlementsApi.payoutAccounts(ids), [key]);
  const byOwner = useMemo(
    () => new Map<number, OwnerPayoutAccountDto>((query.data?.items ?? []).map((a) => [a.ownerProfileId, a])),
    [query.data]
  );
  return { byOwner, status: query.status, error: query.error };
};
