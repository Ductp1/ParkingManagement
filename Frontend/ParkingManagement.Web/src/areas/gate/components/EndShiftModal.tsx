import { useState, type FormEvent } from 'react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { errorText, fullDate, ictDate, money, parseMoney, timeRange } from '@/features/shifts/format';
import { Banner, Button, Facts, Field, Modal, ModalActions, Placeholder, inputClass, useLoad } from '@/features/shifts/ui';

interface Props {
  shift: ShiftView;
  subtitle: string;
  who: (userId: number) => string;
  onClose: () => void;
  onEnded: () => void;
}

/** Figma Gate 20–22. Số phải có = tiền đầu ca + tiền thu trong ca (R19). */
export function EndShiftModal({ shift, subtitle, who, onClose, onEnded }: Props) {
  const summary = useLoad(`end:${shift.id}`, () => shiftApi.summary(shift.id));
  const [text, setText] = useState('');
  const [note, setNote] = useState('');
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const data = summary.data;
  const counted = parseMoney(text);
  const header = `${subtitle} · ${fullDate(ictDate(shift.scheduledStartUtc))}, ${timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}`;

  // Chưa biết tổng tiền thì không cho kết ca giữ két (không coi là 0).
  if (summary.error != null || (data && shift.isCashHolder && data.cashSystem === null)) {
    return (
      <Modal title="Chưa thể kết thúc ca" subtitle="Ca của bạn vẫn đang hoạt động. Không ghi nhận tiền mặt bằng 0." onClose={onClose}>
        <Banner tone="danger" title="Không thể tải tổng tiền mặt">
          {summary.error != null ? errorText(summary.error) : 'Dịch vụ thanh toán chưa phản hồi. Hãy thử lại trước khi kết ca.'}
        </Banner>
        <Facts items={[['Tiền hệ thống', <span className="text-[#dc2626]">Chưa tải được</span>], ['Xe đang trong bãi', data ? `${data.vehiclesInLot} xe` : '—']]} />
        <ModalActions>
          <Button variant="secondary" onClick={onClose}>Tiếp tục trực</Button>
          <Button busy={summary.loading} onClick={summary.reload}>Thử lại</Button>
        </ModalActions>
      </Modal>
    );
  }

  const difference = counted !== null && data?.expectedCash != null ? counted - data.expectedCash : null;
  const receiver = data?.handoverTo;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (busy || !data || (shift.isCashHolder && counted === null)) return;
    setBusy(true);
    setError('');
    try {
      await shiftApi.end(shift.id, shift.isCashHolder ? counted : null, note.trim());
      onEnded();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal title="Kết thúc ca & Bàn giao" subtitle={header} busy={busy} onClose={onClose}>
      {!data ? <Placeholder kind="loading" title="Đang tổng hợp ca" /> : (
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          <div className="rounded-[10px] border border-[#e3e8f0] p-4">
            <Facts items={[
              ['Xe đang trong bãi', `${data.vehiclesInLot} xe`],
              ['Xe vào / Xe ra', `${data.entries} / ${data.exits}`],
              ...(shift.isCashHolder ? ([
                ['Tiền đầu ca', money(data.openingCash)],
                ['Tiền thu trong ca', money(data.cashSystem ?? 0)],
                ['Số phải có = tiền đầu ca + tiền thu trong ca', money(data.expectedCash ?? 0)],
              ] as Array<[string, string]>) : []),
              ['Ngoại lệ trong ca / PMS chờ', `${data.exceptions} / ${data.pmsPending}`],
              ['Mở barie tay / No-show hủy', `${data.manualBarrierOpens} / ${data.noShowCancels}`],
            ]} />
          </div>
          {shift.isCashHolder ? (
            <>
              <Field label="Tiền mặt đếm được" required error={touched && counted === null ? 'Hãy đếm két và nhập số tiền mặt.' : null}>
                <input className={inputClass} inputMode="numeric" value={text} disabled={busy} aria-invalid={touched && counted === null}
                  placeholder="Nhập số tiền đếm được trong két" onChange={(e) => setText(e.target.value)} />
              </Field>
              {difference !== null && (
                <p role="status" className={`text-xs font-semibold ${difference === 0 ? 'text-[#15803d]' : 'text-[#d97706]'}`}>
                  {difference === 0 ? 'Khớp số phải có · Chênh lệch 0 ₫' : `Chênh lệch ${money(difference)} · Không chặn kết ca, Chủ bãi sẽ rà soát.`}
                </p>
              )}
            </>
          ) : <p className="text-[13px] text-[#65738c]">Ca không giữ két: kết ca không cần đếm tiền.</p>}
          <Field label="Ghi chú bàn giao" hint={`${note.length}/1.000`}>
            <textarea className={`${inputClass} h-24 py-2`} value={note} maxLength={1000} disabled={busy}
              placeholder="Nhập ghi chú cho người nhận (tối đa 1.000 ký tự)" onChange={(e) => setNote(e.target.value)} />
          </Field>
          {shift.isCashHolder && (
            <p className="text-[13px] font-semibold text-[#10172a]">
              Người nhận: {receiver ? `${who(receiver.staffUserId)} · Ca ${timeRange(receiver.scheduledStartUtc, receiver.scheduledEndUtc)}` : 'Chủ bãi (không có ca giữ két kế tiếp)'}
            </p>
          )}
          {data.pmsPending > 0 && <p className="text-xs text-[#65738c]">{data.pmsPending} sự kiện PMS đang chờ xử lý sẽ được bàn giao, không chặn kết ca.</p>}
          {error && <Banner tone="danger" title="Chưa kết thúc được ca">{error}</Banner>}
          <ModalActions>
            <Button variant="secondary" disabled={busy} onClick={onClose}>Tiếp tục trực</Button>
            <Button type="submit" busy={busy}>{shift.isCashHolder ? 'Kết thúc & Gửi bàn giao' : 'Kết thúc ca'}</Button>
          </ModalActions>
        </form>
      )}
    </Modal>
  );
}
