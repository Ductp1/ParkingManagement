import React, { useState } from 'react';
import { X, AlertTriangle, RefreshCw, ShieldAlert, CheckCircle2 } from 'lucide-react';
import { ownerBookingsApi } from '@/api/ownerBookingsApi';

interface LotCancelModalProps {
  isOpen: boolean;
  onClose: () => void;
  bookingCode: string;
  plateNumber: string;
  ownerProfileId: number;
  onSuccess: () => void;
}

export const LotCancelModal: React.FC<LotCancelModalProps> = ({
  isOpen,
  onClose,
  bookingCode,
  plateNumber,
  ownerProfileId,
  onSuccess,
}) => {
  const [reason, setReason] = useState('Sự cố kỹ thuật tại bãi đỗ xe không thể tiếp nhận phương tiện');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError('Vui lòng nhập lý do sự cố hủy booking.');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await ownerBookingsApi.lotCancelBooking(bookingCode, {
        ownerProfileId,
        reason: reason.trim(),
      });
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể hủy booking. Vui lòng thử lại.'
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-2xl shadow-2xl border border-slate-100 w-full max-w-md overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-amber-50/70">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-amber-100 text-amber-700 flex items-center justify-center font-bold">
              <AlertTriangle size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Hủy do sự cố tại bãi (Lot Cancel)</h3>
              <p className="text-xs text-amber-700 font-medium">Bồi hoàn 100% tiền cọc (UI-23 / US-042)</p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={submitting}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs">
              {error}
            </div>
          )}

          <p className="text-sm text-slate-600 leading-relaxed">
            Bạn xác nhận hủy lượt đặt chỗ <strong className="font-mono text-slate-900 font-bold">{bookingCode}</strong> (Biển số: {plateNumber}) do sự cố đột xuất tại bãi đỗ?
          </p>

          <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 text-xs space-y-1.5">
            <div className="font-semibold flex items-center gap-1.5 text-amber-800">
              <ShieldAlert size={15} />
              Quy tắc hoàn tiền bảo đảm:
            </div>
            <ul className="list-disc pl-4 space-y-1 text-amber-700">
              <li>Hệ thống sẽ tự động hoàn <strong>100%</strong> toàn bộ số tiền cọc/đỗ xe lại cho khách hàng.</li>
              <li>Ô đỗ liên kết sẽ được giải phóng ngay lập tức.</li>
              <li>Lý do sự cố sẽ được thông báo trực tiếp qua ứng dụng tài xế.</li>
            </ul>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Lý do sự cố cụ thể <span className="text-rose-500">*</span>
            </label>
            <textarea
              required
              rows={3}
              disabled={submitting}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Ví dụ: Sự cố mất điện trạm barie, ngập nước cục bộ khu vực tầng hầm..."
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-amber-500/20 focus:border-amber-600 text-slate-900 transition-all resize-none"
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-amber-600 hover:bg-amber-700 active:bg-amber-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {submitting ? (
                <>
                  <RefreshCw size={14} className="animate-spin" />
                  Đang xử lý hủy...
                </>
              ) : (
                <>
                  <CheckCircle2 size={15} />
                  Xác nhận hủy bãi
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
