import React, { useState, useEffect } from 'react';
import { X, AlertTriangle, ShieldAlert, CheckCircle2, Loader2, Power } from 'lucide-react';

interface QuickClosureModalProps {
  isOpen: boolean;
  onClose: () => void;
  targetType: 'zone' | 'floor';
  targetName: string;
  isCurrentlyClosed: boolean;
  subItemCount?: number;
  onConfirm: (isClosed: boolean, closedReason: string) => Promise<void>;
}

export const QuickClosureModal: React.FC<QuickClosureModalProps> = ({
  isOpen,
  onClose,
  targetType,
  targetName,
  isCurrentlyClosed,
  subItemCount,
  onConfirm,
}) => {
  const [closedReason, setClosedReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const willClose = !isCurrentlyClosed;
  const typeLabel = targetType === 'zone' ? 'Khu vực' : 'Tầng';

  useEffect(() => {
    if (isOpen) {
      setClosedReason(
        willClose
          ? targetType === 'zone'
            ? 'Bảo trì hệ thống PCCC & chiếu sáng định kỳ'
            : 'Sơn kẻ lại vạch và bảo trì sàn đỗ'
          : ''
      );
      setErrorMessage('');
    }
  }, [isOpen, willClose, targetType]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (willClose && !closedReason.trim()) {
      setErrorMessage('Vui lòng nhập lý do tạm đóng để lưu vết vận hành.');
      return;
    }

    setBusy(true);
    try {
      await onConfirm(willClose, closedReason.trim());
      onClose();
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Có lỗi xảy ra khi cập nhật trạng thái hoạt động.');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
      <div className="relative w-full max-w-md rounded-3xl bg-white border border-slate-200 shadow-2xl p-6 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between pb-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div
              className={`w-10 h-10 rounded-2xl flex items-center justify-center shrink-0 ${
                willClose
                  ? 'bg-rose-50 text-rose-600 border border-rose-100'
                  : 'bg-emerald-50 text-emerald-600 border border-emerald-100'
              }`}
            >
              {willClose ? <ShieldAlert size={20} /> : <CheckCircle2 size={20} />}
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">
                {willClose ? `Tạm đóng ${typeLabel}` : `Mở lại hoạt động ${typeLabel}`}
              </h3>
              <p className="text-xs text-slate-500">
                {typeLabel}: <strong className="text-slate-800">{targetName}</strong>
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-xl flex items-center justify-center text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
          >
            <X size={16} />
          </button>
        </div>

        {errorMessage && (
          <div className="mt-4 p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
            <AlertTriangle size={15} className="text-rose-500 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          {willClose ? (
            <>
              {/* Cảnh báo tác động */}
              <div className="p-3.5 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 text-xs space-y-1">
                <div className="font-bold flex items-center gap-1.5">
                  <AlertTriangle size={14} className="text-amber-600 shrink-0" />
                  <span>Cảnh báo ảnh hưởng đặt chỗ:</span>
                </div>
                <p className="text-amber-800 leading-relaxed">
                  Khi tạm đóng {typeLabel.toLowerCase()} này, hệ thống sẽ ngay lập tức{' '}
                  <strong>ngừng nhận tất cả lượt đặt chỗ mới (Booking)</strong> đối với{' '}
                  {subItemCount ? `toàn bộ ${subItemCount} thuộc phạm vi` : 'các ô đỗ trực thuộc'}.
                </p>
              </div>

              {/* Nhập lý do */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Lý do tạm đóng: <span className="text-rose-500">*</span>
                </label>
                <textarea
                  value={closedReason}
                  onChange={(e) => setClosedReason(e.target.value)}
                  placeholder="VD: Sự cố kỹ thuật / Sơn vạch kẻ / Bảo trì PCCC định kỳ..."
                  rows={3}
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </>
          ) : (
            <div className="p-3.5 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-900 text-xs space-y-1">
              <div className="font-bold flex items-center gap-1.5">
                <CheckCircle2 size={15} className="text-emerald-600 shrink-0" />
                <span>Kích hoạt lại dịch vụ:</span>
              </div>
              <p className="text-emerald-800 leading-relaxed">
                {typeLabel} <strong>{targetName}</strong> sẽ mở cửa trở lại để nhận khách vãng lai và
                cho phép tài xế đặt trước chỗ trên ứng dụng di động.
              </p>
            </div>
          )}

          {/* Buttons */}
          <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
            <button
              type="button"
              disabled={busy}
              onClick={onClose}
              className="px-4 py-2 rounded-xl text-slate-600 hover:bg-slate-100 text-xs font-bold transition-all cursor-pointer"
            >
              Hủy
            </button>

            <button
              type="submit"
              disabled={busy}
              className={`px-4 py-2 rounded-xl text-white text-xs font-bold transition-all shadow-xs flex items-center gap-1.5 cursor-pointer disabled:opacity-50 ${
                willClose
                  ? 'bg-rose-600 hover:bg-rose-700 shadow-rose-600/20'
                  : 'bg-emerald-600 hover:bg-emerald-700 shadow-emerald-600/20'
              }`}
            >
              {busy ? (
                <>
                  <Loader2 size={14} className="animate-spin" />
                  Đang xử lý…
                </>
              ) : (
                <>
                  <Power size={14} />
                  {willClose ? 'Xác nhận Tạm đóng' : 'Mở lại hoạt động'}
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
