import React, { useState } from 'react';
import { adminCommunicationsApi } from '@/api/adminCommunicationsApi';
import { NotificationChannel } from '@/types/adminEnums';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { Banner } from '../../components/Banner';
import { DetailColumns } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { DetailList, DetailRow } from '../../components/DetailRow';
import { AdminButton } from '../../components/AdminButton';
import { AdminDialog } from '../../components/AdminDialog';
import { SelectField, TextAreaField, TextField } from '../../components/FormFields';
import { useAdminToast } from '../../hooks/useAdminToast';
import { humanizeEnum } from '../../utils/enumLabels';

interface ComposeForm {
  userId: string;
  channel: string;
  templateKey: string;
  title: string;
  body: string;
}

const EMPTY: ComposeForm = { userId: '', channel: NotificationChannel.InApp, templateKey: '', title: '', body: '' };

const validate = (f: ComposeForm): Partial<Record<keyof ComposeForm, string>> => {
  const e: Partial<Record<keyof ComposeForm, string>> = {};
  if (!/^\d+$/.test(f.userId) || Number(f.userId) <= 0) e.userId = 'Enter a valid user ID.';
  if (!f.channel) e.channel = 'Choose a channel.';
  if (!/^[A-Z0-9_]{3,100}$/.test(f.templateKey)) e.templateKey = 'Use UPPER_SNAKE_CASE, 3–100 characters.';
  if (f.title.trim() === '') e.title = 'A title is required.';
  if (f.body.trim() === '') e.body = 'A message is required.';
  return e;
};

/**
 * Communications / Send notification – POST /api/v1/notifications/send (NotificationService).
 * Chưa có API danh sách NotificationTemplates nên Template key nhập tay; nội dung gửi đúng như preview.
 */
export const SendNotificationPage: React.FC = () => {
  const toast = useAdminToast();
  const [form, setForm] = useState<ComposeForm>(EMPTY);
  const [submitted, setSubmitted] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [sending, setSending] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const errors = validate(form);
  const err = (k: keyof ComposeForm) => (submitted ? errors[k] ?? null : null);
  const set = (k: keyof ComposeForm) => (v: string) => setForm((f) => ({ ...f, [k]: v }));

  const send = async () => {
    setSending(true);
    setApiError(null);
    try {
      const res = await adminCommunicationsApi.send({
        userId: Number(form.userId),
        channel: form.channel as NotificationChannel,
        templateKey: form.templateKey,
        title: form.title.trim(),
        body: form.body.trim(),
      });
      toast.show({ type: 'success', title: 'Notification sent', message: `#${res.notificationId} · ${humanizeEnum(res.status)} · ${res.message}` });
      setConfirmOpen(false);
      setForm(EMPTY);
      setSubmitted(false);
    } catch (e: unknown) {
      setApiError(e instanceof Error ? e.message : String(e));
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      <AdminTitleBar title="Send notification" subtitle="Template-based message to one user" />
      <Banner tone="info">Notifications are stored per user in Notifications and delivered on the chosen channel (In-app is pushed in real time).</Banner>
      <DetailColumns
        main={
          <Panel title="Compose">
            <div className="flex flex-col gap-4">
              <div className="grid grid-cols-2 gap-3">
                <TextField label="Recipient user ID" required type="number" value={form.userId} onChange={set('userId')} helper="Notifications.UserId" error={err('userId')} />
                <SelectField
                  label="Channel"
                  required
                  value={form.channel}
                  onChange={set('channel')}
                  options={Object.values(NotificationChannel).map((c) => ({ value: c, label: humanizeEnum(c) }))}
                  error={err('channel')}
                />
              </div>
              <TextField label="Template key" required value={form.templateKey} onChange={(v) => set('templateKey')(v.toUpperCase())} helper="NotificationTemplates.Key (no template list API yet)" error={err('templateKey')} />
              <TextField label="Title" required value={form.title} onChange={set('title')} maxLength={200} error={err('title')} />
              <TextAreaField label="Message" required value={form.body} onChange={set('body')} maxLength={2000} error={err('body')} />
              <div className="flex justify-end">
                <AdminButton
                  icon="send"
                  onClick={() => {
                    setSubmitted(true);
                    if (Object.keys(errors).length === 0) setConfirmOpen(true);
                  }}
                >
                  Send
                </AdminButton>
              </div>
            </div>
          </Panel>
        }
        side={
          <>
            <Panel title="Preview" description="Title / body as the user will see it">
              <div className="flex flex-col gap-1 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-selected) p-3">
                <span className="adm-type-body-strong text-(--admin-text-primary)">{form.title || '—'}</span>
                <span className="adm-type-body-regular-12 whitespace-pre-wrap text-(--admin-text-secondary)">{form.body || '—'}</span>
                <span className="adm-type-body-small text-(--admin-text-placeholder)">{humanizeEnum(form.channel)} · now</span>
              </div>
            </Panel>
            <Panel title="Channel rules">
              <DetailList>
                <DetailRow label="Channel">{humanizeEnum(form.channel)}</DetailRow>
                <DetailRow label="User preference">NotificationPreferences (checked by NotificationService)</DetailRow>
              </DetailList>
            </Panel>
          </>
        }
      />
      <AdminDialog
        open={confirmOpen}
        title="Send notification?"
        description="Creates one Notifications row (Status Pending → Sent)."
        onClose={() => setConfirmOpen(false)}
        onConfirm={send}
        confirmLabel="Send now"
        submitting={sending}
        error={apiError}
      >
        <div className="rounded-[2px] border border-(--admin-border-default)">
          <DetailRow label="Template">
            {form.templateKey} · {humanizeEnum(form.channel)}
          </DetailRow>
          <DetailRow label="Recipient">User #{form.userId}</DetailRow>
          <DetailRow label="Title">{form.title}</DetailRow>
        </div>
      </AdminDialog>
    </>
  );
};
