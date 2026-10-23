import React, { useId, useRef, useState } from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';
import { FutureTag } from './FutureTag';
import { formatNumber } from '../utils/format';

interface FieldShellProps {
  label: string;
  htmlFor: string;
  required?: boolean;
  future?: boolean;
  counter?: string;
  helper?: string;
  error?: string | null;
  children: React.ReactNode;
}

/** Khung chung của Input/Select/Textarea/Date (Figma): nhãn body/strong (+ * + Future), hộp 32px, message body/small. */
const FieldShell: React.FC<FieldShellProps> = ({ label, htmlFor, required, future, counter, helper, error, children }) => (
  <div className="flex flex-col gap-1">
    <div className="flex items-center gap-1">
      <label htmlFor={htmlFor} className="adm-type-body-strong text-(--admin-text-primary)">
        {label}
      </label>
      {required && <span className="adm-type-body-strong text-(--admin-status-danger)">*</span>}
      {future && <FutureTag />}
      {counter && <span className="adm-type-body-small ml-auto text-(--admin-text-secondary)">{counter}</span>}
    </div>
    {children}
    {error ? (
      <span id={`${htmlFor}-msg`} className="adm-type-body-small text-(--admin-status-danger)">
        {error}
      </span>
    ) : (
      helper && (
        <span id={`${htmlFor}-msg`} className="adm-type-body-small text-(--admin-text-secondary)">
          {helper}
        </span>
      )
    )}
  </div>
);

const boxClass = (error?: string | null, readOnly?: boolean) =>
  `rounded-[2px] border px-2.5 outline-none adm-type-body-default text-(--admin-text-primary) placeholder:text-(--admin-text-placeholder) disabled:cursor-not-allowed disabled:bg-(--admin-surface-page) ${
    error
      ? 'border-(--admin-status-danger) bg-(--admin-surface-danger)'
      : readOnly
        ? 'border-(--admin-border-default) bg-(--admin-surface-page)'
        : 'border-(--admin-border-default) bg-(--admin-surface-card) focus:border-(--admin-brand-navy-900)'
  }`;

interface TextFieldProps {
  label: string;
  value: string;
  onChange?: (value: string) => void;
  required?: boolean;
  placeholder?: string;
  helper?: string;
  error?: string | null;
  type?: 'text' | 'number' | 'email';
  readOnly?: boolean;
  disabled?: boolean;
  maxLength?: number;
  future?: boolean;
  suffix?: string;
}

/** Shell/Input – Default / Focus / Error / Disabled / Read-only. */
export const TextField: React.FC<TextFieldProps> = ({ label, value, onChange, required, placeholder, helper, error, type = 'text', readOnly, disabled, maxLength, future, suffix }) => {
  const id = useId();
  return (
    <FieldShell label={label} htmlFor={id} required={required} helper={helper} error={error} future={future}>
      <div className="relative flex">
        <input
          id={id}
          type={type}
          value={value}
          readOnly={readOnly}
          disabled={disabled}
          maxLength={maxLength}
          placeholder={placeholder}
          aria-invalid={!!error}
          aria-describedby={`${id}-msg`}
          onChange={(e) => onChange?.(e.target.value)}
          className={`h-8 w-full ${suffix ? 'pr-8' : ''} ${boxClass(error, readOnly)}`}
        />
        {suffix && <span className="adm-type-body-default absolute top-1.5 right-2.5 text-(--admin-text-secondary)">{suffix}</span>}
      </div>
    </FieldShell>
  );
};

interface TextAreaFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  maxLength: number;
  required?: boolean;
  placeholder?: string;
  helper?: string;
  error?: string | null;
  rows?: number;
}

/** Shell/Textarea – hộp 88px, bộ đếm "0/1,000" bên phải nhãn. */
export const TextAreaField: React.FC<TextAreaFieldProps> = ({ label, value, onChange, maxLength, required, placeholder, helper, error, rows = 4 }) => {
  const id = useId();
  return (
    <FieldShell
      label={label}
      htmlFor={id}
      required={required}
      helper={helper}
      error={error}
      counter={`${value.length}/${maxLength.toLocaleString('en-US')}`}
    >
      <textarea
        id={id}
        value={value}
        rows={rows}
        maxLength={maxLength}
        placeholder={placeholder}
        aria-invalid={!!error}
        aria-describedby={`${id}-msg`}
        onChange={(e) => onChange(e.target.value)}
        className={`min-h-[88px] w-full resize-y py-2 ${boxClass(error)}`}
      />
    </FieldShell>
  );
};

export interface SelectOption {
  value: string;
  label: string;
}

interface SelectFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: SelectOption[];
  required?: boolean;
  placeholder?: string;
  helper?: string;
  error?: string | null;
  disabled?: boolean;
}

/** Shell/Select – native select trong hộp 32px + chevron. */
export const SelectField: React.FC<SelectFieldProps> = ({ label, value, onChange, options, required, placeholder = 'Select…', helper, error, disabled }) => {
  const id = useId();
  return (
    <FieldShell label={label} htmlFor={id} required={required} helper={helper} error={error}>
      <div className="relative flex">
        <select
          id={id}
          value={value}
          disabled={disabled}
          aria-invalid={!!error}
          aria-describedby={`${id}-msg`}
          onChange={(e) => onChange(e.target.value)}
          className={`h-8 w-full appearance-none pr-8 ${boxClass(error)}`}
        >
          <option value="">{placeholder}</option>
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <span className="pointer-events-none absolute top-2 right-2.5 text-(--admin-text-secondary)">
          <AdminIcon icon={ADMIN_ICONS.chevronDown} />
        </span>
      </div>
    </FieldShell>
  );
};

interface DateTimeFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  helper?: string;
  error?: string | null;
  /** 'datetime-local' cho ngày giờ (DD/MM/YYYY HH:mm), 'date' cho ngày. */
  mode?: 'datetime-local' | 'date';
}

/** Shell/Date – input ngày giờ gốc của trình duyệt trong hộp 32px. */
export const DateTimeField: React.FC<DateTimeFieldProps> = ({ label, value, onChange, required, helper, error, mode = 'datetime-local' }) => {
  const id = useId();
  return (
    <FieldShell label={label} htmlFor={id} required={required} helper={helper} error={error}>
      <input
        id={id}
        type={mode}
        value={value}
        aria-invalid={!!error}
        aria-describedby={`${id}-msg`}
        onChange={(e) => onChange(e.target.value)}
        className={`h-8 w-full ${boxClass(error)}`}
      />
    </FieldShell>
  );
};

interface ToggleFieldProps {
  label: string;
  checked: boolean;
  onChange?: (checked: boolean) => void;
  disabled?: boolean;
}

/** Shell/Toggle: track 32×18, bật = status/success, tắt = border/default. */
export const ToggleField: React.FC<ToggleFieldProps> = ({ label, checked, onChange, disabled }) => (
  <label className={`inline-flex items-center gap-2 ${disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}>
    <input
      type="checkbox"
      role="switch"
      className="peer sr-only"
      checked={checked}
      disabled={disabled}
      onChange={(e) => onChange?.(e.target.checked)}
    />
    <span
      aria-hidden="true"
      className={`flex h-[18px] w-8 items-center rounded-full p-0.5 ${checked ? 'justify-end bg-(--admin-status-success)' : 'justify-start bg-(--admin-border-default)'}`}
    >
      <span className="size-3.5 rounded-full bg-(--admin-surface-card)" />
    </span>
    <span className="adm-type-body-default text-(--admin-text-primary)">{label}</span>
  </label>
);

interface RadioFieldProps {
  name: string;
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
}

/** Shell/Radio: vòng 16px, chọn = viền navy-900 5px. */
export const RadioField: React.FC<RadioFieldProps> = ({ name, options, value, onChange }) => (
  <div className="flex flex-col gap-2">
    {options.map((o) => (
      <label key={o.value} className="inline-flex cursor-pointer items-center gap-2">
        <input type="radio" name={name} value={o.value} checked={value === o.value} onChange={() => onChange(o.value)} className="sr-only" />
        <span
          aria-hidden="true"
          className={`size-4 rounded-full bg-(--admin-surface-card) ${value === o.value ? 'border-[5px] border-(--admin-brand-navy-900)' : 'border border-(--admin-border-default)'}`}
        />
        <span className="adm-type-body-default text-(--admin-text-primary)">{o.label}</span>
      </label>
    ))}
  </div>
);

interface UploadZoneProps {
  label: string;
  required?: boolean;
  accept?: string;
  maxBytes?: number;
  file: File | null;
  onChange: (file: File | null) => void;
  error?: string | null;
}

/** Shell/Upload zone – Empty / Error / Uploaded (chỉ chọn file ở client; chưa có API lưu file). */
export const UploadZone: React.FC<UploadZoneProps> = ({ label, required, accept = '.pdf,.jpg,.jpeg,.png', maxBytes = 10 * 1024 * 1024, file, onChange, error }) => {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [sizeError, setSizeError] = useState<string | null>(null);
  const message = error ?? sizeError;

  const pick = (f: File | null) => {
    if (f && f.size > maxBytes) {
      setSizeError('File is larger than 10 MB.');
      onChange(null);
      return;
    }
    setSizeError(null);
    onChange(f);
  };

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-1">
        <label htmlFor={id} className="adm-type-body-strong text-(--admin-text-primary)">
          {label}
        </label>
        {required && <span className="adm-type-body-strong text-(--admin-status-danger)">*</span>}
      </div>
      <div
        className={`flex h-14 items-center gap-2 rounded-[2px] border px-3 text-(--admin-text-secondary) ${
          message ? 'border-(--admin-status-danger) bg-(--admin-surface-danger)' : 'border-(--admin-border-default) bg-(--admin-surface-page)'
        }`}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          pick(e.dataTransfer.files[0] ?? null);
        }}
      >
        <AdminIcon icon={file ? ADMIN_ICONS.file : ADMIN_ICONS.upload} />
        {file ? (
          <>
            <span className="adm-type-body-regular-12 min-w-0 flex-1 truncate">
              {file.name} · {formatNumber(Math.round(file.size / 1024))} KB
            </span>
            <button type="button" aria-label="Remove file" onClick={() => pick(null)}>
              <AdminIcon icon={ADMIN_ICONS.x} />
            </button>
          </>
        ) : (
          <button type="button" onClick={() => inputRef.current?.click()} className="adm-type-body-regular-12 text-left">
            Drop file here or browse (PDF, JPG, PNG · max 10 MB)
          </button>
        )}
        <input
          id={id}
          ref={inputRef}
          type="file"
          accept={accept}
          className="sr-only"
          onChange={(e) => pick(e.target.files?.[0] ?? null)}
        />
      </div>
      {message && <span className="adm-type-body-small text-(--admin-status-danger)">{message}</span>}
    </div>
  );
};
