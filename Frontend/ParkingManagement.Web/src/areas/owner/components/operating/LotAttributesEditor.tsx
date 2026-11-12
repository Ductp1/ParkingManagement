import React, { useState, useEffect, useMemo } from 'react';
import {
  Phone,
  ArrowUpDown,
  FileText,
  Save,
  RotateCcw,
  SlidersHorizontal,
  Info,
} from 'lucide-react';
import type {
  LotAttributesDto,
  UpdateLotAttributesRequest,
} from '@/types/ownerLotDetails';

interface LotAttributesEditorProps {
  attributes?: LotAttributesDto | null;
  defaultHeight?: number;
  saving: boolean;
  onSave: (req: UpdateLotAttributesRequest) => Promise<void>;
  onRefresh?: () => void | Promise<void>;
}

export const LotAttributesEditor: React.FC<LotAttributesEditorProps> = ({
  attributes,
  defaultHeight = 220,
  saving,
  onSave,
  onRefresh,
}) => {
  const [description, setDescription] = useState<string>('');
  const [hotlinePhone, setHotlinePhone] = useState<string>('');
  const [maxHeightCm, setMaxHeightCm] = useState<number>(defaultHeight);

  // Đồng bộ khi attributes thay đổi
  useEffect(() => {
    if (attributes) {
      setDescription(attributes.description || '');
      setHotlinePhone(attributes.hotlinePhone || '');
      setMaxHeightCm(attributes.maxHeightCm || defaultHeight);
    } else {
      setMaxHeightCm(defaultHeight);
    }
  }, [attributes, defaultHeight]);

  const isDirty = useMemo(() => {
    const origDesc = attributes?.description || '';
    const origPhone = attributes?.hotlinePhone || '';
    const origHeight = attributes?.maxHeightCm || defaultHeight;
    return (
      description !== origDesc ||
      hotlinePhone !== origPhone ||
      maxHeightCm !== origHeight
    );
  }, [description, hotlinePhone, maxHeightCm, attributes, defaultHeight]);

  const handleReset = () => {
    if (attributes) {
      setDescription(attributes.description || '');
      setHotlinePhone(attributes.hotlinePhone || '');
      setMaxHeightCm(attributes.maxHeightCm || defaultHeight);
    } else {
      setDescription('');
      setHotlinePhone('');
      setMaxHeightCm(defaultHeight);
    }
    if (onRefresh) {
      void onRefresh();
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSave({
      description: description.trim() || null,
      hotlinePhone: hotlinePhone.trim() || null,
      maxHeightCm: maxHeightCm > 0 ? maxHeightCm : null,
    });
  };

  return (
    <div className="rounded-3xl bg-white border border-slate-200/90 p-5 sm:p-7 shadow-xs space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-5 border-b border-slate-100">
        <div>
          <div className="flex items-center gap-2">
            <span className="p-1.5 rounded-lg bg-blue-50 text-blue-600">
              <SlidersHorizontal size={18} />
            </span>
            <h3 className="text-base font-bold text-slate-900">
              Thông Tin Vận Hành & Hỗ Trợ Bãi Đỗ (US-064)
            </h3>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Thiết lập số điện thoại trực cổng, giới hạn chiều cao xe và hướng dẫn chi tiết
            để tài xế dễ dàng nhận diện khi tới bãi.
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-5">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
          {/* 1. Hotline trực bãi */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
              <Phone size={14} className="text-blue-600" />
              <span>Số điện thoại Hotline cứu hộ / Quản lý:</span>
            </label>
            <input
              type="tel"
              placeholder="Ví dụ: 0901 234 567"
              value={hotlinePhone}
              onChange={(e) => setHotlinePhone(e.target.value)}
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs font-bold text-slate-900 focus:outline-hidden focus:border-blue-600 shadow-2xs"
            />
            <p className="text-[11px] text-slate-400">
              Hiển thị nút "Gọi cứu hộ / Hotline" trên app của tài xế khi gặp sự cố tại bãi.
            </p>
          </div>

          {/* 2. Chiều cao giới hạn (cm) */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
              <ArrowUpDown size={14} className="text-purple-600" />
              <span>Chiều cao xe tối đa (Max Height):</span>
            </label>
            <div className="relative">
              <input
                type="number"
                min={150}
                max={500}
                step={5}
                value={maxHeightCm}
                onChange={(e) => setMaxHeightCm(parseInt(e.target.value, 10) || 0)}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs font-bold text-slate-900 focus:outline-hidden focus:border-blue-600 shadow-2xs pr-12"
              />
              <span className="absolute right-3.5 top-2.5 text-xs font-semibold text-slate-400">
                cm
              </span>
            </div>
            <p className="text-[11px] text-slate-400">
              Tương đương: <strong>{(maxHeightCm / 100).toFixed(2)} mét</strong>. Giúp hệ
              thống tự động cảnh báo các xe quá khổ/xe van không vào được bãi.
            </p>
          </div>
        </div>

        {/* 3. Mô tả / Giới thiệu bãi xe */}
        <div className="space-y-1.5">
          <label className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
            <FileText size={14} className="text-slate-600" />
            <span>Mô tả & Hướng dẫn ra vào bãi xe:</span>
          </label>
          <textarea
            rows={3}
            placeholder="Ví dụ: Lối vào nằm tại đường Hải Triều, có thanh chắn barie tự động nhận diện biển số. Quý khách vui lòng đi chậm dưới 10km/h..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-blue-600 shadow-2xs leading-relaxed"
          />
        </div>

        {/* Chú thích */}
        <div className="p-3 rounded-xl bg-slate-50 border border-slate-200/70 flex items-center gap-2 text-xs text-slate-600">
          <Info size={15} className="text-slate-400 shrink-0" />
          <span>
            Thông tin này được đồng bộ tức thời và hiển thị trong trang <em>Chi tiết bãi đỗ</em>{' '}
            trên ứng dụng dành cho khách hàng.
          </span>
        </div>

        {/* Nút lưu */}
        <div className="flex items-center justify-between gap-3 pt-3 border-t border-slate-100">
          <button
            type="button"
            onClick={handleReset}
            disabled={!isDirty || saving}
            className="px-4 py-2 rounded-xl border border-slate-300 text-slate-700 hover:bg-slate-50 text-xs font-bold transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5 cursor-pointer"
          >
            <RotateCcw size={14} />
            <span>Khôi phục</span>
          </button>

          <button
            type="submit"
            disabled={!isDirty || saving}
            className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-md shadow-blue-600/20 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 cursor-pointer"
          >
            <Save size={15} />
            <span>{saving ? 'Đang lưu...' : 'Lưu thông tin bổ trợ'}</span>
          </button>
        </div>
      </form>
    </div>
  );
};
