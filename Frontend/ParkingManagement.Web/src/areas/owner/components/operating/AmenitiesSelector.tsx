import React, { useState, useEffect, useMemo } from 'react';
import {
  Sparkles,
  Save,
  RotateCcw,
  CheckCircle2,
  Warehouse,
  Video,
  ShieldCheck,
  Zap,
  Truck,
  Accessibility,
  KeyRound,
  Bus,
  DoorClosed,
  ArrowUpDown,
  Gauge,
  HelpCircle,
  Edit3,
} from 'lucide-react';
import type {
  LotAmenityDto,
  LotAmenityInputDto,
  AmenityCatalogItemDto,
  AmenityMeta,
} from '@/types/ownerLotDetails';
import { STANDARD_AMENITIES_CATALOG } from '@/types/ownerLotDetails';

interface AmenitiesSelectorProps {
  activeAmenities: LotAmenityDto[];
  catalog?: AmenityCatalogItemDto[];
  saving: boolean;
  onSave: (amenities: LotAmenityInputDto[]) => Promise<void>;
  onRefresh?: () => void | Promise<void>;
}

// Map tên icon với React Component icon tương ứng
const ICON_COMPONENT_MAP: Record<string, React.FC<{ size?: number; className?: string }>> = {
  Warehouse,
  Video,
  ShieldCheck,
  Zap,
  Sparkles,
  Truck,
  Accessibility,
  KeyRound,
  Bus,
  DoorClosed,
  ArrowUpDown,
  Gauge,
};

export const AmenitiesSelector: React.FC<AmenitiesSelectorProps> = ({
  activeAmenities,
  catalog,
  saving,
  onSave,
  onRefresh,
}) => {
  // Tập hợp mã tiện ích đang được chọn
  const [selectedCodes, setSelectedCodes] = useState<Set<string>>(new Set());
  // Ghi chú chi tiết cho từng tiện ích
  const [notes, setNotes] = useState<Record<string, string>>({});
  // Tiện ích đang mở ô nhập ghi chú
  const [editingNoteCode, setEditingNoteCode] = useState<string | null>(null);

  // Đồng bộ khi prop activeAmenities thay đổi
  useEffect(() => {
    const codes = new Set<string>();
    const noteMap: Record<string, string> = {};
    activeAmenities.forEach((a) => {
      codes.add(a.code.toUpperCase());
      if (a.note) {
        noteMap[a.code.toUpperCase()] = a.note;
      }
    });
    setSelectedCodes(codes);
    setNotes(noteMap);
  }, [activeAmenities]);

  // Danh mục tiện ích chuẩn kết hợp với catalog từ server (nếu có thêm)
  const fullCatalog: AmenityMeta[] = useMemo(() => {
    if (!catalog || catalog.length === 0) return STANDARD_AMENITIES_CATALOG;

    // Hợp nhất danh mục từ server với metadata icons
    const map = new Map<string, AmenityMeta>();
    STANDARD_AMENITIES_CATALOG.forEach((m) => map.set(m.code.toUpperCase(), m));

    catalog.forEach((cat) => {
      const upper = cat.code.toUpperCase();
      if (!map.has(upper)) {
        map.set(upper, {
          code: upper,
          name: cat.name,
          description: cat.description,
          iconName: 'Sparkles',
          badgeClass: 'bg-indigo-50 text-indigo-700 border-indigo-200',
          iconBgClass: 'bg-indigo-100',
          iconColorClass: 'text-indigo-600',
        });
      }
    });
    return Array.from(map.values());
  }, [catalog]);

  // Toggle chọn / bỏ chọn tiện ích
  const handleToggleCode = (code: string) => {
    const upper = code.toUpperCase();
    setSelectedCodes((prev) => {
      const next = new Set(prev);
      if (next.has(upper)) {
        next.delete(upper);
        if (editingNoteCode === upper) {
          setEditingNoteCode(null);
        }
      } else {
        next.add(upper);
      }
      return next;
    });
  };

  // Cập nhật ghi chú của 1 tiện ích
  const handleUpdateNote = (code: string, noteValue: string) => {
    const upper = code.toUpperCase();
    setNotes((prev) => ({
      ...prev,
      [upper]: noteValue,
    }));
  };

  // Áp dụng chọn nhanh 4 tiện ích cơ bản
  const handleSelectBasicAmenities = () => {
    const basicCodes = ['COVERED', 'CCTV', 'SECURITY_24H', 'TOILET'];
    setSelectedCodes((prev) => {
      const next = new Set(prev);
      basicCodes.forEach((c) => next.add(c));
      return next;
    });
  };

  // Bỏ chọn tất cả
  const handleDeselectAll = () => {
    setSelectedCodes(new Set());
    setEditingNoteCode(null);
  };

  // Khôi phục về dữ liệu gốc
  const handleReset = () => {
    const codes = new Set<string>();
    const noteMap: Record<string, string> = {};
    activeAmenities.forEach((a) => {
      codes.add(a.code.toUpperCase());
      if (a.note) {
        noteMap[a.code.toUpperCase()] = a.note;
      }
    });
    setSelectedCodes(codes);
    setNotes(noteMap);
    setEditingNoteCode(null);
    if (onRefresh) {
      void onRefresh();
    }
  };

  // Kiểm tra xem có sự thay đổi so với dữ liệu gốc không
  const isDirty = useMemo(() => {
    const origSet = new Set(activeAmenities.map((a) => a.code.toUpperCase()));
    if (selectedCodes.size !== origSet.size) return true;
    for (const code of selectedCodes) {
      if (!origSet.has(code)) return true;
    }

    // So sánh notes
    for (const a of activeAmenities) {
      const currentNote = (notes[a.code.toUpperCase()] || '').trim();
      const origNote = (a.note || '').trim();
      if (currentNote !== origNote) return true;
    }
    return false;
  }, [selectedCodes, notes, activeAmenities]);

  // Submit lên server
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload: LotAmenityInputDto[] = Array.from(selectedCodes).map((code) => {
      const meta = fullCatalog.find((m) => m.code === code);
      return {
        code,
        name: meta?.name || code,
        note: notes[code]?.trim() || null,
      };
    });
    await onSave(payload);
  };

  return (
    <div className="rounded-3xl bg-white border border-slate-200/90 p-5 sm:p-7 shadow-xs space-y-6">
      {/* Header mục Tiện ích */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-5 border-b border-slate-100">
        <div>
          <div className="flex items-center gap-2">
            <span className="p-1.5 rounded-lg bg-emerald-50 text-emerald-600">
              <Sparkles size={18} />
            </span>
            <h3 className="text-base font-bold text-slate-900">
              Tiện Ích & Dịch Vụ Gia Tăng Bãi Đỗ (US-064)
            </h3>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Chọn các tiện ích thực tế bãi xe có trang bị. Thông tin sẽ hiển thị công khai trên ứng dụng
            để thu hút tài xế và tăng tỷ lệ đặt chỗ.
          </p>
        </div>

        {/* Badge thống kê tiện ích */}
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs px-3 py-1 rounded-full bg-emerald-50 text-emerald-800 font-bold border border-emerald-200 flex items-center gap-1.5">
            <CheckCircle2 size={13} className="text-emerald-600" />
            Đã kích hoạt {selectedCodes.size} / {fullCatalog.length} tiện ích
          </span>
        </div>
      </div>

      {/* Nút thao tác nhanh */}
      <div className="flex items-center justify-between gap-2 flex-wrap text-xs">
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            onClick={handleSelectBasicAmenities}
            className="px-3 py-1.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold transition-colors cursor-pointer"
          >
            + Chọn 4 tiện ích cơ bản (Mái che, Camera, Bảo vệ, WC)
          </button>
          <button
            type="button"
            onClick={handleDeselectAll}
            className="px-3 py-1.5 rounded-xl border border-slate-200 text-slate-500 hover:text-slate-800 hover:bg-slate-50 transition-colors cursor-pointer"
          >
            Bỏ chọn tất cả
          </button>
        </div>
      </div>

      {/* Grid 12 Tiện ích chuẩn */}
      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
          {fullCatalog.map((amenity) => {
            const isSelected = selectedCodes.has(amenity.code);
            const IconComp = ICON_COMPONENT_MAP[amenity.iconName] || HelpCircle;
            const currentNote = notes[amenity.code] || '';
            const isEditingNote = editingNoteCode === amenity.code;

            return (
              <div
                key={amenity.code}
                className={`p-4 rounded-2xl border transition-all cursor-pointer flex flex-col justify-between ${
                  isSelected
                    ? 'bg-emerald-50/30 border-emerald-300 ring-2 ring-emerald-500/10 shadow-xs'
                    : 'bg-white border-slate-200/80 hover:border-slate-300 hover:bg-slate-50/50'
                }`}
                onClick={() => handleToggleCode(amenity.code)}
              >
                <div>
                  <div className="flex items-start justify-between gap-2.5">
                    {/* Icon & Tên */}
                    <div className="flex items-start gap-3">
                      <div
                        className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${
                          isSelected
                            ? 'bg-emerald-600 text-white shadow-xs'
                            : `${amenity.iconBgClass} ${amenity.iconColorClass}`
                        }`}
                      >
                        <IconComp size={20} />
                      </div>
                      <div>
                        <h4 className="text-xs sm:text-sm font-bold text-slate-900 leading-snug">
                          {amenity.name}
                        </h4>
                        <p className="text-[11px] text-slate-500 mt-0.5 leading-relaxed">
                          {amenity.description}
                        </p>
                      </div>
                    </div>

                    {/* Checkbox */}
                    <div
                      className={`w-5 h-5 rounded-lg border flex items-center justify-center shrink-0 transition-all ${
                        isSelected
                          ? 'bg-emerald-600 border-emerald-600 text-white'
                          : 'border-slate-300 bg-white'
                      }`}
                    >
                      {isSelected && <CheckCircle2 size={13} className="text-white" />}
                    </div>
                  </div>

                  {/* Hiển thị ghi chú nếu đã có */}
                  {isSelected && currentNote && !isEditingNote && (
                    <div className="mt-3 p-2 rounded-xl bg-white border border-emerald-200/70 text-[11px] text-slate-700 flex items-start justify-between gap-1.5">
                      <span className="italic">"{currentNote}"</span>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          setEditingNoteCode(amenity.code);
                        }}
                        className="text-emerald-700 hover:text-emerald-900 shrink-0 p-0.5"
                      >
                        <Edit3 size={12} />
                      </button>
                    </div>
                  )}
                </div>

                {/* Khu vực nhập ghi chú chi tiết khi đã kích hoạt tiện ích */}
                {isSelected && (
                  <div
                    className="mt-3 pt-3 border-t border-emerald-200/50"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {isEditingNote || !currentNote ? (
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between text-[11px] font-semibold text-emerald-900">
                          <span>Ghi chú chi tiết (tùy chọn):</span>
                          {currentNote && (
                            <button
                              type="button"
                              onClick={() => setEditingNoteCode(null)}
                              className="text-slate-400 hover:text-slate-600 text-[10px]"
                            >
                              Đóng
                            </button>
                          )}
                        </div>
                        <input
                          type="text"
                          placeholder="Ví dụ: Có 4 trụ sạc AC 22kW..."
                          value={currentNote}
                          onChange={(e) => handleUpdateNote(amenity.code, e.target.value)}
                          className="w-full px-3 py-1.5 rounded-xl border border-emerald-300 bg-white text-xs text-slate-800 placeholder-slate-400 focus:outline-hidden focus:border-emerald-600 shadow-2xs"
                        />
                      </div>
                    ) : null}
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {/* Nút lưu */}
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
            className="px-6 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-md shadow-emerald-600/20 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 cursor-pointer"
          >
            <Save size={15} />
            <span>{saving ? 'Đang lưu tiện ích...' : 'Lưu danh sách tiện ích'}</span>
          </button>
        </div>
      </form>
    </div>
  );
};
