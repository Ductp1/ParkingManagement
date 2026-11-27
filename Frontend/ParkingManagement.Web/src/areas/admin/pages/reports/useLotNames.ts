import { useMemo } from 'react';
import { adminRequest } from '@/api/adminClientApi';
import { useAdminQuery } from '../../hooks/useAdminQuery';

interface LotNameRow {
  parkingLotId: number;
  name: string;
}

/**
 * Tên bãi cho panel "Top lots" (ParkingService lots-summary, RequireAdmin). Nguồn lỗi hoặc thiếu bãi → caller hiện "Lot #id".
 * Nhánh Parking Lots (P0-3) có useLotOptions dùng chung; sau khi cả hai merge sẽ gộp về một hook (Nhánh 5).
 */
export const useLotNames = (): ((id: number) => string | undefined) => {
  const query = useAdminQuery(
    () => adminRequest<{ lots: LotNameRow[] }>({ method: 'GET', url: '/api/v1/parking-lots/admin/lots-summary' }),
    []
  );
  return useMemo(() => {
    const names = new Map((query.data?.lots ?? []).map((l) => [l.parkingLotId, l.name]));
    return (id: number) => names.get(id);
  }, [query.data]);
};
