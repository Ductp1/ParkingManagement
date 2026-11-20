import React from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PartnerApplicationStatus as S } from '@/types/adminEnums';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { LifecycleDialog } from '../../../components/LifecycleDialog';

/**
 * Discard application (US-116): hủy / bỏ hồ sơ ở mọi trạng thái chưa kết thúc. Hồ sơ đã cấp tài khoản (chủ bãi chưa kích hoạt):
 * phải gõ lại mã hồ sơ; tài khoản đó bị gỡ để email dùng lại được. Chủ bãi đã kích hoạt → API trả 409 (hiện nguyên văn).
 */
export const DiscardApplicationDialog: React.FC<{
  open: boolean;
  app: PartnerApplicationDetailDto;
  onClose: () => void;
  onDone: () => void;
}> = ({ open, app, onClose, onDone }) => {
  const issued = app.status === S.AccountIssued;
  return (
    <LifecycleDialog
      open={open}
      title="Discard application"
      description={
        issued
          ? 'The application is closed as Cancelled and the issued owner account is removed.'
          : 'The application is closed as Cancelled. The applicant can submit a new one.'
      }
      confirmLabel="Discard application"
      cancelLabel="Keep application"
      destructive
      reasonPlaceholder="Why is this application discarded?"
      reasonHelper="Saved to the application and the audit log."
      typeToConfirm={issued ? { label: 'Type the application code to confirm', expected: app.code } : undefined}
      onClose={onClose}
      onSubmit={async (reason) => {
        await adminPartnerOnboardingApi.cancel(app.id, { reason, rowVersion: app.rowVersion ?? null, confirmCode: issued ? app.code : null });
        onDone();
      }}
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`MST ${app.taxCode} · ${app.fullName} · ${app.email}`} />
      {issued && (
        <Banner tone="warning">
          The issued owner account (never activated) will be removed and the email can be used again. If the owner has already activated it, this
          is refused — use Suspend or Delete user instead.
        </Banner>
      )}
    </LifecycleDialog>
  );
};
