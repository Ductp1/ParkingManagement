import React from 'react';
import { Clock, ArrowRight } from 'lucide-react';
import type { OwnerExitStatusDto } from '@/api/ownerExitApi';

interface OwnerExitStatusBannerProps {
  exitStatus: OwnerExitStatusDto | null;
  onOpenModal: () => void;
}

export const OwnerExitStatusBanner: React.FC<OwnerExitStatusBannerProps> = ({
  exitStatus,
  onOpenModal,
}) => {
  if (!exitStatus || exitStatus.status !== 'Leaving') return null;

  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '--';
    try {
      return new Date(isoString).toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return isoString;
    }
  };

  return (
    <div className="rounded-3xl bg-gradient-to-r from-amber-500/10 via-amber-50 to-orange-500/10 border border-amber-300 p-4 sm:p-5 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4 animate-fade-in">
      <div className="flex items-start sm:items-center gap-3.5">
        <div className="w-10 h-10 rounded-2xl bg-amber-500 text-white flex items-center justify-center font-bold shrink-0 shadow-sm">
          <Clock size={20} className="animate-pulse" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h4 className="font-bold text-amber-950 text-sm">
              Đang trong thời hạn thông báo rời nền tảng (US-057)
            </h4>
            <span className="text-[11px] font-black bg-amber-200 text-amber-900 px-2 py-0.5 rounded-full">
              Còn {exitStatus.remainingDays ?? 0} ngày
            </span>
          </div>
          <p className="text-xs text-amber-800 mt-0.5">
            Hợp đồng đối tác dự kiến chấm dứt vào ngày{' '}
            <strong className="text-amber-950">{formatDate(exitStatus.exitEffectiveAtUtc)}</strong>. Bãi xe vẫn cần phục vụ các lượt đặt chỗ đã xác nhận.
          </p>
        </div>
      </div>

      <div className="flex items-center gap-2 self-end sm:self-center shrink-0">
        <button
          type="button"
          onClick={onOpenModal}
          className="inline-flex items-center gap-1.5 px-4 py-2 text-xs font-bold text-amber-900 bg-amber-100 hover:bg-amber-200 active:bg-amber-300 rounded-xl transition-all shadow-xs"
        >
          <span>Quản lý thủ tục rời</span>
          <ArrowRight size={14} />
        </button>
      </div>
    </div>
  );
};
