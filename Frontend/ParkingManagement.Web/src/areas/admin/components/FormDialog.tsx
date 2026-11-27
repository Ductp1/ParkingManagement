import React, { useState } from 'react';
import { AdminDialog } from './AdminDialog';
import { DateTimeField, SelectField, TextAreaField, TextField, ToggleField, UploadZone, type SelectOption } from './FormFields';

type FieldValue = string | boolean | File | null;
export type FormValues = Record<string, FieldValue>;

export interface FormFieldSpec {
  key: string;
  kind: 'text' | 'number' | 'textarea' | 'select' | 'datetime' | 'date' | 'toggle' | 'upload';
  label: string;
  required?: boolean;
  helper?: string;
  placeholder?: string;
  maxLength?: number;
  suffix?: string;
  options?: SelectOption[];
  /** Kiểm tra phía client theo Figma "– validation"; trả message lỗi hoặc null. */
  validate?: (value: FieldValue, values: FormValues) => string | null;
  /** Hai field cạnh nhau trên cùng hàng. */
  half?: boolean;
  /** Message khi bỏ trống field bắt buộc (Figma, ví dụ "A reason is required."). */
  requiredMessage?: string;
}

export interface FormDialogSpec {
  title: string;
  description?: string;
  size?: 520 | 640;
  confirmLabel: string;
  destructive?: boolean;
  fields: FormFieldSpec[];
}

interface FormDialogProps {
  spec: FormDialogSpec;
  open: boolean;
  onClose: () => void;
  /** Không truyền = chưa có endpoint: form vẫn kiểm tra dữ liệu nhưng nút gửi bị khóa. */
  onSubmit?: (values: FormValues) => Promise<void>;
  unavailableNote?: string;
}

const initialValue = (f: FormFieldSpec): FieldValue => (f.kind === 'toggle' ? false : f.kind === 'upload' ? null : '');

const isBlank = (v: FieldValue) => v === null || v === '' || (typeof v === 'string' && v.trim() === '');

const validateFields = (fields: FormFieldSpec[], values: FormValues): Record<string, string> => {
  const errors: Record<string, string> = {};
  fields.forEach((f) => {
    const v = values[f.key];
    if (f.required && f.kind !== 'toggle' && isBlank(v)) {
      errors[f.key] = f.requiredMessage ?? (f.kind === 'upload' ? 'A file is required.' : `${f.label.replace(/\s*\(.*\)$/, '')} is required.`);
      return;
    }
    const custom = f.validate?.(v, values);
    if (custom) errors[f.key] = custom;
  });
  return errors;
};

/** Dialog tạo/sửa dựng từ cấu hình field – dùng cho các màn mà backend chưa có endpoint ghi. */
export const FormDialog: React.FC<FormDialogProps> = ({ spec, open, onClose, onSubmit, unavailableNote }) => {
  const [values, setValues] = useState<FormValues>(() => Object.fromEntries(spec.fields.map((f) => [f.key, initialValue(f)])));
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [submitted, setSubmitted] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const errors = validateFields(spec.fields, values);
  const showError = (key: string) => (submitted || touched[key] ? errors[key] ?? null : null);

  const update = (key: string, value: FieldValue) => {
    setValues((prev) => ({ ...prev, [key]: value }));
    setTouched((prev) => ({ ...prev, [key]: true }));
  };

  const confirm = async () => {
    setSubmitted(true);
    if (Object.keys(errors).length > 0 || !onSubmit) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const renderField = (f: FormFieldSpec) => {
    const v = values[f.key];
    const common = { label: f.label, required: f.required, helper: f.helper, error: showError(f.key) };
    switch (f.kind) {
      case 'textarea':
        return <TextAreaField {...common} value={String(v ?? '')} maxLength={f.maxLength ?? 1000} placeholder={f.placeholder} onChange={(x) => update(f.key, x)} />;
      case 'select':
        return <SelectField {...common} value={String(v ?? '')} options={f.options ?? []} placeholder={f.placeholder} onChange={(x) => update(f.key, x)} />;
      case 'datetime':
      case 'date':
        return <DateTimeField {...common} mode={f.kind === 'date' ? 'date' : 'datetime-local'} value={String(v ?? '')} onChange={(x) => update(f.key, x)} />;
      case 'toggle':
        return <ToggleField label={f.label} checked={v === true} onChange={(x) => update(f.key, x)} />;
      case 'upload':
        return <UploadZone label={f.label} required={f.required} file={v instanceof File ? v : null} error={showError(f.key)} onChange={(x) => update(f.key, x)} />;
      default:
        return (
          <TextField
            {...common}
            type={f.kind === 'number' ? 'number' : 'text'}
            value={String(v ?? '')}
            placeholder={f.placeholder}
            maxLength={f.maxLength}
            suffix={f.suffix}
            onChange={(x) => update(f.key, x)}
          />
        );
    }
  };

  const rows: FormFieldSpec[][] = [];
  spec.fields.forEach((f) => {
    const last = rows[rows.length - 1];
    if (f.half && last && last.length === 1 && last[0].half) last.push(f);
    else rows.push([f]);
  });

  return (
    <AdminDialog
      open={open}
      title={spec.title}
      description={spec.description}
      size={spec.size}
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel={spec.confirmLabel}
      destructive={spec.destructive}
      confirmDisabled={!onSubmit}
      submitting={submitting}
      error={apiError}
      footerNote={onSubmit ? undefined : unavailableNote ?? 'No backend endpoint yet — changes cannot be saved.'}
    >
      {rows.map((row) => (
        <div key={row.map((f) => f.key).join('-')} className={row.length > 1 ? 'grid grid-cols-2 gap-3' : ''}>
          {row.map((f) => (
            <React.Fragment key={f.key}>{renderField(f)}</React.Fragment>
          ))}
        </div>
      ))}
    </AdminDialog>
  );
};
