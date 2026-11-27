import React, { useState } from 'react';
import { AdminDialog } from './AdminDialog';
import { TextAreaField, TextField } from './FormFields';

interface LifecycleDialogProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel: string;
  cancelLabel?: string;
  destructive?: boolean;
  /** Nội dung đặt trước các ô nhập (Summary, Banner…). */
  children?: React.ReactNode;
  reasonLabel?: string;
  reasonPlaceholder?: string;
  reasonHelper?: string;
  /** Gõ lại giá trị (mã hồ sơ / email) mới cho xác nhận; so sánh không phân biệt hoa thường. */
  typeToConfirm?: { label: string; expected: string; helper?: string };
  /** Ô nhập thêm (ví dụ "Until") đặt sau lý do; extraError chặn gửi và hiện ở ô đó. */
  extraFields?: React.ReactNode;
  extraError?: string | null;
  /** Danh sách lý do bị chặn (409 có cấu trúc) hiện trên footer cùng thông báo lỗi nguyên văn. */
  onClose: () => void;
  onSubmit: (reason: string) => Promise<void>;
}

/**
 * Dialog thao tác vòng đời (Discard application, Suspend, Reactivate, Delete user): lý do bắt buộc, tùy chọn gõ lại mã / email để xác nhận,
 * lỗi 400 / 409 của API hiện NGUYÊN VĂN trên footer. Dựng trên AdminDialog nên có focus trap, Escape và nút nguy hiểm màu danger.
 */
export const LifecycleDialog: React.FC<LifecycleDialogProps> = ({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel,
  destructive,
  children,
  reasonLabel = 'Reason',
  reasonPlaceholder,
  reasonHelper,
  typeToConfirm,
  extraFields,
  extraError,
  onClose,
  onSubmit,
}) => {
  const [reason, setReason] = useState('');
  const [typed, setTyped] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const reasonError = reason.trim() === '' ? 'A reason is required.' : null;
  const typedError =
    typeToConfirm && typed.trim().toLowerCase() !== typeToConfirm.expected.trim().toLowerCase() ? `Type ${typeToConfirm.expected} exactly to confirm.` : null;

  const reset = () => {
    setReason('');
    setTyped('');
    setSubmitted(false);
    setApiError(null);
  };
  const close = () => {
    reset();
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    if (reasonError || typedError || extraError) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await onSubmit(reason.trim());
      reset();
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
      title={title}
      description={description}
      onClose={close}
      onConfirm={confirm}
      confirmLabel={confirmLabel}
      cancelLabel={cancelLabel}
      destructive={destructive}
      submitting={submitting}
      error={apiError}
    >
      {children}
      <TextAreaField
        label={reasonLabel}
        required
        value={reason}
        onChange={setReason}
        maxLength={1000}
        placeholder={reasonPlaceholder}
        helper={reasonHelper}
        error={submitted ? reasonError : null}
      />
      {extraFields}
      {typeToConfirm && (
        <TextField
          label={typeToConfirm.label}
          required
          value={typed}
          onChange={setTyped}
          placeholder={typeToConfirm.expected}
          helper={typeToConfirm.helper}
          error={submitted ? typedError : null}
        />
      )}
    </AdminDialog>
  );
};
