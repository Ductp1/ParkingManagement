import { useEffect, useId, useRef, useState, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { AlertTriangle, Check, Inbox, Info, Loader2, RefreshCw, X } from 'lucide-react';
import { useFocusTrap } from '@/areas/admin/hooks/useFocusTrap';
import { errorText } from './format';

// Token theo Figma "Staff & Shifts" (UI tiếng Việt cho Chủ bãi / Cổng / Kích hoạt).
export const inputClass =
  'h-11 w-full rounded-[10px] border border-[#ccd5e1] bg-white px-3 text-sm text-[#10172a] placeholder:text-[#888b94] focus:border-[#2c5cf2] focus:outline-none focus:ring-2 focus:ring-[#2c5cf2]/20 disabled:bg-[#f2f5f9] disabled:text-[#65738c] aria-invalid:border-[#dc2626] aria-invalid:bg-[#fdf2f2]';
export const cardClass = 'rounded-2xl border border-[#e3e8f0] bg-white p-4 sm:p-6';

const BUTTON = {
  primary: 'bg-[#2c5cf2] text-white hover:bg-[#2450d8]',
  secondary: 'border border-[#e3e8f0] bg-white text-[#10172a] hover:bg-[#f2f5f9]',
  danger: 'bg-[#dc2626] text-white hover:bg-[#b91c1c]',
} as const;

export function Button({ variant = 'primary', busy, children, className = '', disabled, ...rest }:
  ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof BUTTON; busy?: boolean }) {
  return (
    <button type="button" {...rest} disabled={disabled || busy} aria-busy={busy || undefined}
      className={`inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-[10px] px-4 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-45 ${BUTTON[variant]} ${className}`}>
      {busy && <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />}
      {children}
    </button>
  );
}

export const LinkButton = ({ children, className = '', ...rest }: ButtonHTMLAttributes<HTMLButtonElement>) => (
  <button type="button" {...rest} className={`cursor-pointer text-xs font-semibold text-[#2c5cf2] hover:underline disabled:cursor-not-allowed disabled:opacity-45 ${className}`}>
    {children}
  </button>
);

const BANNER = {
  info: { box: 'bg-[#f0f6fe]', icon: Info, color: 'text-[#2c5cf2]' },
  success: { box: 'bg-[#ecfdf3]', icon: Check, color: 'text-[#15803d]' },
  warning: { box: 'border-l-3 border-[#d97706] bg-white', icon: AlertTriangle, color: 'text-[#d97706]' },
  danger: { box: 'bg-[#fdf2f2]', icon: AlertTriangle, color: 'text-[#dc2626]' },
} as const;

export function Banner({ tone, title, children, onClose }:
  { tone: keyof typeof BANNER; title: string; children?: ReactNode; onClose?: () => void }) {
  const { box, icon: Icon, color } = BANNER[tone];
  return (
    <div role={tone === 'danger' ? 'alert' : 'status'} className={`flex items-start gap-3 rounded-[10px] p-4 text-[13px] text-[#10172a] ${box}`}>
      <Icon size={18} className={`mt-0.5 shrink-0 ${color}`} aria-hidden="true" />
      <div className="min-w-0 flex-1">
        <p className="font-semibold">{title}</p>
        {children && <div className="mt-1 leading-relaxed">{children}</div>}
      </div>
      {onClose && (
        <button type="button" onClick={onClose} aria-label="Đóng thông báo" className="cursor-pointer text-[#65738c] hover:text-[#10172a]">
          <X size={16} />
        </button>
      )}
    </div>
  );
}

/** Hộp thoại (hoặc ngăn kéo bên phải): giữ focus, Escape để đóng; không đóng khi đang gửi. */
export function Modal({ title, subtitle, onClose, busy, drawer, children }:
  { title: string; subtitle?: ReactNode; onClose: () => void; busy?: boolean; drawer?: boolean; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const titleId = useId();
  useFocusTrap(ref, true);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape' && !busy) onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose, busy]);
  return (
    <div className={`fixed inset-0 z-[60] flex bg-[#10172a]/45 ${drawer ? 'justify-end' : 'items-center justify-center p-4'}`}>
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}
        className={`flex w-full flex-col gap-4 overflow-y-auto bg-white p-6 shadow-2xl ${drawer ? 'h-full max-w-[480px]' : 'max-h-[92vh] max-w-[600px] rounded-2xl'}`}>
        <header className="flex items-start justify-between gap-3">
          <div>
            <h2 id={titleId} className="text-xl font-bold text-[#10172a]">{title}</h2>
            {subtitle && <p className="mt-1.5 text-[13px] text-[#65738c]">{subtitle}</p>}
          </div>
          <button type="button" onClick={onClose} disabled={busy} aria-label="Đóng" className="cursor-pointer rounded-lg p-1 text-[#65738c] hover:bg-[#f2f5f9]">
            <X size={18} />
          </button>
        </header>
        {children}
      </div>
    </div>
  );
}

export const ModalActions = ({ children }: { children: ReactNode }) => <div className="flex flex-wrap items-center gap-3">{children}</div>;

export function Field({ label, required, error, hint, children }:
  { label: string; required?: boolean; error?: string | null; hint?: string; children: ReactNode }) {
  return (
    <label className="flex min-w-0 flex-col gap-1.5 text-[13px] font-semibold text-[#10172a]">
      <span>{label}{required && <span className="text-[#dc2626]" aria-hidden="true"> *</span>}</span>
      {children}
      {error && <span role="alert" className="text-xs font-normal text-[#dc2626]">{error}</span>}
      {hint && <span className="text-xs font-normal text-[#65738c]">{hint}</span>}
    </label>
  );
}

export const Facts = ({ items }: { items: Array<[string, ReactNode]> }) => (
  <dl className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-x-4 gap-y-3 text-[13px]">
    {items.map(([label, value]) => (
      <div key={label} className="contents">
        <dt className="text-[#65738c]">{label}</dt>
        <dd className="font-semibold text-[#10172a]">{value}</dd>
      </div>
    ))}
  </dl>
);

export const MoneyBox = ({ label, value, highlight }: { label: string; value: ReactNode; highlight?: boolean }) => (
  <div className={`flex-1 rounded-[10px] p-3 ${highlight ? 'bg-[#f0f6fe] text-[#2c5cf2]' : 'bg-[#f2f5f9] text-[#10172a]'}`}>
    <p className="text-[11px] font-semibold tracking-wider text-[#65738c] uppercase">{label}</p>
    <p className="mt-1 text-lg font-bold">{value}</p>
  </div>
);

/** Đang tải / trống / lỗi tải trong một khung. */
export function Placeholder({ kind, title, message, onRetry, children }:
  { kind: 'loading' | 'empty' | 'error'; title: string; message?: string; onRetry?: () => void; children?: ReactNode }) {
  const Icon = kind === 'loading' ? Loader2 : kind === 'empty' ? Inbox : AlertTriangle;
  return (
    <div role={kind === 'error' ? 'alert' : 'status'} className="flex flex-col items-center gap-2 rounded-[10px] border border-[#e3e8f0] bg-white px-4 py-14 text-center">
      <Icon size={24} aria-hidden="true"
        className={kind === 'loading' ? 'animate-spin text-[#2c5cf2] motion-reduce:animate-none' : kind === 'error' ? 'text-[#dc2626]' : 'text-[#65738c]'} />
      <p className="text-sm font-semibold text-[#10172a]">{title}</p>
      {message && <p className="max-w-md text-[13px] text-[#65738c]">{message}</p>}
      {onRetry && <Button variant="secondary" onClick={onRetry}><RefreshCw size={15} aria-hidden="true" />Thử lại</Button>}
      {children}
    </div>
  );
}

export const LoadError = ({ title, error, onRetry }: { title: string; error: unknown; onRetry: () => void }) => (
  <Placeholder kind="error" title={title} message={errorText(error)} onRetry={onRetry} />
);

export function Page({ title, subtitle, children }: { title: string; subtitle: ReactNode; children: ReactNode }) {
  return (
    <div className="min-h-screen bg-[#f8fafb]">
      <div className="mx-auto flex max-w-[1344px] flex-col gap-5 px-4 py-8 sm:px-8 lg:px-16">
        <header>
          <h1 className="text-[28px] font-bold text-[#10172a] sm:text-[32px]">{title}</h1>
          <p className="mt-2 text-sm text-[#65738c]">{subtitle}</p>
        </header>
        {children}
      </div>
    </div>
  );
}

const OWNER_TABS = [
  ['/owner/staff', 'Nhân viên'],
  ['/owner/staff/shifts', 'Lịch ca'],
  ['/owner/staff/handovers', 'Bàn giao & Đối soát'],
] as const;

/** Tab "Nhân sự & Ca trực" của chủ bãi; giữ bãi đang chọn (?lot=). */
export function StaffTabs() {
  const { pathname, search } = useLocation();
  return (
    <nav aria-label="Nhân sự và ca trực" className="flex flex-wrap gap-5 text-[13px] font-bold">
      {OWNER_TABS.map(([to, label]) => (
        <Link key={to} to={to + search} aria-current={pathname === to ? 'page' : undefined}
          className={pathname === to ? 'text-[#2c5cf2]' : 'text-[#65738c] hover:text-[#10172a]'}>
          {label}
        </Link>
      ))}
      {/* Chủ bãi được tự trực cổng tại bãi của mình. */}
      <Link to="/gate/shifts" className="ml-auto text-[#65738c] hover:text-[#10172a]">Tự trực cổng →</Link>
    </nav>
  );
}

/** Tải dữ liệu theo key; đổi key thì bỏ dữ liệu cũ, reload() giữ dữ liệu đang hiện trong lúc tải lại. */
export function useLoad<T>(key: string, load: () => Promise<T>) {
  const [state, setState] = useState<{ key: string; data?: T; error?: unknown; loading: boolean }>({ key, loading: true });
  const [tick, setTick] = useState(0);
  const loader = useRef(load);
  loader.current = load;
  useEffect(() => {
    let active = true;
    setState((s) => ({ key, data: s.key === key ? s.data : undefined, loading: true }));
    loader.current().then(
      (data) => { if (active) setState({ key, data, loading: false }); },
      (error: unknown) => { if (active) setState((s) => ({ key, data: s.data, error, loading: false })); },
    );
    return () => { active = false; };
  }, [key, tick]);
  const current = state.key === key ? state : { key, loading: true, data: undefined, error: undefined };
  return { ...current, reload: () => setTick((t) => t + 1) };
}
