import React from 'react';
import { Check, FileText, Camera, Flame, CalendarCheck } from 'lucide-react';

export interface StepItem {
  step: number;
  title: string;
  subtitle: string;
  icon: React.ReactNode;
}

interface KybStepIndicatorProps {
  currentStep: number;
  completedSteps: number[];
  isLargeLot?: boolean;
  onSelectStep?: (step: number) => void;
  disabled?: boolean;
}

export const KybStepIndicator: React.FC<KybStepIndicatorProps> = ({
  currentStep,
  completedSteps,
  isLargeLot = false,
  onSelectStep,
  disabled = false,
}) => {
  const steps: StepItem[] = [
    {
      step: 1,
      title: 'Pháp lý Doanh nghiệp',
      subtitle: 'Giấy phép ĐKKD',
      icon: <FileText size={18} />,
    },
    {
      step: 2,
      title: 'Hiện trường & GPS',
      subtitle: 'Ảnh chụp & Vị trí',
      icon: <Camera size={18} />,
    },
    {
      step: 3,
      title: 'An toàn PCCC',
      subtitle: 'Chứng nhận PCCC',
      icon: <Flame size={18} />,
    },
    {
      step: 4,
      title: 'Khảo sát Thực địa',
      subtitle: isLargeLot
        ? 'Lịch hẹn (Bãi > 50 chỗ)'
        : 'Miễn khảo sát (Bãi ≤ 50 chỗ)',
      icon: <CalendarCheck size={18} />,
    },
  ];

  return (
    <div className="w-full bg-white/90 backdrop-blur-xl border border-slate-200/90 rounded-2xl p-4 sm:p-6 shadow-xs">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 relative">
        {steps.map((s, index) => {
          const isCurrent = s.step === currentStep;
          const isDone = completedSteps.includes(s.step);
          const maxAllowedStep = Math.max(0, ...completedSteps) + 1;
          const isClickable = !disabled && (isDone || s.step <= maxAllowedStep);

          // Nhãn hiển thị trạng thái của bước
          const getStatusBadge = () => {
            if (isDone) {
              return isCurrent
                ? `Bước ${s.step} · Xong (Đang xem)`
                : `Bước ${s.step} · Xong`;
            }
            if (isCurrent) {
              return `Bước ${s.step} · Đang thực hiện`;
            }
            return `Bước ${s.step} · Chưa thực hiện`;
          };

          return (
            <button
              key={s.step}
              type="button"
              disabled={!isClickable}
              onClick={() => onSelectStep && isClickable && onSelectStep(s.step)}
              className={`flex items-center gap-3.5 p-3 rounded-xl border text-left transition-all ${
                isDone
                  ? isCurrent
                    ? 'border-emerald-500 bg-emerald-50/80 shadow-xs ring-2 ring-emerald-500/25'
                    : 'border-emerald-200 bg-emerald-50/40 hover:bg-emerald-50 text-slate-800'
                  : isCurrent
                    ? 'border-indigo-600 bg-indigo-50/70 shadow-xs ring-2 ring-indigo-500/20 text-slate-900'
                    : 'border-slate-200/80 bg-slate-50/40 text-slate-400 cursor-not-allowed opacity-75'
              } ${isClickable && !isCurrent ? 'cursor-pointer hover:border-slate-300' : ''}`}
            >
              {/* Badge Icon số bước hoặc tick hoàn thành */}
              <div
                className={`w-10 h-10 rounded-xl flex items-center justify-center font-bold text-sm shrink-0 transition-transform ${
                  isDone
                    ? isCurrent
                      ? 'bg-emerald-600 text-white shadow-xs scale-105'
                      : 'bg-emerald-600 text-white'
                    : isCurrent
                      ? 'bg-indigo-600 text-white shadow-xs scale-105'
                      : 'bg-slate-100 text-slate-400 border border-slate-200'
                }`}
              >
                {isDone ? <Check size={18} strokeWidth={2.5} /> : s.icon}
              </div>

              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-1">
                  <span
                    className={`text-[10px] font-extrabold uppercase tracking-wider ${
                      isDone
                        ? isCurrent
                          ? 'text-emerald-800'
                          : 'text-emerald-700'
                        : isCurrent
                          ? 'text-indigo-700'
                          : 'text-slate-400'
                    }`}
                  >
                    {getStatusBadge()}
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono">
                    {index + 1}/4
                  </span>
                </div>
                <div
                  className={`text-xs font-bold truncate mt-0.5 ${
                    isDone
                      ? 'text-slate-900'
                      : isCurrent
                        ? 'text-slate-900'
                        : 'text-slate-500'
                  }`}
                >
                  {s.title}
                </div>
                <div
                  className={`text-[11px] truncate ${
                    isDone
                      ? 'text-emerald-700 font-medium'
                      : isCurrent
                        ? 'text-slate-600'
                        : 'text-slate-400'
                  }`}
                >
                  {s.subtitle}
                </div>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
};
