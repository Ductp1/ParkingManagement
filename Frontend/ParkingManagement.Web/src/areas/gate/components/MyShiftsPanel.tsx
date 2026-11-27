import { useState } from 'react';
import { Link } from 'react-router-dom';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { CLOSED_REASON_LABEL, STATUS_LABEL, dateTime, dayLabel, errorText, fullDate, ictDate, ictTime, lotStatusLabel, money, timeRange } from '@/features/shifts/format';
import { Banner, Button, Facts, Modal, ModalActions, Placeholder, cardClass } from '@/features/shifts/ui';

/** Khớp "Gate:Shift:*" của GateService: được Start sớm 15 phút. Server vẫn là nơi quyết định. */
const START_EARLY_MS = 15 * 60_000;
const DAY_MS = 24 * 3_600_000;

export interface LotInfo { name: string; status?: string }

interface Props {
  shifts: ShiftView[];
  now: number;
  fullName: string;
  isOwner: boolean;
  lot: (id: number) => LotInfo;
  onStarted: () => void;
}

export function MyShiftsPanel({ shifts, now, fullName, isOwner, lot, onStarted }: Props) {
  const [starting, setStarting] = useState<ShiftView | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const open = shifts.find((s) => s.status === 'Open');
  const next = shifts.find((s) => s.status === 'Scheduled' && Date.parse(s.scheduledEndUtc) > now);
  const recent = (s: ShiftView) => now - Date.parse(s.endedAtUtc ?? s.scheduledEndUtc) < DAY_MS;
  const newest = [...shifts].reverse();
  const forced = newest.find((s) => s.status === 'AutoClosed' && recent(s));
  const missed = newest.find((s) => s.status === 'Missed' && recent(s));
  const waiting = newest.find((s) => s.status === 'HandoverPending' && s.handoverAcceptedAtUtc === null);
  const startFrom = next ? Date.parse(next.scheduledStartUtc) - START_EARLY_MS : 0;
  const tooEarly = !!next && now < startFrom;

  async function start() {
    if (!starting) return;
    setBusy(true);
    setError('');
    try {
      await shiftApi.start(starting.id);
      setStarting(null);
      onStarted();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {!open && <Banner tone="warning" title="Chưa có ca đang hoạt động">Bạn có thể xem booking và lượt xe. Bắt đầu ca để check-in/out, thu tiền và vận hành cổng.</Banner>}
      {forced && (
        <Banner tone="danger" title={`Ca đã tự đóng · ${CLOSED_REASON_LABEL[forced.closedReason ?? 'Forgot']}`}>
          Ca {timeRange(forced.scheduledStartUtc, forced.scheduledEndUtc)} ngày {fullDate(forced.businessDate)} đã đóng. Chủ bãi sẽ kiểm tra tiền mặt
          {forced.closedReason === 'Forgot' || !forced.closedReason ? '.' : '; quyền ghi tại cổng đã bị chặn, xe được xử lý bởi người trực khác.'}
        </Banner>
      )}
      {missed && (
        <Banner tone="warning" title="Ca vắng">
          Ca {timeRange(missed.scheduledStartUtc, missed.scheduledEndUtc)} ngày {fullDate(missed.businessDate)} không được bắt đầu kịp giờ nên được đánh dấu vắng. Liên hệ Chủ bãi nếu cần xếp ca khác.
        </Banner>
      )}
      {waiting && (
        <Banner tone="info" title="Đã kết ca — chờ nhận bàn giao">
          Tiền hệ thống {waiting.cashSystem === null ? 'chưa tổng hợp' : money(waiting.cashSystem)} · Thực đếm {waiting.cashCounted === null ? 'chưa nhập' : money(waiting.cashCounted)}.
          {waiting.discrepancyStatus === 'Open' && ' Chênh lệch đã được gửi Chủ bãi rà soát.'} {waiting.handoverToShiftId === null ? 'Chủ bãi sẽ nhận bàn giao.' : 'Ca kế tiếp sẽ đếm lại và nhận.'}
        </Banner>
      )}

      {next ? (
        <>
          {tooEarly && (
            <Banner tone="warning" title="Chưa đến giờ bắt đầu ca">
              Bạn có thể bắt đầu ca {ictTime(next.scheduledStartUtc)} từ {dateTime(new Date(startFrom).toISOString())}. Hiện tại là {ictTime(now)} ICT.
            </Banner>
          )}
          {lot(next.parkingLotId).status && lot(next.parkingLotId).status !== 'Active' && (
            <Banner tone="danger" title="Bãi hiện không hoạt động">Bãi không nhận xe vào. Chỉ bắt đầu ca được khi còn xe trong bãi cần cho ra; liên hệ Chủ bãi nếu cần.</Banner>
          )}
          <section className={`${cardClass} flex flex-wrap items-start justify-between gap-4`}>
            <div>
              <h2 className="text-xl font-bold text-[#10172a]">Ca {timeRange(next.scheduledStartUtc, next.scheduledEndUtc)} · {dayLabel(next.businessDate)}</h2>
              <p className="mt-2 text-[13px] text-[#65738c]">{lot(next.parkingLotId).name} · {lotStatusLabel(lot(next.parkingLotId).status)}{next.isCashHolder ? ' · Giữ két' : ''}</p>
              <p className="mt-2 text-[13px] font-semibold text-[#2c5cf2]">Đã xếp lịch · Có thể bắt đầu từ {ictTime(new Date(startFrom).toISOString())}</p>
              {open && <p className="mt-2 text-[13px] text-[#65738c]">Hãy kết thúc ca đang trực trước khi bắt đầu ca mới.</p>}
            </div>
            <Button disabled={tooEarly || !!open} onClick={() => { setError(''); setStarting(next); }}>Bắt đầu ca</Button>
          </section>
        </>
      ) : !open && (
        <Placeholder kind="empty" title="Chưa được xếp ca" message={isOwner ? 'Tạo ca nhanh để tự trực bãi của bạn.' : 'Liên hệ Chủ bãi để được xếp ca.'}>
          {isOwner && <Link to="/owner/staff/shifts" className="text-sm font-semibold text-[#2c5cf2] hover:underline">Mở Lịch ca</Link>}
        </Placeholder>
      )}

      {shifts.length > 0 && (
        <section className={cardClass}>
          <h2 className="mb-3 text-sm font-semibold text-[#10172a]">Lịch của tôi</h2>
          <ul className="flex flex-col gap-2">
            {shifts.map((s) => (
              <li key={s.id} className="grid grid-cols-2 gap-2 rounded bg-[#f2f5f9] px-3 py-3 text-[13px] sm:grid-cols-4">
                <span className="font-semibold">{dayLabel(s.businessDate)}</span>
                <span className="font-mono text-xs">{timeRange(s.scheduledStartUtc, s.scheduledEndUtc)}</span>
                <span className="text-[#65738c]">{lot(s.parkingLotId).name}</span>
                <span className="text-xs font-semibold text-[#2c5cf2]">{STATUS_LABEL[s.status]}</span>
              </li>
            ))}
          </ul>
          <p className="mt-3 text-[11px] text-[#65738c]">Bắt đầu sớm tối đa 15 phút. Không bắt đầu sau 30 phút sẽ bị đánh dấu vắng · Giờ ICT (UTC+7)</p>
        </section>
      )}

      {starting && (
        <Modal title="Bắt đầu ca?" busy={busy} onClose={() => setStarting(null)}
          subtitle={`${fullName} · ${lot(starting.parkingLotId).name} · ${fullDate(ictDate(starting.scheduledStartUtc))}`}>
          <Facts items={[
            ['Dự kiến', timeRange(starting.scheduledStartUtc, starting.scheduledEndUtc)],
            ['Giờ hiện tại', `${ictTime(now)} ICT`],
            ['Giữ két', starting.isCashHolder ? 'Có – bạn được thu tiền mặt' : 'Không'],
          ]} />
          {error && <Banner tone="danger" title="Chưa bắt đầu được ca">{error}</Banner>}
          <ModalActions>
            <Button variant="secondary" disabled={busy} onClick={() => setStarting(null)}>Để sau</Button>
            <Button busy={busy} onClick={() => void start()}>Bắt đầu ca</Button>
          </ModalActions>
        </Modal>
      )}
    </>
  );
}
