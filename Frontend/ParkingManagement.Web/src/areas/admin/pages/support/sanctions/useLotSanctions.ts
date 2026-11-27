import { useState } from 'react';
import { adminSanctionsApi } from '@/api/adminSanctionsApi';
import { SanctionStatus } from '@/types/adminEnums';
import { useAdminQuery } from '../../../hooks/useAdminQuery';

/**
 * Chế tài của một bãi: hiệu lực đang áp bên ParkingService (kèm chủ bãi hiện tại của bãi) + chế tài Active bên AdminService.
 * lotId = null → chưa gọi.
 */
export const useLotSanctions = (lotId: number | null) => {
  const [version, setVersion] = useState(0);
  const state = useAdminQuery(lotId === null ? null : () => adminSanctionsApi.lotState(lotId), [lotId, version]);
  const active = useAdminQuery(
    lotId === null ? null : () => adminSanctionsApi.list({ parkingLotId: lotId, status: SanctionStatus.Active }, 1, 20),
    [lotId, version],
    (d) => d.items.length === 0
  );
  return { state, active, reload: () => setVersion((v) => v + 1) };
};

export type LotSanctions = ReturnType<typeof useLotSanctions>;
