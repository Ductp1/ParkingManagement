import React, { useState } from 'react';
import { AdminDialog } from './AdminDialog';
import { TextAreaField } from './FormFields';

export interface ActionDialogField {
  label: string;
  required?: boolean;
  requiredMessage?: string;
  placeholder?: string;
  helper?: string;
  maxLength?: number;
}

interface ActionDialogProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel: string;
  cancelLabel?: string;
  destructive?: boolean;
  /** Nội dung đặt trước ô nhập (Summary, Details, Banner…). */
  children?: React.ReactNode;
  /** Ô lý do/ghi chú; bỏ trống = dialog xác nhận không có ô nhập. */
  field?: ActionDialogField;
  onClose: () => void;
  onSubmit: (text: string) => Promise<void>;
}

/**
 * Dialog thao tác chuyển trạng thái (Start review, Approve, Reject, Request info, Unlock, Cancel change…):
 * kiểm tra bắt buộc phía client theo frame "– validation", hiển thị lỗi 400/409 từ API ngay trên footer.
 */
export const ActionDialog: React.FC<ActionDialogProps> = ({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel,
  destructive,
  children,
  field,
  onClose,
  onSubmit,
}) => {
  const [text, setText] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const fieldError =
    field?.required && text.trim() === '' ? field.requiredMessage ?? `${field.label} is required.` : null;

  const close = () => {
    setText('');
    setSubmitted(false);
    setApiError(null);
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    if (fieldError) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await onSubmit(text.trim());
      setText('');
      setSubmitted(false);
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
      {field && (
        <TextAreaField
          label={field.label}
          required={field.required}
          value={text}
          onChange={setText}
          maxLength={field.maxLength ?? 1000}
          placeholder={field.placeholder}
          helper={field.helper}
          error={submitted ? fieldError : null}
        />
      )}
    </AdminDialog>
  );
};
