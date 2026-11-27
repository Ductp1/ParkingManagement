import { useState, type FormEvent } from 'react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { CLOSED_REASON_LABEL, STATUS_LABEL, addDays, dateTime, errorText, fullDate, ictDate, timeRange, weekLabel } from '@/features/shifts/format';
import { Banner, Button, Facts, Field, Modal, ModalActions, inputClass } from '@/features/shifts/ui';
import type { StaffOption } from './ShiftEditorModal';

/** Figma Owner 15 – ca bắt đầu ngay; staff[0] là chủ bãi (tự trực). */
export function AdHocShiftModal({ lotId, lotName, staff, onClose, onSaved }:
  { lotId: number; lotName: string; staff: StaffOption[]; onClose: () => void; onSaved: (message: string) => void }) {
  const [staffId, setStaffId] = useState(staff[0]?.id ?? 0);
  const [minutes, setMinutes] = useState('120');
  const [cashHolder, setCashHolder] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    const duration = Number(minutes);
    if (!Number.isInteger(duration) || duration < 15 || duration > 1440) { setError('Thời lượng từ 15 đến 1440 phút.'); return; }
    setBusy(true);
    setError('');
    try {
      await shiftApi.adHoc(lotId, { staffUserId: staffId, durationMinutes: duration, isCashHolder: cashHolder });
      onSaved('Đã tạo ca nhanh. Người trực cần bấm “Bắt đầu ca” trước khi vận hành.');
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal title="Tạo ca nhanh" subtitle="Ca bắt đầu ngay. Người trực vẫn cần bấm Bắt đầu ca trước khi vận hành." busy={busy} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        {error && <Banner tone="danger" title="Chưa tạo được ca">{error}</Banner>}
        <Field label="Bãi đỗ"><input className={inputClass} value={lotName} disabled readOnly /></Field>
        <Field label="Người trực" required>
          <select className={inputClass} value={staffId} disabled={busy} onChange={(e) => setStaffId(Number(e.target.value))}>
            {staff.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
          </select>
        </Field>
        <Field label="Thời lượng (phút)" required>
          <input type="number" min={15} max={1440} className={inputClass} value={minutes} disabled={busy} onChange={(e) => setMinutes(e.target.value)} />
        </Field>
        <label className="flex items-center gap-2.5 text-[13px] font-semibold text-[#10172a]">
          <input type="checkbox" className="size-4 accent-[#2c5cf2]" checked={cashHolder} disabled={busy} onChange={(e) => setCashHolder(e.target.checked)} />
          Giữ két
        </label>
        <Banner tone="info" title="Chủ bãi có thể tự trực">Bạn chỉ được vận hành bãi thuộc quyền quản lý của mình và cần có ca đang hoạt động.</Banner>
        <ModalActions>
          <Button variant="secondary" disabled={busy} onClick={onClose}>Hủy</Button>
          <Button type="submit" busy={busy}>Tạo ca nhanh</Button>
        </ModalActions>
      </form>
    </Modal>
  );
}

/** Figma Owner 14. */
export function CopyWeekModal({ lotId, lotName, weekStart, weeks, onClose, onCopied }: {
  lotId: number; lotName: string; weekStart: string; weeks: string[]; onClose: () => void;
  onCopied: (result: { created: number; skipped: string[] }, toWeek: string) => void;
}) {
  const [from, setFrom] = useState(addDays(weekStart, -7));
  const [to, setTo] = useState(weekStart);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const options = [...new Set([...weeks, from, to])].sort();

  async function copy() {
    if (from === to) { setError('Tuần đích phải khác tuần nguồn.'); return; }
    setBusy(true);
    setError('');
    try {
      onCopied(await shiftApi.copyWeek(lotId, from, to), to);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal title="Sao chép tuần trước" subtitle={`Sao chép các ca và phân công tại ${lotName}.`} busy={busy} onClose={onClose}>
      {error && <Banner tone="danger" title="Chưa sao chép được lịch">{error}</Banner>}
      <Field label="Từ tuần" required>
        <select className={inputClass} value={from} disabled={busy} onChange={(e) => setFrom(e.target.value)}>
          {options.map((w) => <option key={w} value={w}>{weekLabel(w)}</option>)}
        </select>
      </Field>
      <Field label="Sang tuần" required>
        <select className={inputClass} value={to} disabled={busy} onChange={(e) => setTo(e.target.value)}>
          {options.map((w) => <option key={w} value={w}>{weekLabel(w)}</option>)}
        </select>
      </Field>
      <Banner tone="info" title="Kiểm tra nhân viên và trùng giờ">Hệ thống kiểm lại phân công tại bãi và ca của từng người trước khi lưu.</Banner>
      <ModalActions>
        <Button variant="secondary" disabled={busy} onClick={onClose}>Hủy</Button>
        <Button busy={busy} onClick={() => void copy()}>Sao chép lịch</Button>
      </ModalActions>
    </Modal>
  );
}

/** Figma Owner 17 – ca đã bắt đầu / đã đóng chỉ xem. */
export function ShiftDetailModal({ shift, who, onClose }: { shift: ShiftView; who: string; onClose: () => void }) {
  return (
    <Modal title="Ca đã bắt đầu" onClose={onClose}
      subtitle={`${who} · ${fullDate(ictDate(shift.scheduledStartUtc))} · ${timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}`}>
      <Banner tone="warning" title="Không thể sửa hoặc xóa ca này">
        Chỉ ca chưa bắt đầu mới thay đổi được. Tiền mặt và bàn giao xem tại tab “Bàn giao & Đối soát”.
      </Banner>
      <Facts items={[
        ['Trạng thái', STATUS_LABEL[shift.status]],
        ['Bắt đầu thực tế', shift.startedAtUtc ? dateTime(shift.startedAtUtc) : '—'],
        ['Kết thúc thực tế', shift.endedAtUtc ? dateTime(shift.endedAtUtc) : '—'],
        ['Giữ két', shift.isCashHolder ? 'Có' : 'Không'],
        ['Lý do đóng', shift.closedReason ? CLOSED_REASON_LABEL[shift.closedReason] : '—'],
      ]} />
      <ModalActions><Button variant="secondary" onClick={onClose}>Đóng</Button></ModalActions>
    </Modal>
  );
}
