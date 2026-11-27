import { useState, type FormEvent } from 'react';
import { shiftApi, type ShiftTemplate, type ShiftView } from '@/api/shiftApi';
import { errorText, fullDate, ictDate, ictTime, shiftRangeUtc, timeRange } from '@/features/shifts/format';
import { Banner, Button, Field, Modal, ModalActions, inputClass } from '@/features/shifts/ui';

export interface StaffOption { id: number; label: string }

interface Props {
  lotId: number;
  lotName: string;
  staff: StaffOption[];
  templates: ShiftTemplate[];
  /** Ca đang sửa (chỉ ca Scheduled); không có = thêm ca. */
  shift?: ShiftView;
  initialDate: string;
  initialTemplateId?: number;
  onClose: () => void;
  onSaved: (message: string) => void;
}

export function ShiftEditorModal({ lotId, lotName, staff, templates, shift, initialDate, initialTemplateId, onClose, onSaved }: Props) {
  const preset = templates.find((t) => t.id === (shift ? shift.templateId : initialTemplateId));
  const [staffId, setStaffId] = useState(shift?.staffUserId ?? staff[0]?.id ?? 0);
  const [templateId, setTemplateId] = useState(preset?.id ?? 0);
  const [date, setDate] = useState(shift ? ictDate(shift.scheduledStartUtc) : initialDate);
  const [start, setStart] = useState(shift ? ictTime(shift.scheduledStartUtc) : preset?.startTime.slice(0, 5) ?? '06:00');
  const [end, setEnd] = useState(shift ? ictTime(shift.scheduledEndUtc) : preset?.endTime.slice(0, 5) ?? '14:00');
  const [cashHolder, setCashHolder] = useState(shift?.isCashHolder ?? preset?.isCashHolder ?? true);
  const [busy, setBusy] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [error, setError] = useState('');

  const who = staff.find((s) => s.id === staffId)?.label ?? `Nhân viên #${staffId}`;

  function pickTemplate(id: number) {
    setTemplateId(id);
    const t = templates.find((x) => x.id === id);
    if (t) { setStart(t.startTime.slice(0, 5)); setEnd(t.endTime.slice(0, 5)); setCashHolder(t.isCashHolder); }
  }

  async function run(action: () => Promise<unknown>, message: string) {
    setBusy(true);
    setError('');
    try {
      await action();
      onSaved(message);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    if (!staffId || !date || !start || !end || start === end) { setError('Hãy chọn nhân viên, ngày và giờ bắt đầu khác giờ kết thúc.'); return; }
    const body = { staffUserId: staffId, ...shiftRangeUtc(date, start, end), isCashHolder: cashHolder, templateId: templateId || null };
    void run(() => (shift ? shiftApi.update(shift.id, body) : shiftApi.create(lotId, [body])), shift ? 'Đã cập nhật ca.' : 'Đã thêm ca vào lịch.');
  }

  if (shift && confirmDelete) {
    return (
      <Modal title="Xóa ca đã xếp?" busy={busy} onClose={onClose}
        subtitle={`${who} · ${fullDate(ictDate(shift.scheduledStartUtc))} · ${timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}`}>
        <Banner tone="warning" title="Ca sẽ bị xóa khỏi lịch">Khoảng giờ này có thể thiếu người trực hoặc người giữ két.</Banner>
        {error && <Banner tone="danger" title="Chưa xóa được ca">{error}</Banner>}
        <ModalActions>
          <Button variant="secondary" disabled={busy} onClick={() => { setConfirmDelete(false); setError(''); }}>Giữ ca</Button>
          <Button variant="danger" busy={busy} onClick={() => void run(() => shiftApi.remove(shift.id), 'Đã xóa ca.')}>Xóa ca</Button>
        </ModalActions>
      </Modal>
    );
  }

  return (
    <Modal title={error ? 'Không thể lưu ca' : shift ? 'Chỉnh sửa ca đã xếp' : 'Thêm ca làm việc'} busy={busy} onClose={onClose}
      subtitle={error ? 'Thông tin ca được giữ lại để bạn điều chỉnh.' : `${lotName} · ${date ? fullDate(date) : ''}`}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        {error && <Banner tone="danger" title="Ca chưa được lưu">{error}</Banner>}
        <Field label="Nhân viên" required>
          <select className={inputClass} value={staffId} disabled={busy} onChange={(e) => setStaffId(Number(e.target.value))}>
            {staff.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
          </select>
        </Field>
        <Field label="Mẫu ca">
          <select className={inputClass} value={templateId} disabled={busy} onChange={(e) => pickTemplate(Number(e.target.value))}>
            <option value={0}>Không dùng mẫu</option>
            {templates.filter((t) => t.isActive || t.id === templateId).map((t) => (
              <option key={t.id} value={t.id}>{t.name} · {t.startTime.slice(0, 5)}–{t.endTime.slice(0, 5)}</option>
            ))}
          </select>
        </Field>
        <Field label="Ngày bắt đầu" required>
          <input type="date" className={inputClass} value={date} disabled={busy} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Bắt đầu" required>
            <input type="time" className={inputClass} value={start} disabled={busy} onChange={(e) => setStart(e.target.value)} />
          </Field>
          <Field label="Kết thúc" required hint={end && start && end <= start ? 'Giờ kết thúc thuộc ngày kế tiếp.' : undefined}>
            <input type="time" className={inputClass} value={end} disabled={busy} onChange={(e) => setEnd(e.target.value)} />
          </Field>
        </div>
        <label className="flex items-start gap-2.5 text-[13px] text-[#10172a]">
          <input type="checkbox" className="mt-0.5 size-4 accent-[#2c5cf2]" checked={cashHolder} disabled={busy} onChange={(e) => setCashHolder(e.target.checked)} />
          <span><span className="font-semibold">Giữ két</span><span className="block text-xs text-[#65738c]">Chỉ người giữ két được thu tiền mặt. Mỗi thời điểm chỉ một người.</span></span>
        </label>
        <Banner tone="info" title="Kiểm tra trước khi lưu">Chặn trùng giờ của cùng nhân viên, kể cả ca tại bãi khác. Thiếu người chỉ là cảnh báo.</Banner>
        <ModalActions>
          <Button variant="secondary" disabled={busy} onClick={onClose}>Hủy</Button>
          <Button type="submit" busy={busy}>Lưu ca</Button>
          {shift && <Button variant="danger" className="ml-auto" disabled={busy} onClick={() => { setConfirmDelete(true); setError(''); }}>Xóa ca</Button>}
        </ModalActions>
      </form>
    </Modal>
  );
}
