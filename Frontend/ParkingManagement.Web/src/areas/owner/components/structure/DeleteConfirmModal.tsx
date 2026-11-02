import React, { useState } from 'react';
import { AlertTriangle, Trash2, X } from 'lucide-react';

interface DeleteConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description: string;
  itemName: string;
  onConfirm: () => Promise<void>;
}

export const DeleteConfirmModal: React.FC<DeleteConfirmModalProps> = ({
  isOpen,
  onClose,
  title,
  description,
  itemName,
  onConfirm,
}) => {
  const [busy, setBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  if (!isOpen) return null;

  const handleConfirm = async () => {
    setErrorMessage('');
    setBusy(true);
    try {
      await onConfirm();
      onClose();
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Không thể xóa mục này do còn ràng buộc dữ liệu con.');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="relative w-full max-w-md rounded-3xl bg-white shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-slate-100 bg-rose-50/50">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-rose-100 text-rose-600 flex items-center justify-center">
              <AlertTriangle size={18} />
            </div>
            <h2 className="text-base font-bold text-slate-900">{title}</h2>
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

        {/* Content */}
        <div className="p-6 space-y-4">
          {errorMessage && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-xs font-semibold text-rose-800 flex items-start gap-2">
              <AlertTriangle size={16} className="text-rose-600 shrink-0 mt-0.5" />
              <span>{errorMessage}</span>
            </div>
          )}

          <p className="text-xs text-slate-600 leading-relaxed">
            {description}
          </p>

          <div className="p-3 rounded-xl bg-slate-100 border border-slate-200 text-xs font-bold text-slate-800 truncate">
            {itemName}
          </div>

          <p className="text-[11px] text-amber-700 bg-amber-50 p-3 rounded-xl border border-amber-200 font-medium">
            ⚠️ <strong>Lưu ý:</strong> Hệ thống sẽ tự động chặn xóa nếu khu vực còn tầng trực thuộc hoặc tầng còn ô đỗ xe.
          </p>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="px-4 py-2 rounded-xl border border-slate-200 text-xs font-bold text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={busy}
            className="px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold shadow-md shadow-rose-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-60"
          >
            <Trash2 size={14} />
            {busy ? 'Đang xóa…' : 'Xác nhận xóa'}
          </button>
        </div>
      </div>
    </div>
  );
};
