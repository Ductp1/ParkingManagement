import { useState, type FormEvent } from 'react';
import { Pencil } from 'lucide-react';
import { shiftApi, type SaveTemplate, type ShiftTemplate } from '@/api/shiftApi';
import { errorText } from '@/features/shifts/format';
import { Banner, Button, Field, Modal, ModalActions, inputClass } from '@/features/shifts/ui';

interface Props {
  lotId: number;
  lotName: string;
  templates: ShiftTemplate[];
  onClose: () => void;
  onChanged: () => void;
  onUse: (templateId: number) => void;
}

const EMPTY: SaveTemplate = { name: '', startTime: '06:00', endTime: '14:00', isCashHolder: true, isActive: true };

export function ShiftTemplatesDrawer({ lotId, lotName, templates, onClose, onChanged, onUse }: Props) {
  const [editing, setEditing] = useState<number | null>(null);
  const [form, setForm] = useState<SaveTemplate>(EMPTY);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const set = (patch: Partial<SaveTemplate>) => setForm((f) => ({ ...f, ...patch }));

  function edit(t: ShiftTemplate | null) {
    setEditing(t?.id ?? null);
    setForm(t ? { name: t.name, startTime: t.startTime.slice(0, 5), endTime: t.endTime.slice(0, 5), isCashHolder: t.isCashHolder, isActive: t.isActive } : EMPTY);
    setConfirmDelete(false);
    setError('');
  }

  async function run(action: () => Promise<unknown>) {
    setBusy(true);
    setError('');
    try {
      await action();
      edit(null);
      onChanged();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    if (!form.name.trim() || !form.startTime || !form.endTime || form.startTime === form.endTime) {
      setError('Hãy nhập tên mẫu và giờ bắt đầu khác giờ kết thúc.');
      return;
    }
    void run(() => shiftApi.saveTemplate(lotId, editing, { ...form, name: form.name.trim(), startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00` }));
  }

  return (
    <Modal drawer title="Mẫu ca của bãi" subtitle={`${lotName} · Mỗi mẫu thuộc riêng bãi này.`} busy={busy} onClose={onClose}>
      {templates.length === 0 && <p className="text-[13px] text-[#65738c]">Bãi chưa có mẫu ca. Tạo mẫu đầu tiên bên dưới.</p>}
      {templates.map((t) => (
        <article key={t.id} className="rounded-[10px] border border-[#e3e8f0] p-4">
          <p className="text-sm font-semibold text-[#10172a]">{t.name}</p>
          <p className="mt-1 font-mono text-xs text-[#65738c]">
            {t.startTime.slice(0, 5)}–{t.endTime.slice(0, 5)}{t.endTime <= t.startTime ? ' (+1 ngày)' : ''}
            {t.isCashHolder ? ' · Giữ két' : ''}{t.isActive ? '' : ' · Tạm ẩn'}
          </p>
          <div className="mt-3 flex gap-2">
            <Button disabled={!t.isActive || busy} onClick={() => onUse(t.id)}>Sử dụng</Button>
            <Button variant="secondary" disabled={busy} onClick={() => edit(t)}><Pencil size={14} aria-hidden="true" />Sửa</Button>
          </div>
        </article>
      ))}
      <form onSubmit={submit} noValidate className="flex flex-col gap-3 border-t border-[#e3e8f0] pt-4">
        <p className="text-sm font-semibold text-[#10172a]">{editing === null ? 'Tạo mẫu ca' : 'Sửa mẫu ca'}</p>
        {error && <Banner tone="danger" title="Chưa lưu được mẫu ca">{error}</Banner>}
        <Field label="Tên mẫu" required>
          <input className={inputClass} value={form.name} maxLength={100} disabled={busy} placeholder="Ví dụ: Ca đêm" onChange={(e) => set({ name: e.target.value })} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Bắt đầu" required>
            <input type="time" className={inputClass} value={form.startTime} disabled={busy} onChange={(e) => set({ startTime: e.target.value })} />
          </Field>
          <Field label="Kết thúc" required>
            <input type="time" className={inputClass} value={form.endTime} disabled={busy} onChange={(e) => set({ endTime: e.target.value })} />
          </Field>
        </div>
        {form.endTime <= form.startTime && <p className="text-xs text-[#65738c]">Giờ kết thúc thuộc ngày kế tiếp.</p>}
        <label className="flex items-center gap-2.5 text-[13px] font-semibold text-[#10172a]">
          <input type="checkbox" className="size-4 accent-[#2c5cf2]" checked={form.isCashHolder} disabled={busy} onChange={(e) => set({ isCashHolder: e.target.checked })} />
          Giữ két
        </label>
        <label className="flex items-center gap-2.5 text-[13px] font-semibold text-[#10172a]">
          <input type="checkbox" className="size-4 accent-[#2c5cf2]" checked={form.isActive} disabled={busy} onChange={(e) => set({ isActive: e.target.checked })} />
          Đang dùng
        </label>
        <ModalActions>
          {editing !== null && (
            <>
              <Button variant="danger" disabled={busy}
                onClick={() => (confirmDelete ? void run(() => shiftApi.deleteTemplate(lotId, editing)) : setConfirmDelete(true))}>
                {confirmDelete ? 'Bấm lần nữa để xóa' : 'Xóa mẫu'}
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => edit(null)}>Hủy sửa</Button>
            </>
          )}
          <Button type="submit" busy={busy}>Lưu mẫu</Button>
        </ModalActions>
      </form>
    </Modal>
  );
}
