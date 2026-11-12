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
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
      {/* 1. Tổng sức chứa vật lý */}
      <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
        <div className="flex items-center justify-between">
          <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
            Sức chứa vật lý
          </span>
          <div className="w-9 h-9 rounded-xl bg-slate-100 text-slate-700 flex items-center justify-center">
            <Building2 size={18} />
          </div>
        </div>
        <div>
          <div className="text-3xl font-extrabold text-slate-900 tracking-tight">
            {config.totalSlots}{' '}
            <span className="text-sm font-normal text-slate-500">ô đỗ</span>
          </div>
          <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
            <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
            <span>100% quy mô bãi xe</span>
          </div>
        </div>
      </div>

      {/* 2. Vùng đệm khách vãng lai */}
      <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
        <div className="flex items-center justify-between">
          <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
            Vùng đệm vãng lai
          </span>
          <div className="w-9 h-9 rounded-xl bg-amber-50 text-amber-700 flex items-center justify-center">
            <Users size={18} />
          </div>
        </div>
        <div>
          <div className="text-3xl font-extrabold text-amber-600 tracking-tight">
            {config.calculatedWalkInBufferSlots}{' '}
            <span className="text-sm font-normal text-slate-500">chỗ</span>
          </div>
          <div className="flex items-center gap-1.5 mt-1 text-xs text-amber-700">
            <ShieldCheck size={14} className="shrink-0" />
            <span>Giữ lại {config.walkInBufferPercent}% tại cổng</span>
          </div>
        </div>
      </div>

      {/* 3. Hạn ngạch mở bán trên App */}
      <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
        <div className="flex items-center justify-between">
          <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
            Hạn ngạch bán Online
          </span>
          <div className="w-9 h-9 rounded-xl bg-blue-50 text-blue-700 flex items-center justify-center">
            <Smartphone size={18} />
          </div>
        </div>
        <div>
          <div className="text-3xl font-extrabold text-blue-600 tracking-tight">
            {config.onlineQuota}{' '}
            <span className="text-sm font-normal text-slate-500">/ {config.maxAllowedOnlineQuota}</span>
          </div>
          <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
            <span className="w-2 h-2 rounded-full bg-blue-500"></span>
            <span>Tối đa cho phép: {config.maxAllowedOnlineQuota} chỗ</span>
          </div>
        </div>
      </div>

      {/* 4. Ngưỡng khóa an toàn */}
      <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
        <div className="flex items-center justify-between">
          <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
            Ngưỡng khóa an toàn
          </span>
          <div className="w-9 h-9 rounded-xl bg-rose-50 text-rose-700 flex items-center justify-center">
            <Lock size={18} />
          </div>
        </div>
        <div>
          <div className="text-3xl font-extrabold text-rose-600 tracking-tight">
            ≤ {config.onlineLockThreshold}{' '}
            <span className="text-sm font-normal text-slate-500">chỗ</span>
          </div>
          <div className="flex items-center gap-1.5 mt-1 text-xs text-rose-600">
            <span className="w-2 h-2 rounded-full bg-rose-500 animate-pulse"></span>
            <span>Tự động đóng kênh online khi chạm</span>
          </div>
        </div>
      </div>
    </div>
  );
};
