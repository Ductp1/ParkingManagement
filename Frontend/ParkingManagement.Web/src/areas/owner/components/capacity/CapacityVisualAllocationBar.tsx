import React from 'react';
import { Smartphone, Users, HelpCircle, Lock } from 'lucide-react';

interface CapacityVisualAllocationBarProps {
  totalSlots: number;
  onlineQuota: number;
  walkInBufferSlots: number;
  walkInBufferPercent: number;
  onlineLockThreshold: number;
}

export const CapacityVisualAllocationBar: React.FC<CapacityVisualAllocationBarProps> = ({
  totalSlots,
  onlineQuota,
  walkInBufferSlots,
  walkInBufferPercent,
  onlineLockThreshold,
}) => {
  if (totalSlots <= 0) return null;

  // Tính phần trăm thực tế trên thanh biểu đồ
  const bufferPercent = Math.min(100, Math.max(0, (walkInBufferSlots / totalSlots) * 100));
  const quotaPercent = Math.min(100 - bufferPercent, Math.max(0, (onlineQuota / totalSlots) * 100));
  const unallocatedSlots = Math.max(0, totalSlots - (onlineQuota + walkInBufferSlots));
  const unallocatedPercent = Math.max(0, 100 - (bufferPercent + quotaPercent));

  return (
    <div className="p-5 sm:p-6 rounded-3xl bg-white border border-slate-200/90 shadow-xs space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-2 border-b border-slate-100">
        <div>
          <h3 className="text-sm sm:text-base font-bold text-slate-900 flex items-center gap-2">
            <span>Sơ đồ phân bổ Sức chứa Thực tế</span>
            <span className="text-[11px] font-semibold text-slate-500 bg-slate-100 px-2 py-0.5 rounded-md">
              Tổng {totalSlots} chỗ
            </span>
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Trực quan hóa tỷ lệ phân chia giữa khách vãng lai tại cổng và khách đặt trước qua ứng dụng.
          </p>
        </div>

        <div className="flex items-center gap-2 text-xs font-semibold text-slate-600">
          <HelpCircle size={14} className="text-slate-400" />
          <span>Khóa an toàn: ≤ {onlineLockThreshold} chỗ</span>
        </div>
      </div>

      {/* Thanh Bar phân bổ trực quan */}
      <div className="space-y-2">
        <div className="h-6 sm:h-7 w-full rounded-xl bg-slate-100 p-1 flex overflow-hidden shadow-inner gap-0.5">
          {/* 1. Phần Vùng đệm Vãng lai (Amber) */}
          {bufferPercent > 0 && (
            <div
              style={{ width: `${bufferPercent}%` }}
              className="h-full rounded-lg bg-linear-to-r from-amber-400 to-amber-500 transition-all duration-300 flex items-center justify-center text-[10px] sm:text-xs font-bold text-white shadow-2xs relative group cursor-pointer"
              title={`Vùng đệm vãng lai: ${walkInBufferSlots} chỗ (${walkInBufferPercent}%)`}
            >
              {bufferPercent >= 12 && (
                <span className="truncate px-1 flex items-center gap-1">
                  <Users size={12} className="shrink-0" />
                  <span>{walkInBufferSlots}</span>
                </span>
              )}
            </div>
          )}

          {/* 2. Phần Hạn ngạch Online (Indigo) */}
          {quotaPercent > 0 && (
            <div
              style={{ width: `${quotaPercent}%` }}
              className="h-full rounded-lg bg-linear-to-r from-blue-500 to-blue-600 transition-all duration-300 flex items-center justify-center text-[10px] sm:text-xs font-bold text-white shadow-2xs relative group cursor-pointer"
              title={`Hạn ngạch Online: ${onlineQuota} chỗ (${Math.round((onlineQuota / totalSlots) * 100)}%)`}
            >
              {quotaPercent >= 12 && (
                <span className="truncate px-1 flex items-center gap-1">
                  <Smartphone size={12} className="shrink-0" />
                  <span>{onlineQuota} chỗ</span>
                </span>
              )}
            </div>
          )}

          {/* 3. Phần Sức chứa dự trữ chưa mở bán (Slate) */}
          {unallocatedPercent > 0 && (
            <div
              style={{ width: `${unallocatedPercent}%` }}
              className="h-full rounded-lg bg-slate-200 text-slate-600 transition-all duration-300 flex items-center justify-center text-[10px] sm:text-xs font-semibold relative group cursor-pointer"
              title={`Chưa phân bổ: ${unallocatedSlots} chỗ (${Math.round(unallocatedPercent)}%)`}
            >
              {unallocatedPercent >= 12 && (
                <span className="truncate px-1 text-[11px] text-slate-500">
                  +{unallocatedSlots} dự phòng
                </span>
              )}
            </div>
          )}
        </div>

        {/* Chú giải (Legend) */}
        <div className="flex flex-wrap items-center justify-between gap-3 text-xs pt-2">
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="w-3.5 h-3.5 rounded-md bg-amber-500 shrink-0"></span>
              <span className="text-slate-700 font-medium">
                Vãng lai tại cổng: <strong>{walkInBufferSlots} chỗ</strong> ({walkInBufferPercent}%)
              </span>
            </div>

            <div className="flex items-center gap-2">
              <span className="w-3.5 h-3.5 rounded-md bg-blue-600 shrink-0"></span>
              <span className="text-slate-700 font-medium">
                Mở bán trên App: <strong>{onlineQuota} chỗ</strong> ({Math.round((onlineQuota / totalSlots) * 100)}%)
              </span>
            </div>

            {unallocatedSlots > 0 && (
              <div className="flex items-center gap-2">
                <span className="w-3.5 h-3.5 rounded-md bg-slate-200 border border-slate-300 shrink-0"></span>
                <span className="text-slate-500">
                  Dự phòng chưa bán: <strong>{unallocatedSlots} chỗ</strong>
                </span>
              </div>
            )}
          </div>

          <div className="inline-flex items-center gap-1 text-[11px] font-semibold text-rose-600 bg-rose-50 border border-rose-200 px-2.5 py-1 rounded-lg">
            <Lock size={12} />
            <span>Ngưỡng ngắt nhận xe: còn ≤ {onlineLockThreshold} chỗ</span>
          </div>
        </div>
      </div>
    </div>
  );
};
