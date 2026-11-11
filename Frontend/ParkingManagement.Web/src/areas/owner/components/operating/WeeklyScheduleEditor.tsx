import React, { useState, useEffect, useMemo } from 'react';
import {
  Clock,
  CalendarDays,
  Sparkles,
  Save,
  RotateCcw,
  Moon,
  Sun,
  Copy,
  Info,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Calendar,
  Layers,
  AlertCircle,
  Trash2,
} from 'lucide-react';
import type {
  LotOperatingHourItemDto,
  LotOperatingHoursWeeklyDto,
  UpdateLotOperatingHourItemRequest,
  DayOfWeekNumber,
} from '@/types/ownerLotDetails';
import { DAY_OF_WEEK_NAMES, WEEK_DAYS_ORDER } from '@/types/ownerLotDetails';

interface WeeklyScheduleEditorProps {
  schedule: LotOperatingHourItemDto[];
  weeklyDto?: LotOperatingHoursWeeklyDto | null;
  saving: boolean;
  initialMode?: 'base' | 'week';
  initialYear?: number;
  initialWeek?: number;
  onSave: (
    schedule: UpdateLotOperatingHourItemRequest[],
    year?: number,
    weekOfYear?: number,
    reason?: string
  ) => Promise<void>;
  onSelectWeek?: (year?: number, weekOfYear?: number) => Promise<void>;
  onDeleteWeekOverride?: (year: number, weekOfYear: number) => Promise<void>;
  onRefresh?: () => void | Promise<void>;
}

// Hàm chuẩn hóa dayOfWeek từ DTO (hỗ trợ cả enum name string và number)
const getDayNumber = (s: LotOperatingHourItemDto): number => {
  if (typeof s.dayOfWeekNumber === 'number') return s.dayOfWeekNumber;
  if (typeof s.dayOfWeek === 'number') return s.dayOfWeek;
  const nameMap: Record<string, number> = {
    Sunday: 0,
    Monday: 1,
    Tuesday: 2,
    Wednesday: 3,
    Thursday: 4,
    Friday: 5,
    Saturday: 6,
  };
  return nameMap[s.dayOfWeek as string] ?? 0;
};

// Tính tuần ISO hiện tại
function getCurrentIsoWeek(): { year: number; week: number } {
  const now = new Date();
  const date = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
  date.setUTCDate(date.getUTCDate() + 4 - (date.getUTCDay() || 7));
  const yearStart = new Date(Date.UTC(date.getUTCFullYear(), 0, 1));
  const weekNo = Math.ceil(((date.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
  return { year: date.getUTCFullYear(), week: weekNo };
}

// Tính khoảng ngày của một tuần (Thứ Hai -> Chủ Nhật)
function getIsoWeekRange(year: number, week: number): { startStr: string; endStr: string } {
  const simple = new Date(Date.UTC(year, 0, 1 + (week - 1) * 7));
  const dayOfWeek = simple.getUTCDay();
  const isoWeekStart = new Date(simple);
  if (dayOfWeek <= 4) {
    isoWeekStart.setUTCDate(simple.getUTCDate() - simple.getUTCDay() + 1);
  } else {
    isoWeekStart.setUTCDate(simple.getUTCDate() + 8 - simple.getUTCDay());
  }
  const isoWeekEnd = new Date(isoWeekStart);
  isoWeekEnd.setUTCDate(isoWeekStart.getUTCDate() + 6);

  const fmt = (d: Date) =>
    `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}/${d.getUTCFullYear()}`;

  return { startStr: fmt(isoWeekStart), endStr: fmt(isoWeekEnd) };
}

export const WeeklyScheduleEditor: React.FC<WeeklyScheduleEditorProps> = ({
  schedule,
  weeklyDto,
  saving,
  initialMode,
  initialYear,
  initialWeek,
  onSave,
  onSelectWeek,
  onDeleteWeekOverride,
  onRefresh,
}) => {
  // Chế độ xem: 'base' (lịch định kỳ cơ sở chung - MẶC ĐỊNH) hoặc 'week' (theo tuần cụ thể)
  const [viewMode, setViewMode] = useState<'base' | 'week'>(() => {
    if (initialMode) return initialMode;
    return weeklyDto?.year && weeklyDto?.weekOfYear ? 'week' : 'base';
  });

  // Thông tin tuần hiện tại
  const nowWeek = useMemo(getCurrentIsoWeek, []);
  const [selectedYear, setSelectedYear] = useState<number>(
    initialYear ?? weeklyDto?.year ?? nowWeek.year
  );
  const [selectedWeek, setSelectedWeek] = useState<number>(
    initialWeek ?? weeklyDto?.weekOfYear ?? nowWeek.week
  );

  // Lý do tùy biến riêng cho tuần
  const [overrideReason, setOverrideReason] = useState<string>('');

  // Trạng thái cục bộ 7 ngày
  const [items, setItems] = useState<UpdateLotOperatingHourItemRequest[]>([]);

  // Đồng bộ khi prop schedule thay đổi
  useEffect(() => {
    if (schedule && schedule.length > 0) {
      const map = new Map<number, LotOperatingHourItemDto>();
      schedule.forEach((s) => map.set(getDayNumber(s), s));

      const initialList: UpdateLotOperatingHourItemRequest[] = WEEK_DAYS_ORDER.map((day) => {
        const existing = map.get(day);
        return {
          dayOfWeek: day,
          openTime: existing ? existing.openTime.slice(0, 5) : '06:00',
          closeTime: existing ? existing.closeTime.slice(0, 5) : '22:00',
          isClosed: existing ? existing.isClosed : false,
        };
      });
      setItems(initialList);

      const firstReason = schedule.find((s) => s.reason)?.reason || '';
      setOverrideReason(firstReason);
    }
  }, [schedule]);

  // Cập nhật 1 trường của 1 ngày
  const handleUpdateDay = (
    day: DayOfWeekNumber,
    patch: Partial<UpdateLotOperatingHourItemRequest>
  ) => {
    setItems((prev) =>
      prev.map((item) => (item.dayOfWeek === day ? { ...item, ...patch } : item))
    );
  };

  // Chuyển đổi trạng thái Đóng cửa / Mở cửa cho 1 ngày
  const handleToggleDayClosed = (day: DayOfWeekNumber) => {
    setItems((prev) =>
      prev.map((item) => {
        if (item.dayOfWeek === day) {
          const willBeClosed = !item.isClosed;
          if (willBeClosed) {
            return { ...item, isClosed: true };
          }

          // Khi MỞ CỬA LẠI:
          // Nếu giờ đang là 00:00 - 00:00 (hoặc chưa có giờ hợp lệ), đưa về giờ mở cửa tiêu chuẩn (06:00 - 22:00)
          // để tránh bị rơi vào trạng thái 24/7 và giúp người dùng có ngay khung giờ có thể điều chỉnh được.
          const isZeroTime =
            (item.openTime === '00:00' && (item.closeTime === '00:00' || item.closeTime === '23:59')) ||
            !item.openTime ||
            !item.closeTime;

          return {
            ...item,
            isClosed: false,
            openTime: isZeroTime ? '06:00' : item.openTime,
            closeTime: isZeroTime ? '22:00' : item.closeTime,
          };
        }
        return item;
      })
    );
  };

  // Áp dụng đặt 24/7 cho 1 ngày
  const handleToggle24_7ForDay = (day: DayOfWeekNumber) => {
    setItems((prev) =>
      prev.map((item) => {
        if (item.dayOfWeek === day) {
          const isCurrently24 =
            !item.isClosed &&
            item.openTime === '00:00' &&
            (item.closeTime === '00:00' || item.closeTime === '23:59');
          if (isCurrently24) {
            return { ...item, openTime: '06:00', closeTime: '22:00', isClosed: false };
          } else {
            return { ...item, openTime: '00:00', closeTime: '00:00', isClosed: false };
          }
        }
        return item;
      })
    );
  };

  // ==================== BỘ PRESET NHANH (QUICK ACTIONS) ====================

  const handlePreset24_7All = () => {
    setItems((prev) =>
      prev.map((item) => ({
        ...item,
        openTime: '00:00',
        closeTime: '00:00',
        isClosed: false,
      }))
    );
  };

  const handlePresetOfficeHours = () => {
    setItems((prev) =>
      prev.map((item) => {
        const isWeekend = item.dayOfWeek === 6 || item.dayOfWeek === 0;
        return {
          ...item,
          openTime: '07:00',
          closeTime: '18:00',
          isClosed: isWeekend,
        };
      })
    );
  };

  const handlePresetNightParking = () => {
    setItems((prev) =>
      prev.map((item) => ({
        ...item,
        openTime: '18:00',
        closeTime: '07:00',
        isClosed: false,
      }))
    );
  };

  const handleCopyMondayToAll = () => {
    const monday = items.find((i) => i.dayOfWeek === 1);
    if (!monday) return;
    setItems((prev) =>
      prev.map((item) => ({
        ...item,
        openTime: monday.openTime,
        closeTime: monday.closeTime,
        isClosed: monday.isClosed,
      }))
    );
  };

  // Khôi phục về dữ liệu từ server
  const handleReset = () => {
    if (schedule && schedule.length > 0) {
      const map = new Map<number, LotOperatingHourItemDto>();
      schedule.forEach((s) => map.set(getDayNumber(s), s));
      const resetList: UpdateLotOperatingHourItemRequest[] = WEEK_DAYS_ORDER.map((day) => {
        const existing = map.get(day);
        return {
          dayOfWeek: day,
          openTime: existing ? existing.openTime.slice(0, 5) : '06:00',
          closeTime: existing ? existing.closeTime.slice(0, 5) : '22:00',
          isClosed: existing ? existing.isClosed : false,
        };
      });
      setItems(resetList);
    }
    if (onRefresh) {
      void onRefresh();
    }
  };

  // Kiểm tra có thay đổi dữ liệu (Dirty state)
  const isDirty = useMemo(() => {
    if (!schedule || schedule.length === 0) return items.length > 0;
    const map = new Map<number, LotOperatingHourItemDto>();
    schedule.forEach((s) => map.set(getDayNumber(s), s));

    return items.some((item) => {
      const original = map.get(item.dayOfWeek);
      if (!original) return true;
      const origOpen = original.openTime.slice(0, 5);
      const origClose = original.closeTime.slice(0, 5);
      return (
        item.openTime !== origOpen ||
        item.closeTime !== origClose ||
        item.isClosed !== original.isClosed
      );
    });
  }, [items, schedule]);

  // ==================== ĐIỀU HƯỚNG TUẦN (WEEK NAVIGATION) ====================

  const weekRange = useMemo(
    () => getIsoWeekRange(selectedYear, selectedWeek),
    [selectedYear, selectedWeek]
  );

  const handlePrevWeek = () => {
    let nextWeek = selectedWeek - 1;
    let nextYear = selectedYear;
    if (nextWeek < 1) {
      nextYear -= 1;
      nextWeek = 52;
    }
    setSelectedYear(nextYear);
    setSelectedWeek(nextWeek);
    if (onSelectWeek) {
      void onSelectWeek(nextYear, nextWeek);
    }
  };

  const handleNextWeek = () => {
    let nextWeek = selectedWeek + 1;
    let nextYear = selectedYear;
    if (nextWeek > 52) {
      nextYear += 1;
      nextWeek = 1;
    }
    setSelectedYear(nextYear);
    setSelectedWeek(nextWeek);
    if (onSelectWeek) {
      void onSelectWeek(nextYear, nextWeek);
    }
  };

  const handleCurrentWeek = () => {
    setSelectedYear(nowWeek.year);
    setSelectedWeek(nowWeek.week);
    if (onSelectWeek) {
      void onSelectWeek(nowWeek.year, nowWeek.week);
    }
  };

  const handleSwitchMode = (mode: 'week' | 'base') => {
    setViewMode(mode);
    if (mode === 'base') {
      if (onSelectWeek) void onSelectWeek(undefined, undefined);
    } else {
      if (onSelectWeek) void onSelectWeek(selectedYear, selectedWeek);
    }
  };

  // Submit lưu lên server
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (viewMode === 'week') {
      await onSave(items, selectedYear, selectedWeek, overrideReason.trim() || undefined);
    } else {
      await onSave(items);
    }
  };

  // Xóa cấu hình riêng của tuần
  const handleDeleteOverride = async () => {
    if (onDeleteWeekOverride) {
      await onDeleteWeekOverride(selectedYear, selectedWeek);
    }
  };

  const isCurrentWeekSelected =
    selectedYear === nowWeek.year && selectedWeek === nowWeek.week;

  return (
    <div className="rounded-3xl bg-white border border-slate-200/90 p-5 sm:p-7 shadow-xs space-y-6">
      {/* 1. Header & Chuyển đổi Chế độ (Base vs Week) */}
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-5 border-b border-slate-100">
        <div>
          <div className="flex items-center gap-2">
            <span className="p-1.5 rounded-lg bg-indigo-50 text-indigo-600">
              <CalendarDays size={18} />
            </span>
            <h3 className="text-base font-bold text-slate-900">
              Lịch Hoạt Động & Khung Giờ Mở Cửa (US-062)
            </h3>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Cấu hình thời gian đón khách theo từng thứ. Bạn có thể thiết lập Lịch định kỳ chung
            hoặc Tùy biến riêng cho từng tuần cụ thể.
          </p>
        </div>

        {/* Nút chuyển chế độ: Lịch cơ sở chung vs Lịch theo tuần */}
        <div className="flex items-center p-1 bg-slate-100 rounded-xl border border-slate-200 shrink-0">
          <button
            type="button"
            onClick={() => handleSwitchMode('base')}
            className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer ${
              viewMode === 'base'
                ? 'bg-white text-indigo-700 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Layers size={13} />
            Lịch cơ sở chung
          </button>
          <button
            type="button"
            onClick={() => handleSwitchMode('week')}
            className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer ${
              viewMode === 'week'
                ? 'bg-white text-indigo-700 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Calendar size={13} />
            Lịch theo tuần
          </button>
        </div>
      </div>

      {/* 2. Thanh Điều Hướng Tuần (Khi ở chế độ 'week') */}
      {viewMode === 'week' ? (
        <div className="p-4 rounded-2xl bg-gradient-to-r from-indigo-50/60 via-purple-50/40 to-slate-50 border border-indigo-100 space-y-3">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            {/* Bộ chọn tuần qua lại */}
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handlePrevWeek}
                disabled={saving}
                className="p-2 rounded-xl bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 shadow-2xs transition-all cursor-pointer disabled:opacity-50"
                title="Tuần trước"
              >
                <ChevronLeft size={16} />
              </button>

              <div className="px-4 py-2 rounded-xl bg-white border border-slate-200 text-center shadow-2xs">
                <div className="text-xs font-extrabold text-slate-900 flex items-center gap-1.5 justify-center">
                  <span>Tuần {selectedWeek} / {selectedYear}</span>
                  {isCurrentWeekSelected && (
                    <span className="text-[10px] px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800 font-bold">
                      Hiện tại
                    </span>
                  )}
                </div>
                <div className="text-[11px] text-slate-500 font-medium">
                  {weekRange.startStr} ➔ {weekRange.endStr}
                </div>
              </div>

              <button
                type="button"
                onClick={handleNextWeek}
                disabled={saving}
                className="p-2 rounded-xl bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 shadow-2xs transition-all cursor-pointer disabled:opacity-50"
                title="Tuần sau"
              >
                <ChevronRight size={16} />
              </button>

              {!isCurrentWeekSelected && (
                <button
                  type="button"
                  onClick={handleCurrentWeek}
                  className="px-3 py-2 rounded-xl bg-white hover:bg-indigo-50 border border-slate-200 text-xs font-semibold text-slate-700 hover:text-indigo-700 transition-all cursor-pointer shadow-2xs"
                >
                  Về Tuần này
                </button>
              )}
            </div>

            {/* Trạng thái Lịch của Tuần */}
            <div className="flex items-center gap-2">
              {weeklyDto?.isCustomWeek ? (
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-xl bg-emerald-100 text-emerald-800 border border-emerald-200">
                    <CheckCircle2 size={13} />
                    Lịch ngoại lệ riêng
                  </span>
                  {onDeleteWeekOverride && (
                    <button
                      type="button"
                      onClick={handleDeleteOverride}
                      disabled={saving}
                      className="px-3 py-1.5 rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 text-xs font-bold transition-all flex items-center gap-1 cursor-pointer disabled:opacity-50"
                      title="Xóa cấu hình riêng và quay về dùng Lịch cơ sở"
                    >
                      <Trash2 size={12} />
                      Khôi phục lịch chung
                    </button>
                  )}
                </div>
              ) : (
                <span className="inline-flex items-center gap-1 text-xs font-semibold px-3 py-1.5 rounded-xl bg-amber-50 text-amber-800 border border-amber-200">
                  <Info size={13} />
                  Đang kế thừa từ Lịch cơ sở
                </span>
              )}
            </div>
          </div>

          {/* Ô nhập lý do ngoại lệ tuần */}
          <div className="flex flex-col sm:flex-row sm:items-center gap-2 pt-2 border-t border-indigo-100/60">
            <span className="text-xs font-semibold text-slate-600 shrink-0">
              Lý do ngoại lệ (tùy chọn):
            </span>
            <input
              type="text"
              placeholder="VD: Nghỉ lễ Tết Nguyên Đán, Bảo trì sơn sàn tầng 1, Mở đêm đón sự kiện..."
              value={overrideReason}
              onChange={(e) => setOverrideReason(e.target.value)}
              className="flex-1 px-3 py-1.5 rounded-xl border border-slate-200 bg-white text-xs text-slate-800 focus:outline-hidden focus:border-indigo-600 shadow-2xs"
            />
          </div>
        </div>
      ) : (
        <div className="p-3.5 rounded-2xl bg-amber-50/80 border border-amber-200 text-xs text-amber-900 flex items-start gap-2.5">
          <AlertCircle size={16} className="text-amber-600 shrink-0 mt-0.5" />
          <div className="leading-relaxed">
            <strong>Chế độ Lịch định kỳ cơ sở:</strong> Bất kỳ thay đổi nào tại đây sẽ được áp dụng tự động
            cho tất cả các tuần trong năm (trừ những tuần bạn đã cấu hình lịch ngoại lệ riêng).
          </div>
        </div>
      )}

      {/* 3. Thanh bộ mẫu áp dụng nhanh (Quick Presets) */}
      <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 space-y-2">
        <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
          <Sparkles size={13} className="text-indigo-600" />
          <span>Mẫu áp dụng nhanh:</span>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            onClick={handlePreset24_7All}
            className="px-3 py-1.5 rounded-xl bg-white hover:bg-indigo-50 border border-slate-200 hover:border-indigo-200 text-slate-700 hover:text-indigo-700 text-xs font-semibold shadow-2xs transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Sun size={13} className="text-amber-500" />
            <span>Mở cửa 24/7 cả tuần</span>
          </button>

          <button
            type="button"
            onClick={handlePresetOfficeHours}
            className="px-3 py-1.5 rounded-xl bg-white hover:bg-indigo-50 border border-slate-200 hover:border-indigo-200 text-slate-700 hover:text-indigo-700 text-xs font-semibold shadow-2xs transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Clock size={13} className="text-blue-500" />
            <span>Giờ hành chính (07:00 - 18:00)</span>
          </button>

          <button
            type="button"
            onClick={handlePresetNightParking}
            className="px-3 py-1.5 rounded-xl bg-white hover:bg-indigo-50 border border-slate-200 hover:border-indigo-200 text-slate-700 hover:text-indigo-700 text-xs font-semibold shadow-2xs transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Moon size={13} className="text-purple-500" />
            <span>Bãi đỗ xe đêm (18:00 - 07:00)</span>
          </button>

          <button
            type="button"
            onClick={handleCopyMondayToAll}
            className="px-3 py-1.5 rounded-xl bg-white hover:bg-indigo-50 border border-slate-200 hover:border-indigo-200 text-slate-700 hover:text-indigo-700 text-xs font-semibold shadow-2xs transition-all flex items-center gap-1.5 cursor-pointer ml-auto"
          >
            <Copy size={13} className="text-slate-500" />
            <span>Sao chép Thứ Hai cho cả tuần</span>
          </button>
        </div>
      </div>

      {/* 4. Bảng danh sách 7 ngày */}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2.5">
          {WEEK_DAYS_ORDER.map((day) => {
            const item = items.find((i) => i.dayOfWeek === day) || {
              dayOfWeek: day,
              openTime: '06:00',
              closeTime: '22:00',
              isClosed: false,
            };

            const is24Hours =
              !item.isClosed &&
              item.openTime === '00:00' &&
              (item.closeTime === '00:00' || item.closeTime === '23:59');

            const isOvernight =
              !item.isClosed && !is24Hours && item.openTime > item.closeTime;

            const isWeekend = day === 6 || day === 0;

            return (
              <div
                key={day}
                className={`p-3.5 sm:p-4 rounded-2xl border transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-3 ${
                  item.isClosed
                    ? 'bg-slate-50/70 border-slate-200/60 opacity-80'
                    : isOvernight
                      ? 'bg-purple-50/40 border-purple-200/70'
                      : is24Hours
                        ? 'bg-emerald-50/40 border-emerald-200/70'
                        : 'bg-white border-slate-200 hover:border-indigo-200'
                }`}
              >
                {/* Tên thứ & Badge trạng thái */}
                <div className="flex items-center gap-3 sm:w-48 shrink-0">
                  <div
                    className={`w-9 h-9 rounded-xl flex items-center justify-center font-bold text-xs shrink-0 ${
                      item.isClosed
                        ? 'bg-slate-200 text-slate-600'
                        : isWeekend
                          ? 'bg-amber-100 text-amber-800'
                          : 'bg-indigo-100 text-indigo-800'
                    }`}
                  >
                    {day === 0 ? 'CN' : `T${day + 1}`}
                  </div>
                  <div>
                    <h4 className="text-xs sm:text-sm font-bold text-slate-900">
                      {DAY_OF_WEEK_NAMES[day]}
                    </h4>
                    <div className="mt-0.5">
                      {item.isClosed ? (
                        <span className="inline-flex items-center text-[10px] font-bold px-2 py-0.5 rounded-full bg-rose-50 text-rose-700 border border-rose-200">
                          Đóng cửa cả ngày
                        </span>
                      ) : is24Hours ? (
                        <span className="inline-flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800">
                          <CheckCircle2 size={10} />
                          Mở cửa 24/7
                        </span>
                      ) : isOvernight ? (
                        <span className="inline-flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-purple-100 text-purple-800">
                          <Moon size={10} />
                          Qua đêm
                        </span>
                      ) : (
                        <span className="text-[10px] text-slate-400 font-medium">
                          Trong ngày
                        </span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Các bộ chọn thời gian & chuyển đổi */}
                <div className="flex items-center gap-2.5 flex-wrap sm:flex-nowrap">
                  {/* Ô chọn Giờ mở cửa */}
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs font-semibold text-slate-500 whitespace-nowrap">
                      Mở:
                    </span>
                    <input
                      type="time"
                      disabled={item.isClosed}
                      value={item.openTime}
                      onChange={(e) =>
                        handleUpdateDay(day, { openTime: e.target.value })
                      }
                      className="px-2.5 py-1.5 rounded-xl border border-slate-200 bg-white text-xs font-bold text-slate-800 focus:outline-hidden focus:border-indigo-600 disabled:bg-slate-100 disabled:text-slate-400 shadow-2xs"
                    />
                  </div>

                  <span className="text-slate-400 text-xs">→</span>

                  {/* Ô chọn Giờ đóng cửa */}
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs font-semibold text-slate-500 whitespace-nowrap">
                      Đóng:
                    </span>
                    <input
                      type="time"
                      disabled={item.isClosed}
                      value={item.closeTime}
                      onChange={(e) =>
                        handleUpdateDay(day, { closeTime: e.target.value })
                      }
                      className="px-2.5 py-1.5 rounded-xl border border-slate-200 bg-white text-xs font-bold text-slate-800 focus:outline-hidden focus:border-indigo-600 disabled:bg-slate-100 disabled:text-slate-400 shadow-2xs"
                    />
                  </div>

                  {/* Nút bật nhanh 24/7 cho ngày */}
                  <button
                    type="button"
                    disabled={item.isClosed}
                    onClick={() => handleToggle24_7ForDay(day)}
                    className={`px-2.5 py-1.5 rounded-xl text-xs font-bold transition-all border cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed ${
                      is24Hours
                        ? 'bg-emerald-600 text-white border-emerald-600 shadow-xs'
                        : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                    }`}
                  >
                    24/7
                  </button>

                  {/* Nút chuyển đổi Đóng cửa / Mở cửa */}
                  <button
                    type="button"
                    onClick={() => handleToggleDayClosed(day)}
                    title={
                      item.isClosed
                        ? 'Bấm để mở cửa lại ngày này'
                        : 'Bấm để tạm đóng cửa ngày này'
                    }
                    className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all border cursor-pointer ${
                      item.isClosed
                        ? 'bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100'
                        : 'bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100'
                    }`}
                  >
                    {item.isClosed ? 'Đang đóng' : 'Đang mở'}
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        {/* Chú thích thông minh */}
        <div className="p-3 rounded-xl bg-blue-50/70 border border-blue-200/60 flex items-start gap-2 text-xs text-blue-900 leading-relaxed">
          <Info size={15} className="text-blue-600 shrink-0 mt-0.5" />
          <span>
            <strong>Gợi ý:</strong> Khi giờ mở lớn hơn giờ đóng (ví dụ: 18:00 đến 06:00), hệ thống tự động
            bật chế độ <em>Hoạt động qua đêm</em>, cho phép tài xế đặt chỗ và check-in xuyên ngày an toàn.
          </span>
        </div>

        {/* Nút hành động */}
        <div className="flex items-center justify-between gap-3 pt-4 border-t border-slate-100">
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
            disabled={!isDirty || saving}
            className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 cursor-pointer"
          >
            <Save size={15} />
            <span>
              {saving
                ? 'Đang lưu...'
                : viewMode === 'week'
                  ? `Lưu lịch Tuần ${selectedWeek}/${selectedYear}`
                  : 'Lưu Lịch định kỳ chung'}
            </span>
          </button>
        </div>
      </form>
    </div>
  );
};
