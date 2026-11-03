import React, { useState, useEffect } from 'react';
import { X, Building2, Sun, AlertTriangle } from 'lucide-react';
import type { ZoneDto, CreateZoneRequest, UpdateZoneRequest } from '@/types/ownerStructure';

interface ZoneModalProps {
  isOpen: boolean;
  onClose: () => void;
  lotId: number;
  initialData?: ZoneDto | null;
  onSubmit: (data: CreateZoneRequest | UpdateZoneRequest) => Promise<void>;
}

export const ZoneModal: React.FC<ZoneModalProps> = ({
  isOpen,
  onClose,
  initialData,
  onSubmit,
}) => {
  const isEdit = !!initialData;

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [isOutdoor, setIsOutdoor] = useState(false);
  const [isClosed, setIsClosed] = useState(false);
  const [closedReason, setClosedReason] = useState('');
  const [sortOrder, setSortOrder] = useState(0);

  const [busy, setBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (initialData) {
      setCode(initialData.code);
      setName(initialData.name);
      setIsOutdoor(initialData.isOutdoor);
      setIsClosed(initialData.isClosed);
      setClosedReason(initialData.closedReason || '');
      setSortOrder(initialData.sortOrder);
    } else {
      setCode('');
      setName('');
      setIsOutdoor(false);
      setIsClosed(false);
      setClosedReason('');
      setSortOrder(0);
    }
    setErrorMessage('');
  }, [initialData, isOpen]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    const trimmedCode = code.trim().toUpperCase();
    const trimmedName = name.trim();

    if (!trimmedCode) {
      setErrorMessage('Vui lòng nhập Mã khu vực (VD: ZA, THAP-A).');
      return;
    }
    if (!trimmedName) {
      setErrorMessage('Vui lòng nhập Tên khu vực.');
      return;
    }

    setBusy(true);
    try {
      if (isEdit) {
        const payload: UpdateZoneRequest = {
          code: trimmedCode,
          name: trimmedName,
          isOutdoor,
          isClosed,
          closedReason: isClosed ? closedReason.trim() || 'Bảo trì khu vực' : null,
          sortOrder,
        };
        await onSubmit(payload);
      } else {
        const payload: CreateZoneRequest = {
          code: trimmedCode,
          name: trimmedName,
          isOutdoor,
          sortOrder,
        };
        await onSubmit(payload);
      }
      onClose();
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Có lỗi xảy ra khi lưu khu vực. Vui lòng thử lại.');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="relative w-full max-w-lg rounded-3xl bg-white shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-slate-100 bg-slate-50/50">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-indigo-100 text-indigo-700 flex items-center justify-center">
              <Building2 size={18} />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900">
                {isEdit ? 'Chỉnh sửa Khu vực (Zone)' : 'Thêm Khu vực mới (Zone)'}
              </h2>
              <p className="text-xs text-slate-500">
                Phân chia không gian quản lý bãi đỗ xe theo tòa nhà hoặc khu vực
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
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {errorMessage && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-xs font-semibold text-rose-800 flex items-start gap-2">
              <AlertTriangle size={16} className="text-rose-600 shrink-0 mt-0.5" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {/* Mã khu vực */}
            <div className="sm:col-span-1">
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Mã Zone <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                maxLength={10}
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase().replace(/\s+/g, ''))}
                placeholder="VD: ZA"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs font-mono font-bold uppercase text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-indigo-600"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Duy nhất trong bãi</span>
            </div>

            {/* Tên khu vực */}
            <div className="sm:col-span-2">
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Tên Khu vực <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="VD: Khu Tháp A - Khối Văn Phòng"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-indigo-600"
              />
            </div>
          </div>

          {/* Phân loại ngoài trời / trong nhà */}
          <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className={`p-2 rounded-xl ${isOutdoor ? 'bg-amber-100 text-amber-700' : 'bg-blue-100 text-blue-700'}`}>
                {isOutdoor ? <Sun size={18} /> : <Building2 size={18} />}
              </div>
              <div>
                <div className="text-xs font-bold text-slate-900">
                  {isOutdoor ? 'Khu vực Ngoài trời (Outdoor)' : 'Khu vực Trong nhà / Hầm (Indoor)'}
                </div>
                <div className="text-[11px] text-slate-500">
                  {isOutdoor ? 'Bãi đỗ ngoài trời, không giới hạn chiều cao trần' : 'Có trần che hoặc kết cấu tầng hầm'}
                </div>
              </div>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={isOutdoor}
                onChange={(e) => setIsOutdoor(e.target.checked)}
                className="sr-only peer"
              />
              <div className="w-11 h-6 bg-slate-200 peer-focus:outline-hidden rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-amber-500"></div>
            </label>
          </div>

          {/* Thứ tự hiển thị */}
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              Thứ tự ưu tiên hiển thị (Sort Order)
            </label>
            <input
              type="number"
              value={sortOrder}
              onChange={(e) => setSortOrder(parseInt(e.target.value, 10) || 0)}
              className="w-full sm:w-32 px-3.5 py-2 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-indigo-600"
            />
          </div>

          {/* Chỉ hiển thị khi Chỉnh sửa: Tạm đóng khu vực */}
          {isEdit && (
            <div className="pt-3 border-t border-slate-100 space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <span className="text-xs font-bold text-slate-800">Tạm đóng khu vực này</span>
                  <p className="text-[11px] text-slate-500">Tạm ngưng phục vụ nhận xe tại toàn bộ các tầng trong khu vực</p>
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

              {isClosed && (
                <div>
                  <label className="block text-xs font-semibold text-rose-700 mb-1">
                    Lý do tạm đóng khu vực:
                  </label>
                  <input
                    type="text"
                    value={closedReason}
                    onChange={(e) => setClosedReason(e.target.value)}
                    placeholder="VD: Bảo trì hệ thống thông gió hầm..."
                    className="w-full px-3.5 py-2 rounded-xl border border-rose-200 bg-rose-50/30 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:border-rose-500"
                  />
                </div>
              )}
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
              {busy ? 'Đang lưu…' : isEdit ? 'Cập nhật Khu vực' : 'Tạo Khu vực'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
