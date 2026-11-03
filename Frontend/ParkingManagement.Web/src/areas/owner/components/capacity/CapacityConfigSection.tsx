import React, { useState, useEffect, useMemo } from 'react';
import {
  Save,
  RotateCcw,
  Sparkles,
  Sliders,
  PauseCircle,
  PlayCircle,
  AlertTriangle,
  Info,
} from 'lucide-react';
import type {
  LotCapacityConfigDto,
  UpdateLotCapacityConfigRequest,
} from '@/types/ownerCapacity';
import { INTEGRATION_TIER_META } from '@/types/ownerCapacity';
import { CapacityVisualAllocationBar } from './CapacityVisualAllocationBar';

interface CapacityConfigSectionProps {
  config: LotCapacityConfigDto;
  saving: boolean;
  onSave: (payload: UpdateLotCapacityConfigRequest) => Promise<void>;
  onRefresh?: () => Promise<void>;
}

export const CapacityConfigSection: React.FC<CapacityConfigSectionProps> = ({
  config,
  saving,
  onSave,
  onRefresh,
}) => {
  // Trạng thái Form nội bộ
  const [bufferPercent, setBufferPercent] = useState<number>(config.walkInBufferPercent);
  const [onlineQuota, setOnlineQuota] = useState<number>(config.onlineQuota);
  const [lockThreshold, setLockThreshold] = useState<number>(config.onlineLockThreshold);
  const [instantPaused, setInstantPaused] = useState<boolean>(config.instantBookingPaused);

  // Cập nhật lại form khi config từ server thay đổi
  useEffect(() => {
    setBufferPercent(config.walkInBufferPercent);
    setOnlineQuota(config.onlineQuota);
    setLockThreshold(config.onlineLockThreshold);
    setInstantPaused(config.instantBookingPaused);
  }, [config]);

  // Tính toán toán học theo thời gian thực (Real-time recalculation)
  const totalSlots = config.totalSlots;
  const calculatedBufferSlots = useMemo(
    () => Math.round(totalSlots * (bufferPercent / 100)),
    [totalSlots, bufferPercent]
  );
  const maxAllowedOnlineQuota = useMemo(
    () => Math.max(0, totalSlots - calculatedBufferSlots),
    [totalSlots, calculatedBufferSlots]
  );

  // Tự động kiểm tra tính hợp lệ
  const validationError = useMemo(() => {
    if (bufferPercent < 0 || bufferPercent > 100) {
      return 'Tỷ lệ vùng đệm vãng lai phải nằm trong khoảng từ 0% đến 100%.';
    }
    if (onlineQuota < 0) {
      return 'Hạn ngạch bán online không được là số âm.';
    }
    if (onlineQuota > maxAllowedOnlineQuota) {
      return `Hạn ngạch online (${onlineQuota}) vượt quá mức cho phép tối đa (${maxAllowedOnlineQuota}) sau khi trừ vùng đệm vãng lai (${calculatedBufferSlots} chỗ).`;
    }
    if (lockThreshold < 0) {
      return 'Ngưỡng khóa an toàn không được là số âm.';
    }
    if (lockThreshold > onlineQuota) {
      return `Ngưỡng khóa an toàn (${lockThreshold}) không được lớn hơn hạn ngạch online (${onlineQuota}).`;
    }
    return null;
  }, [bufferPercent, onlineQuota, maxAllowedOnlineQuota, calculatedBufferSlots, lockThreshold]);

  // Kiểm tra xem form có sự thay đổi so với dữ liệu gốc không
  const isDirty = useMemo(() => {
    return (
      bufferPercent !== config.walkInBufferPercent ||
      onlineQuota !== config.onlineQuota ||
      lockThreshold !== config.onlineLockThreshold ||
      instantPaused !== config.instantBookingPaused
    );
  }, [bufferPercent, onlineQuota, lockThreshold, instantPaused, config]);

  // Áp dụng bộ Preset mẫu thông minh
  const handleApplyPreset = (presetType: 'manual' | 'smart' | 'peak') => {
    if (presetType === 'manual') {
      const bPercent = 30;
      const bSlots = Math.round(totalSlots * (bPercent / 100));
      const maxQuota = Math.max(0, totalSlots - bSlots);
      setBufferPercent(bPercent);
      setOnlineQuota(maxQuota);
      setLockThreshold(Math.min(5, maxQuota));
    } else if (presetType === 'smart') {
      const bPercent = 10;
      const bSlots = Math.round(totalSlots * (bPercent / 100));
      const maxQuota = Math.max(0, totalSlots - bSlots);
      setBufferPercent(bPercent);
      setOnlineQuota(maxQuota);
      setLockThreshold(Math.min(3, maxQuota));
    } else if (presetType === 'peak') {
      const bPercent = 50;
      const bSlots = Math.round(totalSlots * (bPercent / 100));
      const maxQuota = Math.max(0, totalSlots - bSlots);
      setBufferPercent(bPercent);
      setOnlineQuota(maxQuota);
      setLockThreshold(Math.min(5, maxQuota));
    }
  };

  // Khôi phục dữ liệu ban đầu
  const handleReset = () => {
    setBufferPercent(config.walkInBufferPercent);
    setOnlineQuota(config.onlineQuota);
    setLockThreshold(config.onlineLockThreshold);
    setInstantPaused(config.instantBookingPaused);
    if (onRefresh) {
      void onRefresh();
    }
  };

  // Submit lưu lên server
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (validationError) return;

    await onSave({
      onlineQuota,
      walkInBufferPercent: bufferPercent,
      onlineLockThreshold: lockThreshold,
      instantBookingPaused: instantPaused,
    });
  };

  const tierMeta = INTEGRATION_TIER_META[config.integrationTier] || INTEGRATION_TIER_META.Manual;

  return (
    <div className="space-y-6">
      {/* 1. Thanh phân bổ tỷ lệ trực quan */}
      <CapacityVisualAllocationBar
        totalSlots={totalSlots}
        onlineQuota={onlineQuota}
        walkInBufferSlots={calculatedBufferSlots}
        walkInBufferPercent={bufferPercent}
        onlineLockThreshold={lockThreshold}
      />

      {/* 2. Khung điều khiển & Form cấu hình */}
      <div className="rounded-3xl bg-white border border-slate-200/90 p-5 sm:p-7 shadow-xs space-y-6">
        {/* Header thông tin cấp độ bãi xe */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-5 border-b border-slate-100">
          <div>
            <div className="flex items-center gap-2">
              <span className="p-1.5 rounded-lg bg-indigo-50 text-indigo-600">
                <Sliders size={18} />
              </span>
              <h3 className="text-base font-bold text-slate-900">
                Bảng Điều Khiển Hạn Ngạch & Vùng Đệm (UI-26)
              </h3>
            </div>
            <p className="text-xs text-slate-500 mt-1">
              Điều chỉnh tỷ lệ phân bổ linh hoạt. Mọi thay đổi sẽ có hiệu lực ngay lập tức đối với hệ thống tìm kiếm và đặt chỗ.
            </p>
          </div>

          {/* Badge phân cấp công nghệ bãi */}
          <div className="flex flex-col sm:items-end">
            <span
              className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold border ${tierMeta.badgeClass}`}
            >
              <Sparkles size={13} />
              <span>{tierMeta.label}</span>
            </span>
            <span className="text-[11px] text-slate-400 mt-0.5">
              Khuyến nghị vùng đệm: {tierMeta.recommendedBuffer}%
            </span>
          </div>
        </div>

        {/* 3. Nút chọn Preset cấu hình thông minh (Quick Presets) */}
        <div className="space-y-2">
          <label className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center gap-1.5">
            <Sparkles size={14} className="text-amber-500" />
            <span>Áp dụng mẫu cấu hình khuyến nghị:</span>
          </label>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
            <button
              type="button"
              onClick={() => handleApplyPreset('manual')}
              className={`p-3 rounded-2xl border text-left transition-all cursor-pointer ${
                bufferPercent === 30 && onlineQuota === maxAllowedOnlineQuota
                  ? 'bg-amber-50/80 border-amber-300 ring-2 ring-amber-400/30'
                  : 'bg-slate-50/80 border-slate-200 hover:bg-slate-100/80'
              }`}
            >
              <div className="font-bold text-xs text-slate-900">Bãi thủ công (Mức 0)</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Vãng lai 30% ({Math.round(totalSlots * 0.3)} chỗ) • Khóa ≤ 5
              </div>
            </button>

            <button
              type="button"
              onClick={() => handleApplyPreset('smart')}
              className={`p-3 rounded-2xl border text-left transition-all cursor-pointer ${
                bufferPercent === 10 && onlineQuota === maxAllowedOnlineQuota
                  ? 'bg-indigo-50/80 border-indigo-300 ring-2 ring-indigo-400/30'
                  : 'bg-slate-50/80 border-slate-200 hover:bg-slate-100/80'
              }`}
            >
              <div className="font-bold text-xs text-slate-900">Bãi thông minh (Mức 1-2)</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Vãng lai 10% ({Math.round(totalSlots * 0.1)} chỗ) • Khóa ≤ 3
              </div>
            </button>

            <button
              type="button"
              onClick={() => handleApplyPreset('peak')}
              className={`p-3 rounded-2xl border text-left transition-all cursor-pointer ${
                bufferPercent === 50 && onlineQuota === maxAllowedOnlineQuota
                  ? 'bg-purple-50/80 border-purple-300 ring-2 ring-purple-400/30'
                  : 'bg-slate-50/80 border-slate-200 hover:bg-slate-100/80'
              }`}
            >
              <div className="font-bold text-xs text-slate-900">Giờ cao điểm / Sự kiện</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Vãng lai 50% ({Math.round(totalSlots * 0.5)} chỗ) • Ưu tiên cổng
              </div>
            </button>
          </div>
        </div>

        {/* 4. Form điều khiển chi tiết */}
        <form onSubmit={handleSubmit} className="space-y-6 pt-2">
          {/* Cảnh báo lỗi Validation nếu vi phạm toán học */}
          {validationError && (
            <div className="p-3.5 rounded-2xl bg-rose-50 border border-rose-200 text-rose-800 text-xs font-semibold flex items-center gap-2 animate-shake">
              <AlertTriangle size={16} className="shrink-0 text-rose-600" />
              <span>{validationError}</span>
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Control 1: Tỷ lệ vùng đệm khách vãng lai */}
            <div className="p-4 sm:p-5 rounded-2xl bg-amber-50/40 border border-amber-200/60 space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <label className="text-xs font-bold text-amber-950 uppercase tracking-wider block">
                    Tỷ lệ Vùng đệm Vãng lai
                  </label>
                  <span className="text-[11px] text-amber-800">
                    Quy đổi: <strong>{calculatedBufferSlots}</strong> / {totalSlots} chỗ
                  </span>
                </div>
                <div className="flex items-center gap-1.5">
                  <input
                    type="number"
                    min={0}
                    max={100}
                    value={bufferPercent}
                    onChange={(e) => {
                      const val = parseInt(e.target.value, 10) || 0;
                      setBufferPercent(val);
                      // Tự động điều chỉnh onlineQuota nếu vượt quá maxAllowed mới
                      const newBufferSlots = Math.round(totalSlots * (val / 100));
                      const newMaxQuota = Math.max(0, totalSlots - newBufferSlots);
                      if (onlineQuota > newMaxQuota) {
                        setOnlineQuota(newMaxQuota);
                      }
                    }}
                    className="w-16 px-2.5 py-1.5 text-center font-bold text-sm bg-white border border-amber-300 rounded-xl text-amber-900 focus:outline-hidden focus:ring-2 focus:ring-amber-500"
                  />
                  <span className="text-xs font-bold text-amber-900">%</span>
                </div>
              </div>

              {/* Slider % Vùng đệm */}
              <input
                type="range"
                min={0}
                max={100}
                step={5}
                value={bufferPercent}
                onChange={(e) => {
                  const val = parseInt(e.target.value, 10);
                  setBufferPercent(val);
                  const newBufferSlots = Math.round(totalSlots * (val / 100));
                  const newMaxQuota = Math.max(0, totalSlots - newBufferSlots);
                  if (onlineQuota > newMaxQuota) {
                    setOnlineQuota(newMaxQuota);
                  }
                }}
                className="w-full accent-amber-500 cursor-pointer h-2 bg-amber-200 rounded-lg appearance-none"
              />

              <div className="flex justify-between text-[11px] text-amber-700/80 font-medium">
                <span>0% (Không giữ lại)</span>
                <span>Khuyến nghị: {tierMeta.recommendedBuffer}%</span>
                <span>100% (Khóa online)</span>
              </div>
            </div>

            {/* Control 2: Hạn ngạch mở bán trên App (Online Quota) */}
            <div className="p-4 sm:p-5 rounded-2xl bg-indigo-50/40 border border-indigo-200/60 space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <label className="text-xs font-bold text-indigo-950 uppercase tracking-wider block">
                    Hạn ngạch bán Online (Quota)
                  </label>
                  <span className="text-[11px] text-indigo-800">
                    Tối đa cho phép: <strong>{maxAllowedOnlineQuota} chỗ</strong>
                  </span>
                </div>
                <div className="flex items-center gap-1.5">
                  <input
                    type="number"
                    min={0}
                    max={maxAllowedOnlineQuota}
                    value={onlineQuota}
                    onChange={(e) => {
                      const val = parseInt(e.target.value, 10) || 0;
                      setOnlineQuota(val);
                      if (lockThreshold > val) {
                        setLockThreshold(val);
                      }
                    }}
                    className="w-20 px-2.5 py-1.5 text-center font-bold text-sm bg-white border border-indigo-300 rounded-xl text-indigo-900 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
                  />
                  <span className="text-xs font-bold text-indigo-900">chỗ</span>
                </div>
              </div>

              {/* Slider Quota */}
              <input
                type="range"
                min={0}
                max={Math.max(1, maxAllowedOnlineQuota)}
                step={1}
                value={Math.min(onlineQuota, maxAllowedOnlineQuota)}
                onChange={(e) => {
                  const val = parseInt(e.target.value, 10);
                  setOnlineQuota(val);
                  if (lockThreshold > val) {
                    setLockThreshold(val);
                  }
                }}
                className="w-full accent-indigo-600 cursor-pointer h-2 bg-indigo-200 rounded-lg appearance-none"
              />

              <div className="flex justify-between text-[11px] text-indigo-700/80 font-medium">
                <span>0 chỗ</span>
                <span>Chiếm {Math.round((onlineQuota / totalSlots) * 100)}% bãi</span>
                <span>Tối đa: {maxAllowedOnlineQuota}</span>
              </div>
            </div>

            {/* Control 3: Ngưỡng tự động khóa nhận xe trực tuyến */}
            <div className="p-4 sm:p-5 rounded-2xl bg-slate-50 border border-slate-200/80 space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <label className="text-xs font-bold text-slate-800 uppercase tracking-wider block">
                    Ngưỡng Khóa Kênh Trực Tuyến
                  </label>
                  <span className="text-[11px] text-slate-500">
                    Khi số chỗ trống online ≤ mức này sẽ tự động đóng nhận đặt
                  </span>
                </div>
                <div className="flex items-center gap-1.5">
                  <input
                    type="number"
                    min={0}
                    max={onlineQuota}
                    value={lockThreshold}
                    onChange={(e) => setLockThreshold(parseInt(e.target.value, 10) || 0)}
                    className="w-16 px-2.5 py-1.5 text-center font-bold text-sm bg-white border border-slate-300 rounded-xl text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-500"
                  />
                  <span className="text-xs font-bold text-slate-700">chỗ</span>
                </div>
              </div>

              <input
                type="range"
                min={0}
                max={Math.max(1, Math.min(20, onlineQuota))}
                step={1}
                value={Math.min(lockThreshold, onlineQuota)}
                onChange={(e) => setLockThreshold(parseInt(e.target.value, 10))}
                className="w-full accent-rose-600 cursor-pointer h-2 bg-slate-200 rounded-lg appearance-none"
              />

              <div className="flex justify-between text-[11px] text-slate-500 font-medium">
                <span>0 chỗ (Bán đến hết)</span>
                <span>Mặc định: {tierMeta.recommendedBuffer === 30 ? 5 : 3} chỗ</span>
                <span>Tối đa: {Math.min(20, onlineQuota)}</span>
              </div>
            </div>

            {/* Control 4: Công tắc Tạm dừng Nhận khách tức thời (Instant Booking) */}
            <div className="p-4 sm:p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col justify-between space-y-3">
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-slate-800 uppercase tracking-wider">
                    Nhận Khách Đặt Vào Ngay (Instant)
                  </span>
                  <button
                    type="button"
                    onClick={() => setInstantPaused(!instantPaused)}
                    className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden ${
                      instantPaused ? 'bg-amber-500' : 'bg-emerald-500'
                    }`}
                  >
                    <span
                      className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out ${
                        instantPaused ? 'translate-x-5' : 'translate-x-0'
                      }`}
                    />
                  </button>
                </div>
                <p className="text-[11px] text-slate-500 mt-1 leading-relaxed">
                  {instantPaused ? (
                    <span className="text-amber-700 font-semibold flex items-center gap-1">
                      <PauseCircle size={13} />
                      Đang TẠM DỪNG nhận khách đặt nhanh. Tài xế không thể đặt chỗ vào ngay.
                    </span>
                  ) : (
                    <span className="text-emerald-700 font-semibold flex items-center gap-1">
                      <PlayCircle size={13} />
                      Đang MỞ nhận khách đặt vào ngay bình thường.
                    </span>
                  )}
                </p>
              </div>

              <div className="pt-2 border-t border-slate-200/60 flex items-center gap-1 text-[11px] text-slate-400">
                <Info size={13} />
                <span>Bật khi bãi xe gặp sự cố nội bộ hoặc cần ưu tiên đón đoàn xe lớn.</span>
              </div>
            </div>
          </div>

          {/* 5. Nút Thao tác Cuối Form */}
          <div className="flex flex-wrap items-center justify-between gap-3 pt-4 border-t border-slate-100">
            <div className="text-xs text-slate-500 flex items-center gap-1.5">
              {config.updatedAtUtc && (
                <span>
                  Cập nhật lần cuối:{' '}
                  <strong>{new Date(config.updatedAtUtc).toLocaleString('vi-VN')}</strong>
                </span>
              )}
            </div>

            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={handleReset}
                disabled={!isDirty || saving}
                className="px-4 py-2.5 rounded-xl border border-slate-300 text-slate-700 hover:bg-slate-50 text-xs font-bold transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5 cursor-pointer"
              >
                <RotateCcw size={14} />
                <span>Khôi phục</span>
              </button>

              <button
                type="submit"
                disabled={!isDirty || !!validationError || saving}
                className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 cursor-pointer"
              >
                <Save size={15} />
                <span>{saving ? 'Đang lưu cấu hình...' : 'Lưu cấu hình sức chứa'}</span>
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};
