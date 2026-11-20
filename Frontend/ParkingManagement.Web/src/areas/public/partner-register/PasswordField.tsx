import { useId, useState } from 'react';
import { AlertTriangle, Eye, EyeOff } from 'lucide-react';
import { inputClass } from './styles';

interface PasswordFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  /** id của khối mô tả (checklist / lỗi) gắn aria-describedby. */
  describedBy?: string;
  invalid?: boolean;
  autoFocus?: boolean;
}

/**
 * Ô mật khẩu cùng kiểu input trang đăng ký (cao 44, bo 8) với nút hiện / ẩn và cảnh báo Caps Lock; autocomplete="new-password".
 * Dùng cho cả "Mật khẩu" và "Xác nhận mật khẩu" để hai ô giống hệt nhau.
 */
export function PasswordField({ label, value, onChange, disabled, describedBy, invalid, autoFocus }: PasswordFieldProps) {
  const id = useId();
  const [show, setShow] = useState(false);
  const [caps, setCaps] = useState(false);
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] font-semibold text-(--admin-text-primary)">
        {label}
        <span className="text-(--admin-status-danger)" aria-hidden="true">
          {' '}
          *
        </span>
      </label>
      <div className="relative">
        <input
          id={id}
          type={show ? 'text' : 'password'}
          autoComplete="new-password"
          autoFocus={autoFocus}
          value={value}
          disabled={disabled}
          aria-required="true"
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={(e) => setCaps(e.getModifierState('CapsLock'))}
          onKeyUp={(e) => setCaps(e.getModifierState('CapsLock'))}
          onBlur={() => setCaps(false)}
          className={`${inputClass(invalid ? 'invalid' : null)} pr-11`}
        />
        <button
          type="button"
          onClick={() => setShow((v) => !v)}
          aria-label={show ? `Ẩn ${label.toLowerCase()}` : `Hiện ${label.toLowerCase()}`}
          aria-pressed={show}
          className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-lg text-(--admin-text-secondary) outline-none hover:text-(--admin-text-primary) focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-(--admin-brand-navy-900)"
        >
          {show ? <EyeOff size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
        </button>
      </div>
      {caps && (
        <p role="status" className="flex items-center gap-1.5 text-xs text-(--admin-status-warning-text)">
          <AlertTriangle size={14} aria-hidden="true" />
          Caps Lock đang bật
        </p>
      )}
    </div>
  );
}
