import React, { useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PARTNER_INFO_ITEMS, type PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { DateTimeField, TextAreaField } from '../../../components/FormFields';
import { localInputToUtcIso } from '../../../utils/format';

const GROUPS: Array<{ group: 'field' | 'document'; title: string }> = [
  { group: 'field', title: 'Information' },
  { group: 'document', title: 'Documents' },
];

/**
 * Request more information (US-116 T3): ghi chú bắt buộc, chọn các mục / giấy tờ cần bổ sung, hạn bổ sung (mặc định +7 ngày ICT).
 * Gửi xong, ParkMaster email link bổ sung cho người nộp; trang bổ sung chỉ mở đúng các mục được chọn.
 */
export const RequestInfoDialog: React.FC<{
  open: boolean;
  app: PartnerApplicationDetailDto;
  onClose: () => void;
  onDone: () => void;
}> = ({ open, app, onClose, onDone }) => {
  const [note, setNote] = useState('');
  const [items, setItems] = useState<string[]>([]);
  const [due, setDue] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  // Mốc "hiện tại" chốt khi mở hộp thoại (render phải thuần); server vẫn kiểm tra lại hạn bổ sung.
  const [openedAt] = useState(() => Date.now());

  const noteError = note.trim() === '' ? 'A note is required.' : null;
  const dueIso = localInputToUtcIso(due);
  const dueError = dueIso && new Date(dueIso).getTime() <= openedAt ? 'Must be after the current time.' : null;

  const reset = () => {
    setNote('');
    setItems([]);
    setDue('');
    setSubmitted(false);
    setApiError(null);
  };
  const close = () => {
    reset();
    onClose();
  };
  const toggle = (key: string) => setItems((prev) => (prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]));

  const confirm = async () => {
    setSubmitted(true);
    if (noteError || dueError) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await adminPartnerOnboardingApi.requestInfo(app.id, { note: note.trim(), items, dueAtUtc: dueIso, rowVersion: app.rowVersion ?? null });
      reset();
      onDone();
      onClose();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      title="Request more information"
      description={`Status changes to Needs more info and ${app.email} receives an email with a link to update the application.`}
      onClose={close}
      onConfirm={confirm}
      confirmLabel="Send request"
      submitting={submitting}
      error={apiError}
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`MST ${app.taxCode} · ${app.fullName} · ${app.email}`} />
      <TextAreaField
        label="Note to applicant"
        required
        value={note}
        onChange={setNote}
        maxLength={1000}
        placeholder="What should the applicant fix or add?"
        error={submitted ? noteError : null}
      />
      {GROUPS.map(({ group, title }) => (
        <fieldset key={group} className="m-0 flex flex-col gap-1 border-0 p-0">
          <legend className="adm-type-body-strong mb-1 p-0 text-(--admin-text-primary)">{title} to update</legend>
          <div className="grid grid-cols-2 gap-x-4 gap-y-1">
            {PARTNER_INFO_ITEMS.filter((i) => i.group === group).map((item) => (
              <label key={item.key} className="adm-type-body-default flex cursor-pointer items-center gap-2 text-(--admin-text-primary)">
                <input
                  type="checkbox"
                  checked={items.includes(item.key)}
                  onChange={() => toggle(item.key)}
                  className="size-4 accent-(--admin-brand-navy-900)"
                />
                {item.label}
              </label>
            ))}
          </div>
        </fieldset>
      ))}
      <p className="adm-type-body-regular-12 m-0 text-(--admin-text-secondary)">
        Nothing selected = the applicant may edit every item. Email stays read-only.
      </p>
      <DateTimeField
        label="Respond by"
        value={due}
        onChange={setDue}
        helper="Empty = 7 days from now (23:59 ICT). The link stops working after this time."
        error={submitted ? dueError : null}
      />
    </AdminDialog>
  );
};
