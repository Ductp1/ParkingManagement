import React, { useState, useEffect } from 'react';
import { X, Layers, AlertTriangle, Grid, Ruler, Weight } from 'lucide-react';
import type { FloorDto, CreateFloorRequest, UpdateFloorRequest } from '@/types/ownerStructure';

interface FloorModalProps {
  isOpen: boolean;
  onClose: () => void;
  zoneId: number;
  zoneName: string;
  initialData?: FloorDto | null;
  onSubmit: (data: CreateFloorRequest | UpdateFloorRequest) => Promise<void>;
}

export const FloorModal: React.FC<FloorModalProps> = ({
  isOpen,
  onClose,
  zoneName,
  initialData,
  onSubmit,
}) => {
  const isEdit = !!initialData;

  const [name, setName] = useState('');
  const [level, setLevel] = useState(1);
  const [maxHeightCm, setMaxHeightCm] = useState<number | ''>(220);
  const [maxWeightKg, setMaxWeightKg] = useState<number | ''>(2500);
  const [gridColumns, setGridColumns] = useState(10);
  const [gridRows, setGridRows] = useState(6);
  const [isClosed, setIsClosed] = useState(false);

  const [busy, setBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setLevel(initialData.level);
      setMaxHeightCm(initialData.maxHeightCm ?? '');
      setMaxWeightKg(initialData.maxWeightKg ?? '');
      setGridColumns(initialData.gridColumns);
      setGridRows(initialData.gridRows);
      setIsClosed(initialData.isClosed);
    } else {
      setName('');
      setLevel(1);
      setMaxHeightCm(220);
      setMaxWeightKg(2500);
      setGridColumns(10);
      setGridRows(6);
      setIsClosed(false);
    }
    setErrorMessage('');
  }, [initialData, isOpen]);

  if (!isOpen) return null;

  // Lựa chọn nhanh cao độ
  const QUICK_LEVELS = [
    { label: 'Hầm B3', value: -3 },
    { label: 'Hầm B2', value: -2 },
    { label: 'Hầm B1', value: -1 },
    { label: 'Trệt G', value: 0 },
    { label: 'Tầng 1', value: 1 },
    { label: 'Tầng 2', value: 2 },
    { label: 'Tầng 3', value: 3 },
  ];

  const handleSelectQuickLevel = (val: number, label: string) => {
    setLevel(val);
    if (!name || QUICK_LEVELS.some((q) => q.label === name)) {
      setName(label);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    const trimmedName = name.trim();
    if (!trimmedName) {
      setErrorMessage('Vui lòng nhập Tên tầng.');
      return;
    }
    if (gridColumns < 1 || gridRows < 1) {
      setErrorMessage('Số cột và số hàng của lưới sơ đồ phải tối thiểu là 1.');
      return;
    }

    setBusy(true);
    try {
      if (isEdit) {
        const payload: UpdateFloorRequest = {
          name: trimmedName,
          maxHeightCm: maxHeightCm === '' ? null : Number(maxHeightCm),
          maxWeightKg: maxWeightKg === '' ? null : Number(maxWeightKg),
          gridColumns: Number(gridColumns),
          gridRows: Number(gridRows),
          isClosed,
        };
        await onSubmit(payload);
      } else {
        const payload: CreateFloorRequest = {
          name: trimmedName,
          level: Number(level),
          maxHeightCm: maxHeightCm === '' ? null : Number(maxHeightCm),
          maxWeightKg: maxWeightKg === '' ? null : Number(maxWeightKg),
          gridColumns: Number(gridColumns),
          gridRows: Number(gridRows),
        };
        await onSubmit(payload);
      }
      onClose();
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Có lỗi xảy ra khi lưu tầng. Vui lòng thử lại.');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="relative w-full max-w-xl rounded-3xl bg-white shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-slate-100 bg-slate-50/50">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-indigo-100 text-indigo-700 flex items-center justify-center">
              <Layers size={18} />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900">
                {isEdit ? 'Chỉnh sửa Thông số Tầng' : 'Khai báo Tầng đỗ xe mới'}
              </h2>
              <p className="text-xs text-slate-500">
                Trực thuộc khu vực: <strong className="text-slate-800">{zoneName}</strong>
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="p-1.5 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors cursor-pointer"
          >
            <X size={18} />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 max-h-[80vh] overflow-y-auto">
          {errorMessage && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-xs font-semibold text-rose-800 flex items-start gap-2">
              <AlertTriangle size={16} className="text-rose-600 shrink-0 mt-0.5" />
              <span>{errorMessage}</span>
            </div>
          )}

          {/* Chọn nhanh Cao độ (Level) */}
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1.5">
              Chọn nhanh cao độ tầng (Level)
            </label>
            <div className="flex items-center gap-1.5 flex-wrap">
              {QUICK_LEVELS.map((q) => (
                <button
                  key={q.value}
                  type="button"
                  onClick={() => handleSelectQuickLevel(q.value, q.label)}
                  className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer border ${
                    level === q.value
                      ? 'bg-indigo-600 text-white border-indigo-600 shadow-xs'
                      : 'bg-slate-50 hover:bg-slate-100 text-slate-700 border-slate-200'
                  }`}
                >
                  {q.label}
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {/* Tên tầng */}
            <div className="sm:col-span-2">
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Tên Tầng <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="VD: Hầm B1 hoặc Tầng 2 Khu A"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-indigo-600"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Duy nhất trong cùng khu vực</span>
            </div>

            {/* Cao độ Level (Tùy chỉnh) */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Cao độ số (Level) <span className="text-rose-500">*</span>
              </label>
              <input
                type="number"
                required
                value={level}
                onChange={(e) => setLevel(parseInt(e.target.value, 10) || 0)}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs font-mono font-bold text-slate-900 focus:outline-hidden focus:border-indigo-600"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Âm: Hầm, Dương: Nổi</span>
            </div>
          </div>

          {/* Quy chuẩn kỹ thuật: Chiều cao trần & Tải trọng */}
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/90 space-y-3">
            <span className="text-xs font-bold text-slate-800 uppercase tracking-wider block">
              Quy chuẩn kỹ thuật bãi đỗ
            </span>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Ruler size={13} className="text-indigo-600" />
                  Chiều cao trần tối đa (cm)
                </label>
                <input
                  type="number"
                  min={150}
                  max={500}
                  value={maxHeightCm}
                  onChange={(e) => setMaxHeightCm(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
                  placeholder="VD: 220 (2.2 mét)"
                  className="w-full px-3.5 py-2 rounded-xl bg-white border border-slate-200 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-indigo-600"
                />
                <span className="text-[10px] text-slate-400 mt-1 block">Để trống nếu ngoài trời</span>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Weight size={13} className="text-indigo-600" />
                  Tải trọng sàn tối đa (kg)
                </label>
                <input
                  type="number"
                  min={1000}
                  max={20000}
                  step={100}
                  value={maxWeightKg}
                  onChange={(e) => setMaxWeightKg(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
                  placeholder="VD: 2500 (2.5 tấn)"
                  className="w-full px-3.5 py-2 rounded-xl bg-white border border-slate-200 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-indigo-600"
                />
                <span className="text-[10px] text-slate-400 mt-1 block">Giới hạn tải trọng phương tiện</span>
              </div>
            </div>
          </div>

          {/* Ma trận lưới sơ đồ ô đỗ (Grid Matrix) */}
          <div className="p-4 rounded-2xl bg-indigo-50/50 border border-indigo-100 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-indigo-900 uppercase tracking-wider flex items-center gap-1.5">
                <Grid size={14} className="text-indigo-600" />
                Ma trận kích thước lưới sơ đồ ô đỗ
              </span>
              <span className="text-xs font-mono font-bold text-indigo-700 bg-white px-2 py-0.5 rounded-lg border border-indigo-200">
                {gridColumns * gridRows} ô ma trận
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Số Cột lưới (Columns) <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  min={1}
                  max={50}
                  required
                  value={gridColumns}
                  onChange={(e) => setGridColumns(parseInt(e.target.value, 10) || 1)}
                  className="w-full px-3.5 py-2 rounded-xl bg-white border border-slate-200 text-xs font-mono font-bold text-slate-900 focus:outline-hidden focus:border-indigo-600"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Số Hàng lưới (Rows) <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  min={1}
                  max={50}
                  required
                  value={gridRows}
                  onChange={(e) => setGridRows(parseInt(e.target.value, 10) || 1)}
                  className="w-full px-3.5 py-2 rounded-xl bg-white border border-slate-200 text-xs font-mono font-bold text-slate-900 focus:outline-hidden focus:border-indigo-600"
                />
              </div>
            </div>
            <p className="text-[11px] text-slate-500">
              Kích thước ma trận lưới tọa độ (X × Y) được dùng làm khung nền để xếp các ô đỗ xe trên sơ đồ 2D.
            </p>
          </div>

          {/* Chế độ sửa: Tạm đóng tầng */}
          {isEdit && (
            <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex items-center justify-between">
              <div>
                <span className="text-xs font-bold text-slate-800">Tạm đóng tầng này</span>
                <p className="text-[11px] text-slate-500">Tạm dừng nhận khách gửi xe lên tầng này</p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={isClosed}
                  onChange={(e) => setIsClosed(e.target.checked)}
                  className="sr-only peer"
                />
                <div className="w-11 h-6 bg-slate-200 peer-focus:outline-hidden rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-rose-500"></div>
              </label>
            </div>
          )}

          {/* Footer Buttons */}
          <div className="pt-4 border-t border-slate-100 flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={busy}
              className="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-bold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={busy}
              className="px-5 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/25 active:scale-[0.98] transition-all cursor-pointer disabled:opacity-60"
            >
              {busy ? 'Đang lưu…' : isEdit ? 'Cập nhật Tầng' : 'Khai báo Tầng'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
