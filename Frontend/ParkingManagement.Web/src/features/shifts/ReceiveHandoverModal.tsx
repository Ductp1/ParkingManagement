import { useState, type FormEvent } from 'react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { errorText, fullDate, ictDate, money, parseMoney, timeRange } from './format';
import { Banner, Button, Facts, Field, Modal, ModalActions, MoneyBox, inputClass, useLoad } from './ui';

interface Props {
  /** Ca giữ két đang chờ bàn giao. */
  shift: ShiftView;
  subtitle: string;
  /** Chủ bãi nhận thay (Figma Owner 23); false = ca kế tiếp (Gate 23, 30–32). */
  asOwner?: boolean;
  onClose: () => void;
  onAccepted: (updated: ShiftView, openingCash: number) => void;
}

/** R18: người nhận đếm lại két và nhập số thực nhận; lệch với số người giao đếm → chủ bãi rà soát. */
export function ReceiveHandoverModal({ shift, subtitle, asOwner, onClose, onAccepted }: Props) {
  const summary = useLoad(`summary:${shift.id}`, () => shiftApi.summary(shift.id));
  const [text, setText] = useState('');
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const received = parseMoney(text);
  const counted = shift.cashCounted;
  const system = shift.cashSystem ?? summary.data?.cashSystem ?? null;
  const opening = summary.data?.openingCash;
  const previous = counted !== null && system !== null && opening !== undefined ? counted - (opening + system) : 0;
  const difference = received !== null && counted !== null ? received - counted : null;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (busy || received === null) return;
    setBusy(true);
    setError('');
    try {
      onAccepted(await shiftApi.acceptHandover(shift.id, received), opening ?? 0);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal title={asOwner ? 'Chủ bãi đếm lại & Nhận bàn giao' : 'Đếm lại tiền & Nhận bàn giao'} subtitle={subtitle} busy={busy} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <Facts items={[['Ca bàn giao', `${fullDate(ictDate(shift.scheduledStartUtc))} · ${timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}`]]} />
        <div className="flex gap-3">
          <MoneyBox highlight label="Người giao đã đếm" value={counted === null ? 'Chưa đếm (ca tự đóng)' : money(counted)} />
          <MoneyBox label="Hệ thống · tham khảo" value={system === null ? 'Chưa tổng hợp' : money(system)} />
        </div>
        <Field label="Tiền thực nhận sau khi đếm lại" required hint="Tự đếm lại tiền trước khi xác nhận; không dùng lại số tiền của người giao."
          error={touched && received === null ? 'Hãy đếm lại và nhập số tiền bạn thực nhận.' : null}>
          <span className="relative">
            <input className={`${inputClass} pr-9`} inputMode="numeric" value={text} disabled={busy} aria-invalid={touched && received === null}
              placeholder="Nhập số tiền bạn thực nhận" onChange={(e) => setText(e.target.value)} onBlur={() => setTouched(true)} />
            <span className="absolute top-1/2 right-3 -translate-y-1/2 text-[#65738c]" aria-hidden="true">₫</span>
          </span>
        </Field>
        {difference !== null && (
          <p role="status" className={`text-xs font-semibold ${difference === 0 ? 'text-[#15803d]' : 'text-[#d97706]'}`}>
            {difference === 0 ? 'Khớp số người giao đã đếm · Chênh lệch 0 ₫' : `Lệch bàn giao: ${money(difference)} · Chủ bãi sẽ được báo để rà soát.`}
          </p>
        )}
        <Banner tone="info" title="Bắt buộc đếm lại trước khi nhận">Số thực nhận được so với tiền người giao đã đếm, không phải tiền hệ thống.</Banner>
        <div className="rounded-[10px] bg-[#f2f5f9] p-3 text-[13px]">
          <p className="text-[11px] font-semibold tracking-wider text-[#65738c] uppercase">Ghi chú của người giao</p>
          <p className="mt-1 break-words text-[#10172a]">{shift.handoverNote || 'Chưa có ghi chú bàn giao.'}</p>
        </div>
        <div className="flex flex-col gap-1 text-xs text-[#65738c]">
          {summary.data && <p>{summary.data.vehiclesInLot} xe đang trong bãi · {summary.data.exceptions} ngoại lệ trong ca · {summary.data.pmsPending} sự kiện PMS chờ xử lý</p>}
          {previous !== 0 && <p>Lệch ca trước (người giao − hệ thống): {money(previous)}, vẫn đang chờ Chủ bãi rà soát.</p>}
          <p>Nhận bàn giao không tự đánh dấu chênh lệch đã được xử lý.</p>
          {asOwner && shift.handoverToShiftId !== null && <p>Bạn đang nhận thay ca kế tiếp: số tiền này sẽ không tính vào tiền đầu ca của họ.</p>}
        </div>
        {error && <Banner tone="danger" title="Chưa nhận được bàn giao">{error}</Banner>}
        <ModalActions>
          <Button variant="secondary" disabled={busy} onClick={onClose}>Để sau</Button>
          <Button type="submit" busy={busy} disabled={received === null}>Xác nhận nhận bàn giao</Button>
        </ModalActions>
      </form>
    </Modal>
  );
}
