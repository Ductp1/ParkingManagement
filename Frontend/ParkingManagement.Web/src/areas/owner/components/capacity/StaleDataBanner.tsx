import React from 'react';
import { WifiOff, RefreshCw } from 'lucide-react';

interface StaleDataBannerProps {
  isStale: boolean;
  lastSyncAt?: string | null;
  onRefresh?: () => void;
  refreshing?: boolean;
}

export const StaleDataBanner: React.FC<StaleDataBannerProps> = ({
  isStale,
  lastSyncAt,
  onRefresh,
  refreshing = false,
}) => {
  if (!isStale) return null;

  return (
    <div className="rounded-3xl bg-amber-500/10 border-2 border-amber-300 p-4 sm:p-5 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4 animate-fade-in">
      <div className="flex items-start sm:items-center gap-3.5">
        <div className="w-10 h-10 rounded-2xl bg-amber-500 text-white flex items-center justify-center font-bold shrink-0 shadow-sm">
          <WifiOff size={20} className="animate-pulse" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h4 className="font-bold text-amber-950 text-sm">
              Cảnh báo: Dữ liệu trạng thái ô đỗ bị trễ (Stale Data)
            </h4>
            <span className="text-[10px] font-black bg-amber-200 text-amber-900 px-2 py-0.5 rounded-full uppercase tracking-wider">
              Mất kết nối Heartbeat
            </span>
          </div>
          <p className="text-xs text-amber-800 mt-0.5 leading-relaxed">
            Hệ thống không nhận được tín hiệu đồng bộ từ thiết bị trạm barie / phần mềm PMS trong hơn 5 phút. Số lượng chỗ trống hiển thị có thể chưa phản ánh thực tế bãi.
            {lastSyncAt && (
              <span className="block text-[11px] text-amber-700 mt-0.5">
                Lần đồng bộ cuối: <strong>{lastSyncAt}</strong>
              </span>
            )}
          </p>
        </div>
      </div>

      {onRefresh && (
        <button
          type="button"
          onClick={onRefresh}
          disabled={refreshing}
          className="inline-flex items-center gap-1.5 px-4 py-2 text-xs font-bold text-amber-950 bg-amber-200 hover:bg-amber-300 active:bg-amber-400 rounded-xl transition-all self-end sm:self-center shrink-0 shadow-xs disabled:opacity-50"
        >
          <RefreshCw size={14} className={refreshing ? 'animate-spin' : ''} />
          <span>Kiểm tra kết nối lại</span>
        </button>
      )}
    </div>
  );
};
