import { useState } from 'react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { addDays, fullDate, ictDate, mondayOf, money, staffName, timeRange, weekLabel } from '@/features/shifts/format';
import { isPendingHandover } from '@/features/shifts/handover';
import { ReceiveHandoverModal } from '@/features/shifts/ReceiveHandoverModal';
import { Banner, Field, LinkButton, LoadError, Page, Placeholder, StaffTabs, cardClass, inputClass, useLoad } from '@/features/shifts/ui';
import { useOwnerLot } from '../hooks/useOwnerLot';
import { HandoverDetailModal } from '../components/shifts/HandoverDetailModal';

const FILTERS: Array<[string, string, (s: ShiftView) => boolean]> = [
  ['discrepancy', 'Chênh lệch cần xử lý', (s) => s.discrepancyStatus === 'Open'],
  ['pending', 'Chờ nhận bàn giao', isPendingHandover],
  ['totals', 'Chờ tổng hợp tiền', (s) => s.cashSystem === null],
];

const handoverText = (s: ShiftView) => {
  if (isPendingHandover(s)) {
    const waiting = s.handoverToShiftId === null ? 'Chờ chủ bãi nhận' : 'Chờ ca sau nhận';
    return s.status === 'AutoClosed' ? `Tự đóng · ${waiting.toLowerCase()}` : waiting;
  }
  return s.status === 'AutoClosed' ? 'Tự đóng · người nhận đã đếm' : 'Hoàn tất';
};

export function OwnerHandoverListPage() {
  const owner = useOwnerLot();
  const { lotId, lotName, names, selfId } = owner;
  const [week, setWeek] = useState('');
  const [filter, setFilter] = useState('');
  const [viewing, setViewing] = useState<{ shift: ShiftView; receive: boolean } | null>(null);
  const [flash, setFlash] = useState('');
  const handovers = useLoad(`handovers:${lotId}`, async () => (lotId ? shiftApi.handovers(lotId) : []));

  const who = (id: number) => staffName(id, names, selfId);
  const thisWeek = mondayOf(ictDate(Date.now()));
  const inWeek = (handovers.data ?? []).filter((s) => !week || mondayOf(s.businessDate) === week);
  const match = FILTERS.find(([key]) => key === filter)?.[2];
  const rows = match ? inWeek.filter(match) : inWeek;
  const done = (message: string) => { setViewing(null); setFlash(message); handovers.reload(); };

  return (
    <Page title="Bàn giao & Đối soát" subtitle="Kiểm tra tiền mặt, nhận bàn giao và xử lý các ca cần rà soát.">
      <StaffTabs />
      {flash && <Banner tone="success" title={flash} onClose={() => setFlash('')} />}
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Bãi đỗ">
          <select className={inputClass} value={lotId} onChange={(e) => owner.setParam('lot', e.target.value)}>
            {owner.lots.data?.map((l) => <option key={l.id} value={l.id}>{l.name ?? `Bãi #${l.id}`}</option>)}
          </select>
        </Field>
        <Field label="Thời gian">
          <select className={inputClass} value={week} onChange={(e) => setWeek(e.target.value)}>
            <option value="">100 ca gần nhất</option>
            {Array.from({ length: 8 }, (_, i) => addDays(thisWeek, -i * 7)).map((w) => <option key={w} value={w}>{weekLabel(w)}</option>)}
          </select>
        </Field>
        <Field label="Trạng thái">
          <select className={inputClass} value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            {FILTERS.map(([key, label]) => <option key={key} value={key}>{label}</option>)}
          </select>
        </Field>
      </div>
      {owner.lots.error != null && <LoadError title="Không tải được danh sách bãi" error={owner.lots.error} onRetry={owner.lots.reload} />}
      {owner.lots.data?.length === 0 ? <Placeholder kind="empty" title="Chưa có bãi đỗ" />
        : handovers.error != null ? <LoadError title="Không tải được danh sách bàn giao" error={handovers.error} onRetry={handovers.reload} />
          : !handovers.data ? <Placeholder kind="loading" title="Đang tải bàn giao" />
            : (
              <>
                <div className="grid gap-4 md:grid-cols-3">
                  {FILTERS.map(([key, label, test], i) => (
                    <div key={key} className={cardClass}>
                      <p className="text-[11px] font-semibold tracking-wider text-[#65738c] uppercase">{label}</p>
                      <p className={`mt-2 text-2xl font-bold ${['text-[#dc2626]', 'text-[#d97706]', 'text-[#65738c]'][i]}`}>{inWeek.filter(test).length} ca</p>
                    </div>
                  ))}
                </div>
                {rows.length === 0 ? <Placeholder kind="empty" title="Không có ca cần theo dõi" message="Ca giữ két đã kết thúc sẽ hiện tại đây." /> : (
                  <div className={cardClass}>
                    <h2 className="mb-4 text-sm font-semibold text-[#10172a]">Các ca cần theo dõi</h2>
                    <div className="overflow-x-auto rounded-[10px] border border-[#e3e8f0]">
                      <table className="w-full min-w-[860px] text-left text-[13px]">
                        <thead className="bg-[#f2f5f9] text-xs font-semibold tracking-[0.6px] text-[#65738c] uppercase">
                          <tr>{['Ca / Ngày', 'Nhân viên', 'Hệ thống', 'Thực đếm', 'Chênh lệch', 'Bàn giao', 'Thao tác'].map((h) => <th key={h} className="px-4 py-4">{h}</th>)}</tr>
                        </thead>
                        <tbody className="divide-y divide-[#e3e8f0]">
                          {rows.map((s) => (
                            <tr key={s.id} className="h-[68px]">
                              <td className="px-4 py-3"><p className="text-xs font-semibold">{timeRange(s.scheduledStartUtc, s.scheduledEndUtc)}</p><p className="text-[11px] text-[#65738c]">{fullDate(s.businessDate)}</p></td>
                              <td className="px-4 py-3">{who(s.staffUserId)}</td>
                              <td className="px-4 py-3">{s.cashSystem === null ? 'Chờ tổng hợp' : money(s.cashSystem)}</td>
                              <td className="px-4 py-3">{s.cashCounted === null ? 'Chưa nhập' : money(s.cashCounted)}</td>
                              <td className={`px-4 py-3 ${s.discrepancyStatus === 'Open' ? 'font-semibold text-[#dc2626]' : ''}`}>
                                {s.discrepancyStatus === 'Open' ? 'Cần xử lý' : s.discrepancyStatus === 'Resolved' ? 'Đã xử lý'
                                  : s.cashSystem === null || (s.cashCounted === null && s.cashReceived === null) ? 'Chưa xác định' : 'Khớp'}
                              </td>
                              <td className="px-4 py-3">{handoverText(s)}</td>
                              <td className="px-4 py-3">
                                <LinkButton onClick={() => { setFlash(''); setViewing({ shift: s, receive: false }); }}>
                                  {s.discrepancyStatus === 'Open' || isPendingHandover(s) ? 'Kiểm tra' : 'Xem chi tiết'}
                                </LinkButton>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </>
            )}
      <Banner tone="warning" title="Ca tự đóng vẫn cần được kiểm tra">Tiền hệ thống chưa tải được sẽ được bổ sung tự động. Số chưa có không được coi là 0 ₫.</Banner>

      {viewing && !viewing.receive && (
        <HandoverDetailModal shift={viewing.shift} lotName={lotName} who={who} onClose={() => setViewing(null)}
          onReceive={() => setViewing({ ...viewing, receive: true })} onResolved={() => done('Đã đánh dấu chênh lệch là đã xử lý.')} />
      )}
      {viewing?.receive && (
        <ReceiveHandoverModal asOwner shift={viewing.shift} subtitle={`${who(viewing.shift.staffUserId)} → Chủ bãi · ${lotName}`}
          onClose={() => setViewing(null)} onAccepted={() => done('Đã nhận bàn giao tiền mặt.')} />
      )}
    </Page>
  );
}
