import React from 'react';
import {
  Building2,
  Users,
  Smartphone,
  ShieldCheck,
  Lock,
} from 'lucide-react';
import type { LotCapacityConfigDto } from '@/types/ownerCapacity';

interface CapacitySummaryCardsProps {
  config: LotCapacityConfigDto;
}

export const CapacitySummaryCards: React.FC<CapacitySummaryCardsProps> = ({ config }) => {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3.5 sm:gap-4">
      {/* 1. Tổng sức chứa vật lý */}
      <div className="p-4 rounded-2xl bg-white border border-slate-200/90 shadow-2xs">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
            Sức chứa vật lý
          </span>
          <div className="p-1.5 rounded-lg bg-slate-100 text-slate-600">
            <Building2 size={16} />
          </div>
        </div>
        <div className="text-xl sm:text-2xl font-extrabold text-slate-900 mt-2">
          {config.totalSlots}{' '}
          <span className="text-xs font-semibold text-slate-500">ô đỗ</span>
        </div>
        <p className="text-[11px] text-slate-500 mt-1">100% quy mô bãi xe</p>
      </div>

      {/* 2. Vùng đệm khách vãng lai */}
      <div className="p-4 rounded-2xl bg-amber-50/60 border border-amber-200/80 shadow-2xs">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-bold text-amber-700 uppercase tracking-wider">
            Vùng đệm vãng lai
          </span>
          <div className="p-1.5 rounded-lg bg-amber-100 text-amber-700">
            <Users size={16} />
          </div>
        </div>
        <div className="text-xl sm:text-2xl font-extrabold text-amber-900 mt-2">
          {config.calculatedWalkInBufferSlots}{' '}
          <span className="text-xs font-semibold text-amber-700">chỗ</span>
        </div>
        <p className="text-[11px] text-amber-800/80 mt-1 flex items-center gap-1 font-medium">
          <ShieldCheck size={13} className="text-amber-600 shrink-0" />
          <span>Giữ lại {config.walkInBufferPercent}% tại cổng</span>
        </p>
      </div>

      {/* 3. Hạn ngạch mở bán trên App */}
      <div className="p-4 rounded-2xl bg-indigo-50/60 border border-indigo-200/80 shadow-2xs">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-bold text-indigo-700 uppercase tracking-wider">
            Hạn ngạch bán Online
          </span>
          <div className="p-1.5 rounded-lg bg-indigo-100 text-indigo-700">
            <Smartphone size={16} />
          </div>
        </div>
        <div className="text-xl sm:text-2xl font-extrabold text-indigo-900 mt-2">
          {config.onlineQuota}{' '}
          <span className="text-xs font-semibold text-indigo-600">/ {config.maxAllowedOnlineQuota}</span>
        </div>
        <p className="text-[11px] text-indigo-800/80 mt-1 font-medium">
          Tối đa cho phép: {config.maxAllowedOnlineQuota} chỗ
        </p>
      </div>

      {/* 4. Ngưỡng khóa an toàn */}
      <div className="p-4 rounded-2xl bg-rose-50/60 border border-rose-200/80 shadow-2xs">
        <div className="flex items-center justify-between">
          <span className="text-[11px] font-bold text-rose-700 uppercase tracking-wider">
            Ngưỡng khóa an toàn
          </span>
          <div className="p-1.5 rounded-lg bg-rose-100 text-rose-700">
            <Lock size={16} />
          </div>
        </div>
        <div className="text-xl sm:text-2xl font-extrabold text-rose-900 mt-2">
          ≤ {config.onlineLockThreshold}{' '}
          <span className="text-xs font-semibold text-rose-700">chỗ</span>
        </div>
        <p className="text-[11px] text-rose-800/80 mt-1 font-medium">
          Tự động đóng kênh online khi chạm
        </p>
      </div>
    </div>
  );
};
