import React, { useState } from 'react';
import { X, RefreshCw, XCircle } from 'lucide-react';
import { ownerBookingsApi } from '@/api/ownerBookingsApi';

interface RejectBookingModalProps {
  isOpen: boolean;
  onClose: () => void;
  bookingCode: string;
  plateNumber: string;
  ownerProfileId: number;
  onSuccess: () => void;
}

export const RejectBookingModal: React.FC<RejectBookingModalProps> = ({
  isOpen,
  onClose,
  bookingCode,
  plateNumber,
  ownerProfileId,
  onSuccess,
}) => {
  const [reason, setReason] = useState('Chủ bãi từ chối do hết chỗ');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await ownerBookingsApi.rejectBooking(bookingCode, {
        ownerProfileId,
        reason: reason.trim() || 'Chủ bãi từ chối do hết chỗ',
      });
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể từ chối đơn đặt chỗ. Vui lòng thử lại.'
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
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-rose-50/70">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-rose-100 text-rose-700 flex items-center justify-center font-bold">
              <XCircle size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Từ chối đơn đặt chỗ</h3>
              <p className="text-xs text-rose-600 font-medium">Bãi Mức 0 (Thủ công - US-031)</p>
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
            Bạn có chắc chắn muốn từ chối lượt đặt chỗ mã{' '}
            <strong className="font-mono text-slate-900 font-bold">{bookingCode}</strong> (Biển số: {plateNumber})?
          </p>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Lý do từ chối
            </label>
            <textarea
              rows={3}
              disabled={submitting}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Nhập lý do gửi tới tài xế..."
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-600 text-slate-900 transition-all resize-none"
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
              className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 active:bg-rose-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {submitting ? (
                <>
                  <RefreshCw size={14} className="animate-spin" />
                  Đang từ chối...
                </>
              ) : (
                <>
                  <XCircle size={15} />
                  Xác nhận từ chối
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
