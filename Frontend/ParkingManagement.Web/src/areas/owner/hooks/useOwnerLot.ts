import { useSearchParams } from 'react-router-dom';
import { ownerStaffApi } from '@/api/ownerStaffApi';
import { useLoad } from '@/features/shifts/ui';
import { useAppSelector } from '@/store';

/** Bãi đang chọn (?lot=, mặc định bãi đầu) và nhân viên của bãi đó – dùng cho Lịch ca và Bàn giao. */
export function useOwnerLot() {
  const [params, setParams] = useSearchParams();
  const selfId = useAppSelector((s) => s.auth.user?.id) ?? 0;
  const lots = useLoad('lots', () => ownerStaffApi.getOwnerLots());
  const lotId = Number(params.get('lot')) || lots.data?.[0]?.id || 0;
  const staff = useLoad(`staff:${lotId}`, async () => (lotId ? ownerStaffApi.getAllStaff(lotId) : []));
  const lot = lots.data?.find((l) => l.id === lotId);
  // Chủ bãi đứng đầu: được tự trực bãi của mình mà không cần phân công.
  const options = [{ id: selfId, label: 'Bạn (Chủ bãi)' }, ...(staff.data ?? []).map((s) => ({ id: s.userId, label: s.fullName }))];
  const names = new Map(options.map((o) => [o.id, o.label]));
  const setParam = (key: string, value: string) => setParams((p) => { p.set(key, value); return p; });
  return { params, setParam, lots, lotId, lot, lotName: lot?.name ?? `Bãi #${lotId}`, staff, options, names, selfId };
}
