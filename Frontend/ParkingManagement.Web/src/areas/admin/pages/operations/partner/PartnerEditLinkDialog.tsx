import React, { useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import type { PartnerApplicationDetailDto, PartnerEditLinkDto } from '@/types/adminPartnerOnboarding';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { AdminButton } from '../../../components/AdminButton';
import { TextField } from '../../../components/FormFields';
import { formatDateTimeIct } from '../../../utils/format';
import { partnerEditUrl } from './partnerFormat';

interface PartnerEditLinkDialogProps {
  app: PartnerApplicationDetailDto;
  onClose: () => void;
}

/**
 * Edit link (US-116 T3): hồ sơ Needs more info → Admin tạo link bổ sung một lần (7 ngày) rồi gửi đối tác qua email / Zalo.
 * Token chỉ hiện ở dialog này (server không lưu bản rõ); tạo lại thì link cũ hết hiệu lực.
 */
export const PartnerEditLinkDialog: React.FC<PartnerEditLinkDialogProps> = ({ app, onClose }) => {
  const [link, setLink] = useState<PartnerEditLinkDto | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const url = link ? partnerEditUrl(window.location.origin, link) : '';

  const create = async () => {
    setSubmitting(true);
    setApiError(null);
    try {
      setLink(await adminPartnerOnboardingApi.issueEditLink(app.id));
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
    } catch {
      setApiError('Could not copy automatically. Select the link and copy it.');
    }
  };

  return (
    <AdminDialog
      open
      size={640}
      title="Link for the applicant to update"
      description={
        link
          ? `Send this link to ${app.email}. It works once and expires on ${formatDateTimeIct(link.expiresAtUtc)}.`
          : 'Creates a one-time link (valid 7 days) where the applicant fixes the application. Any earlier link stops working.'
      }
      confirmLabel={link ? 'Done' : 'Create link'}
      onConfirm={link ? onClose : create}
      onClose={onClose}
      submitting={submitting}
      error={apiError}
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`Requested: ${app.infoRequestNote ?? '—'}`} />
      {link && (
        <div className="flex items-end gap-2">
          <div className="min-w-0 flex-1">
            <TextField label="Update link" value={url} readOnly helper="Shown only now – the server keeps a hash, not the link." />
          </div>
          <div className="pb-6">
            <AdminButton variant="secondary" icon={copied ? 'check' : undefined} onClick={copy}>
              {copied ? 'Copied' : 'Copy'}
            </AdminButton>
          </div>
        </div>
      )}
    </AdminDialog>
  );
};
