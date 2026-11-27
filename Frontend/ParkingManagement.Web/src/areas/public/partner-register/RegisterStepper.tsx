import { Check } from 'lucide-react';
import { STEPS, type StepId } from './registrationForm';

export type StepStatus = 'done' | 'current' | 'error' | 'todo';

const STATUS_TEXT: Record<StepStatus, string> = {
  done: 'Đã hoàn tất',
  current: 'Đang thực hiện',
  error: 'Cần sửa',
  todo: 'Chưa thực hiện',
};

const MARKER: Record<StepStatus, string> = {
  done: 'bg-(--admin-surface-selected) text-(--admin-brand-navy-900)',
  current: 'bg-(--admin-brand-navy-900) text-(--admin-text-on-navy)',
  error: 'bg-(--admin-surface-danger) text-(--admin-status-danger)',
  todo: 'bg-(--admin-surface-row-alt) text-(--admin-text-secondary)',
};

interface StepperProps {
  current: StepId;
  statusOf: (step: StepId) => StepStatus;
  /** Chỉ bước đã qua (hoàn tất / cần sửa) mới bấm được để quay lại. */
  onSelect: (step: StepId) => void;
  disabled?: boolean;
}

/** Stepper dọc ở sidebar desktop (Figma 221:139). */
export function RegisterStepper({ current, statusOf, onSelect, disabled }: StepperProps) {
  return (
    <nav aria-label="Các bước đăng ký">
      <ol className="flex flex-col gap-3">
        {STEPS.map((step) => {
          const status = statusOf(step.id);
          const clickable = !disabled && step.id !== current && (status === 'done' || status === 'error');
          const body = (
            <>
              <span className={`flex size-8 shrink-0 items-center justify-center rounded-full text-[13px] font-semibold ${MARKER[status]}`} aria-hidden="true">
                {status === 'done' ? <Check size={16} /> : step.id}
              </span>
              <span className="flex min-w-0 flex-col pt-0.5 text-left">
                <span className={`text-[13px] font-semibold ${status === 'todo' ? 'text-(--admin-text-secondary)' : 'text-(--admin-text-primary)'}`}>{step.title}</span>
                <span className={`text-xs ${status === 'error' ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
                  <span className="sr-only">Bước {step.id}: </span>
                  {STATUS_TEXT[status]}
                </span>
              </span>
            </>
          );
          return (
            <li key={step.id} aria-current={step.id === current ? 'step' : undefined}>
              {clickable ? (
                <button
                  type="button"
                  onClick={() => onSelect(step.id)}
                  className="-m-1 flex w-full items-start gap-3 rounded-lg p-1 outline-none hover:bg-(--admin-surface-selected) focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)"
                >
                  {body}
                </button>
              ) : (
                <div className="flex items-start gap-3">{body}</div>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

/** Mobile / tablet (Figma 221:140…): "Bước x/5 · Tên bước" + thanh 5 đoạn. */
export function CompactStepper({ current, statusOf }: Pick<StepperProps, 'current' | 'statusOf'>) {
  const step = STEPS[current - 1];
  return (
    <div>
      <p className="flex items-baseline gap-3 text-sm" aria-live="polite">
        <span className="font-semibold text-(--admin-text-primary)">
          Bước {current}/{STEPS.length}
        </span>
        <span className="text-(--admin-text-secondary)">{step.title}</span>
      </p>
      <ol className="mt-2 grid grid-cols-5 gap-1" aria-label="Tiến độ đăng ký">
        {STEPS.map((s) => {
          const status = statusOf(s.id);
          return (
            <li
              key={s.id}
              aria-current={s.id === current ? 'step' : undefined}
              className={`h-1 rounded-full ${
                status === 'error' ? 'bg-(--admin-status-danger)' : status === 'todo' ? 'bg-(--admin-chart-track)' : 'bg-(--admin-brand-navy-900)'
              }`}
            >
              <span className="sr-only">
                Bước {s.id} {s.title}: {STATUS_TEXT[status]}
              </span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
