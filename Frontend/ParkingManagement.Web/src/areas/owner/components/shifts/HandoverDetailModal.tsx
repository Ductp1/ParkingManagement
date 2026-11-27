import { useState } from 'react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { CLOSED_REASON_LABEL, STATUS_LABEL, errorText, fullDate, ictDate, money, timeRange } from '@/features/shifts/format';
import { cashGaps, isPendingHandover } from '@/features/shifts/handover';
import { Banner, Button, Facts, Field, LoadError, Modal, ModalActions, MoneyBox, Placeholder, inputClass, useLoad } from '@/features/shifts/ui';

interface Props {
  shift: ShiftView;
  lotName: string;
  who: (userId: number) => string;
  onClose: () => void;
  onReceive: () => void;
  onResolved: () => void;
}

/** Figma Owner 21, 22, 24. */
export function HandoverDetailModal({ shift, lotName, who, onClose, onReceive, onResolved }: Props) {
  const summary = useLoad(`summary:${shift.id}`, () => shiftApi.summary(shift.id));
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const data = summary.data;
  const system = shift.cashSystem ?? data?.cashSystem ?? null;
  const gaps = cashGaps({ ...shift, cashSystem: system }, data?.openingCash);
  const open = shift.discrepancyStatus === 'Open';
  const resolved = shift.discrepancyStatus === 'Resolved';

  async function resolve() {
    if (!note.trim()) { setError('Hãy nhập ghi chú xử lý.'); return; }
    setBusy(true);
    setError('');
    try {
      await shiftApi.resolveDiscrepancy(shift.id, note.trim());
      onResolved();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal busy={busy} onClose={onClose} subtitle={`${who(shift.staffUserId)} · ${lotName} · ${fullDate(ictDate(shift.scheduledStartUtc))}`}
      title={resolved ? 'Đã xử lý chênh lệch' : open && gaps.handover ? 'Rà soát chênh lệch bàn giao' : 'Chi tiết bàn giao ca'}>
      <Facts items={[
        ['Ca dự kiến', timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)],
        ['Ca thực tế', shift.startedAtUtc && shift.endedAtUtc ? timeRange(shift.startedAtUtc, shift.endedAtUtc) : '—'],
        ['Trạng thái', shift.status === 'AutoClosed' && shift.handoverAcceptedAtUtc ? 'Tự đóng – người nhận đã đếm' : STATUS_LABEL[shift.status]],
        ['Lý do đóng', shift.closedReason ? CLOSED_REASON_LABEL[shift.closedReason] : '—'],
      ]} />
      <div className="flex gap-3">
        <MoneyBox label="Hệ thống" value={system === null ? 'Chưa tổng hợp' : money(system)} />
        <MoneyBox label="Người giao đếm" value={shift.cashCounted === null ? 'Chưa nhập' : money(shift.cashCounted)} />
        {shift.cashReceived !== null && <MoneyBox highlight label="Người nhận đếm" value={money(shift.cashReceived)} />}
      </div>
      {(open || resolved) && (
        <Banner tone={open ? 'warning' : 'success'} title={`Chênh lệch tiền mặt · ${open ? 'Chưa xử lý' : 'Đã xử lý'}`}>
          {gaps.end !== null && gaps.end !== 0 && <p>Cuối ca (đếm − số phải có): {money(gaps.end)}</p>}
          {gaps.handover !== null && gaps.handover !== 0 && <p>Khi bàn giao (người nhận − người giao): {money(gaps.handover)}</p>}
          {shift.discrepancyNote && <p className="mt-1 break-words text-[#65738c]">{shift.discrepancyNote}</p>}
        </Banner>
      )}
      {summary.error != null ? <LoadError title="Không tải được tổng hợp ca" error={summary.error} onRetry={summary.reload} />
        : !data ? <Placeholder kind="loading" title="Đang tải tổng hợp ca" />
          : (
            <Facts items={[
              ['Tiền đầu ca', money(data.openingCash)],
              ['Số phải có (đầu ca + thu trong ca)', system === null ? 'Chưa tổng hợp' : money(data.openingCash + system)],
              ['Xe trong bãi / Vào / Ra', `${data.vehiclesInLot} / ${data.entries} / ${data.exits}`],
              ['Ngoại lệ trong ca / PMS chờ', `${data.exceptions} / ${data.pmsPending}`],
              ['Mở barie tay / No-show hủy', `${data.manualBarrierOpens} / ${data.noShowCancels}`],
              ['Người nhận kế tiếp', data.handoverTo ? `${who(data.handoverTo.staffUserId)} · ${timeRange(data.handoverTo.scheduledStartUtc, data.handoverTo.scheduledEndUtc)}` : 'Chủ bãi'],
              ['Ghi chú bàn giao', shift.handoverNote || '—'],
            ]} />
          )}
      {open && (
        <>
          <p className="text-xs text-[#65738c]">Chỉ Chủ bãi được xử lý chênh lệch. Nhân viên không tự xóa được.</p>
          <Field label="Ghi chú xử lý (bắt buộc)" hint={`${note.length}/500`}>
            <textarea className={`${inputClass} h-24 py-2`} value={note} maxLength={500} disabled={busy}
              placeholder="Nhập kết quả kiểm tra và lý do chênh lệch" onChange={(e) => setNote(e.target.value)} />
          </Field>
        </>
      )}
      {error && <Banner tone="danger" title="Chưa lưu được">{error}</Banner>}
      <ModalActions>
        <Button variant="secondary" disabled={busy} onClick={onClose}>Đóng</Button>
        {isPendingHandover(shift) && <Button variant={open ? 'secondary' : 'primary'} disabled={busy} onClick={onReceive}>Đếm lại & nhận bàn giao</Button>}
        {open && <Button busy={busy} onClick={() => void resolve()}>Đánh dấu đã xử lý</Button>}
      </ModalActions>
    </Modal>
  );
}
