import type { InputHTMLAttributes, ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, Info, Lock, Minus, Plus, X } from 'lucide-react';
import { inputClass, messageId } from './styles';

/**
 * Primitive của trang Đăng ký đối tác (Figma Design System 140:2 – Input h-44 r-8, Button h-40 r-8, Card r-12).
 * Màu lấy từ token Admin Portal (--admin-*, admin.css) – trang bọc trong .admin-scope.
 */

interface FieldProps {
  id: string;
  label: string;
  required?: boolean;
  helper?: string;
  error?: string | null;
  /** Nhãn nhỏ (Tỉnh/Thành phố…) trong nhóm địa chỉ. */
  compact?: boolean;
  children: ReactNode;
}

/** Khung nhãn + nội dung + helper/lỗi (lỗi có icon, gắn aria-describedby qua messageId). */
export function Field({ id, label, required, helper, error, compact, children }: FieldProps) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className={`${compact ? 'text-xs' : 'text-[13px]'} font-semibold text-(--admin-text-primary)`}>
        {label}
        {required && (
          <span className="text-(--admin-status-danger)" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      {children}
      <FieldMessage id={id} helper={helper} error={error} />
    </div>
  );
}

export function FieldMessage({ id, helper, error }: { id: string; helper?: string; error?: string | null }) {
  if (error)
    return (
      <p id={messageId(id)} className="flex items-start gap-1.5 text-xs text-(--admin-status-danger)">
        <AlertTriangle size={14} className="mt-px shrink-0" aria-hidden="true" />
        {error}
      </p>
    );
  if (helper)
    return (
      <p id={messageId(id)} className="text-xs text-(--admin-text-secondary)">
        {helper}
      </p>
    );
  return null;
}

type TextInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'className'> & {
  id: string;
  label: string;
  required?: boolean;
  helper?: string;
  error?: string | null;
  compact?: boolean;
  /** Hậu tố trong ô ("chỗ") */
  suffix?: string;
  locked?: boolean;
};

export function TextInput({ id, label, required, helper, error, compact, suffix, locked, ...rest }: TextInputProps) {
  const describedBy = error || helper ? messageId(id) : undefined;
  return (
    <Field id={id} label={label} required={required} helper={helper} error={error} compact={compact}>
      <div className="relative">
        <input
          id={id}
          aria-required={required || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          className={`${inputClass(error)} ${suffix || locked ? 'pr-12' : ''}`}
          {...rest}
        />
        {suffix && <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-(--admin-text-secondary)">{suffix}</span>}
        {locked && (
          <Lock size={16} aria-hidden="true" className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-(--admin-text-secondary)" />
        )}
      </div>
    </Field>
  );
}

/** Checkbox gốc (bàn phím / screen reader sẵn có), màu navy. */
export function Checkbox({
  id,
  checked,
  onChange,
  disabled,
  children,
  describedBy,
  invalid,
}: {
  id: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  children: ReactNode;
  describedBy?: string;
  invalid?: boolean;
}) {
  return (
    <label htmlFor={id} className={`flex items-start gap-2.5 text-sm text-(--admin-text-primary) ${disabled ? 'opacity-60' : 'cursor-pointer'}`}>
      <input
        id={id}
        type="checkbox"
        checked={checked}
        disabled={disabled}
        aria-describedby={describedBy}
        aria-invalid={invalid || undefined}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-0.5 size-4 shrink-0 cursor-pointer accent-(--admin-brand-navy-900) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--admin-brand-navy-900)"
      />
      <span>{children}</span>
    </label>
  );
}

/** Công tắc "Mở cửa 24/7" (role=switch). */
export function Toggle({ id, checked, onChange, label, disabled }: { id: string; checked: boolean; onChange: (v: boolean) => void; label: string; disabled?: boolean }) {
  return (
    <div className="flex items-center gap-2.5">
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) focus-visible:ring-offset-2 disabled:opacity-50 ${
          checked ? 'bg-(--admin-brand-navy-900)' : 'bg-(--admin-chart-neutral)'
        }`}
      >
        <span className={`inline-block size-4 rounded-full bg-white shadow transition-transform ${checked ? 'translate-x-[18px]' : 'translate-x-0.5'}`} />
      </button>
      <label htmlFor={id} className="cursor-pointer text-sm text-(--admin-text-primary)">
        {label}
      </label>
    </div>
  );
}

/** Ô số kèm nút −/+ (Figma "Số bãi"); không dùng input type=number để tránh spinner. */
export function NumberStepper({
  id,
  label,
  value,
  min,
  max,
  onChange,
  required,
  error,
  disabled,
}: {
  id: string;
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (v: number) => void;
  required?: boolean;
  error?: string | null;
  disabled?: boolean;
}) {
  const btn =
    'flex size-11 items-center justify-center text-(--admin-text-primary) outline-none enabled:hover:bg-(--admin-surface-selected) focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-(--admin-brand-navy-900) disabled:opacity-40';
  return (
    <Field id={id} label={label} required={required} error={error}>
      <div
        className={`inline-flex w-fit items-center overflow-hidden rounded-lg border ${
          error ? 'border-(--admin-status-danger) bg-(--admin-surface-danger)' : 'border-(--admin-border-default) bg-(--admin-surface-card)'
        }`}
      >
        <button type="button" className={btn} aria-label={`Giảm ${label.toLowerCase()}`} disabled={disabled || value <= min} onClick={() => onChange(Math.max(min, value - 1))}>
          <Minus size={16} aria-hidden="true" />
        </button>
        <input
          id={id}
          inputMode="numeric"
          pattern="[0-9]*"
          aria-required={required || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? messageId(id) : undefined}
          disabled={disabled}
          value={Number.isFinite(value) ? String(value) : ''}
          onChange={(e) => {
            const digits = e.target.value.replace(/\D/g, '').slice(0, 3);
            onChange(digits === '' ? Number.NaN : Number(digits));
          }}
          className="h-11 w-14 bg-transparent text-center text-sm text-(--admin-text-primary) outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-(--admin-brand-navy-900)"
        />
        <button type="button" className={btn} aria-label={`Tăng ${label.toLowerCase()}`} disabled={disabled || value >= max} onClick={() => onChange(Math.min(max, (Number.isFinite(value) ? value : 0) + 1))}>
          <Plus size={16} aria-hidden="true" />
        </button>
      </div>
    </Field>
  );
}

type NoticeTone = 'info' | 'warning' | 'danger' | 'success' | 'lock';

const NOTICE_STYLE: Record<NoticeTone, string> = {
  info: 'bg-(--admin-surface-selected) text-(--admin-text-primary)',
  lock: 'bg-(--admin-surface-selected) text-(--admin-text-primary)',
  warning: 'border-l-4 border-(--admin-status-warning) bg-[#fff7ed] text-(--admin-text-primary)',
  danger: 'bg-(--admin-surface-danger) text-(--admin-text-primary)',
  success: 'border-l-4 border-(--admin-status-success) bg-(--admin-surface-card) text-(--admin-text-primary) shadow-sm',
};

const NOTICE_ICON: Record<NoticeTone, ReactNode> = {
  info: <Info size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-(--admin-text-secondary)" />,
  lock: <Lock size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-(--admin-text-secondary)" />,
  warning: <AlertTriangle size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-(--admin-status-warning-text)" />,
  danger: <AlertTriangle size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-(--admin-status-danger)" />,
  success: <CheckCircle2 size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-(--admin-status-success-text)" />,
};

/** Hộp thông báo (Figma Alert): tiêu đề đậm + mô tả nhỏ, tuỳ chọn nút đóng. */
export function Notice({
  tone,
  title,
  children,
  onClose,
  role,
  id,
  tabIndex,
}: {
  tone: NoticeTone;
  title: ReactNode;
  children?: ReactNode;
  onClose?: () => void;
  role?: 'alert' | 'status';
  id?: string;
  tabIndex?: number;
}) {
  return (
    <div id={id} role={role} tabIndex={tabIndex} className={`flex gap-2.5 rounded-lg px-4 py-3 outline-none ${NOTICE_STYLE[tone]}`}>
      {NOTICE_ICON[tone]}
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-semibold">{title}</p>
        {children && <div className="mt-1 text-xs text-(--admin-text-secondary)">{children}</div>}
      </div>
      {onClose && (
        <button
          type="button"
          onClick={onClose}
          aria-label="Đóng thông báo"
          className="flex size-6 shrink-0 items-center justify-center rounded text-(--admin-text-secondary) outline-none hover:bg-black/5 focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)"
        >
          <X size={14} aria-hidden="true" />
        </button>
      )}
    </div>
  );
}
