import React, { useState } from 'react';
import { adminUsersApi } from '@/api/adminUsersApi';
import type { AdminUserDto, SanctionDto, UserLifecycleResultDto } from '@/types/adminUsers';
import { ActionDialog } from '../../../components/ActionDialog';
import { LifecycleDialog } from '../../../components/LifecycleDialog';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DateTimeField, TextAreaField } from '../../../components/FormFields';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDate, localInputToUtcIso } from '../../../utils/format';

const Summary: React.FC<{ user: AdminUserDto }> = ({ user }) => (
  <DialogSummary
    title={`${user.fullName}${user.ownerProfile ? ` · Owner #${user.ownerProfile.id}` : ''}`}
    subtitle={user.ownerProfile?.businessName}
  />
);

interface DialogProps {
  open: boolean;
  user: AdminUserDto;
  onClose: () => void;
}

/** Dialog – Lock owner (US-096): lý do bắt buộc, "Locked until" phải sau hiện tại; trống = Permanent ban. */
export const LockOwnerDialog: React.FC<DialogProps & { onDone: () => void }> = ({ open, user, onClose, onDone }) => {
  const [reason, setReason] = useState('');
  const [until, setUntil] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [untilError, setUntilError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const reasonError = reason.trim() === '' ? 'A reason is required.' : null;

  const close = () => {
    setReason('');
    setUntil('');
    setSubmitted(false);
    setUntilError(null);
    setApiError(null);
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    const untilIso = localInputToUtcIso(until);
    const pastError = untilIso && new Date(untilIso).getTime() <= Date.now() ? 'Must be after the current time.' : null;
    setUntilError(pastError);
    if (reasonError || pastError || !user.ownerProfile) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await adminUsersApi.lockOwner(user.ownerProfile.id, { ownerUserId: user.id, reason: reason.trim(), lockedUntilUtc: untilIso });
      onDone();
      close();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      title="Lock owner"
      description="Records a sanction, locks the account, signs the user out everywhere and notifies the owner."
      onClose={close}
      onConfirm={confirm}
      confirmLabel="Lock owner"
      destructive
      submitting={submitting}
      error={apiError}
    >
      <Summary user={user} />
      <TextAreaField
        label="Reason"
        required
        value={reason}
        onChange={setReason}
        maxLength={1000}
        placeholder="Why is this owner locked (SLA violation, fraud, fake licence…)?"
        error={submitted ? reasonError : null}
      />
      <DateTimeField
        label="Locked until"
        value={until}
        onChange={(v) => {
          setUntil(v);
          setUntilError(null);
        }}
        helper="Empty = no end date → Permanent ban; with a date → Temporary suspension."
        error={untilError}
      />
      <p className="adm-type-body-regular-12 m-0 text-(--admin-text-secondary)">
        The reason is saved to the sanction history and audit log and cannot be edited.
      </p>
    </AdminDialog>
  );
};

/** Dialog – Unlock owner (US-096): thu hồi lock cấp chủ bãi mới nhất, lý do lưu audit log. */
export const UnlockOwnerDialog: React.FC<DialogProps & { activeLock: SanctionDto | null; onDone: () => void }> = ({
  open,
  user,
  activeLock,
  onClose,
  onDone,
}) => (
  <ActionDialog
    open={open}
    title="Unlock owner"
    description="Revokes the latest owner-level lock and unlocks the account. Lot-level sanctions are not touched."
    confirmLabel="Unlock owner"
    field={{
      label: 'Reason',
      required: true,
      requiredMessage: 'A reason is required.',
      placeholder: 'Why is this owner unlocked?',
      helper: 'Stored in the audit log only.',
      maxLength: 1000,
    }}
    onClose={onClose}
    onSubmit={async (reason) => {
      if (!user.ownerProfile) return;
      await adminUsersApi.unlockOwner(user.ownerProfile.id, { reason });
      onDone();
    }}
  >
    <Summary user={user} />
    {activeLock && (
      <Banner tone="warning">
        Active lock: Sanction #{activeLock.id} · {humanizeEnum(activeLock.level)} since {formatDate(activeLock.startsAtUtc)} · “{activeLock.reason}”.
      </Banner>
    )}
  </ActionDialog>
);

// ===== Đình chỉ / mở lại / xóa user (US-096) – áp dụng cho mọi vai trò trừ chủ bãi (chủ bãi dùng Lock owner) =====

/** Suspend: lý do bắt buộc; "Until" trống = vô thời hạn. Phiên hiện tại bị vô hiệu, đăng nhập bị từ chối với thông báo rõ. */
export const SuspendUserDialog: React.FC<DialogProps & { onDone: (result: UserLifecycleResultDto) => void }> = ({ open, user, onClose, onDone }) => {
  const [until, setUntil] = useState('');
  const [openedAt] = useState(() => Date.now());   // render phải thuần; server kiểm tra lại thời hạn
  const untilIso = localInputToUtcIso(until);
  const untilError = untilIso && new Date(untilIso).getTime() <= openedAt ? 'Must be after the current time.' : null;
  return (
    <LifecycleDialog
      open={open}
      title="Suspend user"
      description="Signs the user out everywhere and blocks sign-in until the account is reactivated."
      confirmLabel="Suspend user"
      destructive
      reasonPlaceholder="Why is this user suspended?"
      reasonHelper="Saved to the audit log and shown to other admins."
      extraFields={
        <DateTimeField label="Suspended until" value={until} onChange={setUntil} helper="Empty = indefinitely. With a date, the account reopens by itself." error={untilError} />
      }
      extraError={untilError}
      onClose={() => {
        setUntil('');
        onClose();
      }}
      onSubmit={async (reason) => {
        const result = await adminUsersApi.suspendUser(user.id, { reason, lockedUntilUtc: untilIso, rowVersion: user.rowVersion ?? null });
        onDone(result);
      }}
    >
      <Summary user={user} />
    </LifecycleDialog>
  );
};

export const ReactivateUserDialog: React.FC<DialogProps & { onDone: (result: UserLifecycleResultDto) => void }> = ({ open, user, onClose, onDone }) => (
  <LifecycleDialog
    open={open}
    title="Reactivate user"
    description="The user can sign in again."
    confirmLabel="Reactivate user"
    reasonPlaceholder="Why is this user reactivated?"
    onClose={onClose}
    onSubmit={async (reason) => onDone(await adminUsersApi.reactivateUser(user.id, { reason, rowVersion: user.rowVersion ?? null }))}
  >
    <Summary user={user} />
    {user.lockReason && <Banner tone="warning">Suspended because: “{user.lockReason}”.</Banner>}
  </LifecycleDialog>
);

/** Delete user: ẩn danh hóa (giữ Id cho lịch sử), không hoàn tác. Gõ lại email để xác nhận; 409 liệt kê nghĩa vụ còn lại nguyên văn. */
export const DeleteUserDialog: React.FC<DialogProps & { onDone: (result: UserLifecycleResultDto) => void }> = ({ open, user, onClose, onDone }) => (
  <LifecycleDialog
    open={open}
    title="Delete user"
    description="Anonymizes the account: name, email, phone and KYC are removed and the email can be registered again. This cannot be undone."
    confirmLabel="Delete user"
    destructive
    reasonPlaceholder="Why is this user deleted?"
    typeToConfirm={{ label: 'Type the user’s email to confirm', expected: user.email ?? '' }}
    onClose={onClose}
    onSubmit={async (reason) =>
      onDone(await adminUsersApi.deleteUser(user.id, { reason, confirmEmail: user.email ?? '', rowVersion: user.rowVersion ?? null }))
    }
  >
    <Summary user={user} />
    <Banner tone="warning">
      Refused while the user still has open bookings, signed lot contracts, unpaid settlements or active sanctions. Suspend the user instead.
    </Banner>
  </LifecycleDialog>
);
