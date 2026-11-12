import React, { useState, useEffect } from 'react';
import { X, DollarSign, AlertCircle, RefreshCw, CheckCircle2, ShieldCheck } from 'lucide-react';
import {
  ownerRefundApi,
  type PaymentDto,
  type OwnerRefundResultDto,
} from '@/api/ownerRefundApi';

interface OwnerRefundModalProps {
  isOpen: boolean;
  onClose: () => void;
  bookingId: number;
  bookingCode: string;
  ownerProfileId: number;
  complaintId?: number | null;
  onSuccess?: (result: OwnerRefundResultDto) => void;
}

export const OwnerRefundModal: React.FC<OwnerRefundModalProps> = ({
  isOpen,
  onClose,
  bookingId,
  bookingCode,
  ownerProfileId,
  complaintId = null,
  onSuccess,
}) => {
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [selectedPaymentId, setSelectedPaymentId] = useState<number | null>(null);
  const [loadingPayments, setLoadingPayments] = useState(true);
  const [refundAmount, setRefundAmount] = useState<number>(0);
  const [reasonDescription, setReasonDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch payments for this booking
  useEffect(() => {
    if (!isOpen || !bookingId) return;

    let isMounted = true;
    setLoadingPayments(true);
    setError(null);

    ownerRefundApi
      .getPaymentsByBooking(bookingId)
      .then((data) => {
        if (!isMounted) return;
        setPayments(data || []);
        // Find completed payment with refundable amount
        const successful = data.find(
          (p) => (p.status === 'Completed' || p.status === 'Success') && p.amount > p.refundedAmount
        ) || data[0];

        if (successful) {
          setSelectedPaymentId(successful.id);
          const maxRefund = Math.max(0, successful.amount - successful.refundedAmount);
          setRefundAmount(maxRefund);
        }
      })
      .catch((err: unknown) => {
        if (!isMounted) return;
        const msg = err instanceof Error ? err.message : 'Không thể tải thông tin thanh toán.';
        setError(msg);
      })
      .finally(() => {
        if (isMounted) setLoadingPayments(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, bookingId]);

  if (!isOpen) return null;

  const currentPayment = payments.find((p) => p.id === selectedPaymentId) || payments[0];
  const maxRefundable = currentPayment
    ? Math.max(0, currentPayment.amount - currentPayment.refundedAmount)
    : 0;

  const formatVnd = (val: number) => {
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(val);
  };

  const handleSetQuickPercentage = (percent: number) => {
    if (!maxRefundable) return;
    const calculated = Math.round((maxRefundable * percent) / 100);
    setRefundAmount(calculated);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPaymentId) {
      setError('Vui lòng chọn giao dịch cần hoàn tiền.');
      return;
    }
    if (refundAmount <= 0) {
      setError('Số tiền hoàn phải lớn hơn 0 ₫.');
      return;
    }
    if (refundAmount > maxRefundable) {
      setError(`Số tiền hoàn không được vượt quá số tiền còn lại (${formatVnd(maxRefundable)}).`);
      return;
    }
    if (reasonDescription.trim().length < 5) {
      setError('Vui lòng nhập lý do hoàn tiền cụ thể (tối thiểu 5 ký tự).');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const result = await ownerRefundApi.processOwnerRefund({
        paymentId: selectedPaymentId,
        ownerProfileId,
        amount: refundAmount,
        reasonDescription: reasonDescription.trim(),
        complaintId,
      });

      onSuccess?.(result);
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Hoàn tiền không thành công. Vui lòng kiểm tra lại.'
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-2xl shadow-2xl border border-slate-100 w-full max-w-lg overflow-hidden flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-emerald-50/60">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
              <DollarSign size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Hoàn tiền bồi hoàn cho khách</h3>
              <p className="text-xs text-emerald-700 font-medium">
                Mã booking: <span className="font-mono font-bold">{bookingCode}</span> (UI-24 / US-041)
              </p>
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

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {loadingPayments ? (
            <div className="py-8 flex flex-col items-center justify-center gap-2 text-slate-500">
              <RefreshCw size={20} className="animate-spin text-emerald-600" />
              <p className="text-xs">Đang kiểm tra giao dịch thanh toán...</p>
            </div>
          ) : payments.length === 0 ? (
            <div className="p-4 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs leading-relaxed">
              Không tìm thấy giao dịch thanh toán nào hợp lệ cho đơn đặt chỗ này. Không thể thực hiện hoàn tiền.
            </div>
          ) : (
            <>
              {/* Chọn giao dịch nếu có nhiều lần thanh toán */}
              {payments.length > 1 && (
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Chọn giao dịch thanh toán
                  </label>
                  <select
                    value={selectedPaymentId || ''}
                    onChange={(e) => {
                      const id = Number(e.target.value);
                      setSelectedPaymentId(id);
                      const p = payments.find((item) => item.id === id);
                      if (p) {
                        setRefundAmount(Math.max(0, p.amount - p.refundedAmount));
                      }
                    }}
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-600 bg-white"
                  >
                    {payments.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.code} - {formatVnd(p.amount)} ({p.method}) - Trạng thái: {p.status}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Thông tin giao dịch thanh toán */}
              {currentPayment && (
                <div className="p-4 rounded-xl bg-slate-50 border border-slate-200/80 space-y-2 text-xs">
                  <div className="flex justify-between items-center text-slate-600">
                    <span>Mã giao dịch:</span>
                    <span className="font-mono font-semibold text-slate-900">{currentPayment.code}</span>
                  </div>
                  <div className="flex justify-between items-center text-slate-600">
                    <span>Phương thức:</span>
                    <span className="font-semibold text-slate-900">{currentPayment.method}</span>
                  </div>
                  <div className="flex justify-between items-center text-slate-600">
                    <span>Số tiền đã thanh toán:</span>
                    <span className="font-bold text-slate-900">{formatVnd(currentPayment.amount)}</span>
                  </div>
                  <div className="flex justify-between items-center text-slate-600">
                    <span>Đã hoàn trước đây:</span>
                    <span className="font-semibold text-amber-700">{formatVnd(currentPayment.refundedAmount)}</span>
                  </div>
                  <div className="pt-2 border-t border-slate-200 flex justify-between items-center font-bold text-slate-900">
                    <span>Số tiền tối đa có thể hoàn:</span>
                    <span className="text-emerald-700 text-sm">{formatVnd(maxRefundable)}</span>
                  </div>
                </div>
              )}

              {/* Số tiền hoàn */}
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-semibold text-slate-700">
                    Số tiền hoàn lại cho khách (VNĐ) <span className="text-rose-500">*</span>
                  </label>
                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      onClick={() => handleSetQuickPercentage(25)}
                      className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-slate-100 hover:bg-slate-200 text-slate-700 transition-colors"
                    >
                      25%
                    </button>
                    <button
                      type="button"
                      onClick={() => handleSetQuickPercentage(50)}
                      className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-slate-100 hover:bg-slate-200 text-slate-700 transition-colors"
                    >
                      50%
                    </button>
                    <button
                      type="button"
                      onClick={() => handleSetQuickPercentage(100)}
                      className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-emerald-100 hover:bg-emerald-200 text-emerald-800 transition-colors"
                    >
                      100%
                    </button>
                  </div>
                </div>
                <div className="relative">
                  <span className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 font-bold text-xs">₫</span>
                  <input
                    type="number"
                    required
                    min={1000}
                    max={maxRefundable}
                    step={1000}
                    disabled={submitting || maxRefundable <= 0}
                    value={refundAmount}
                    onChange={(e) => setRefundAmount(Number(e.target.value))}
                    className="w-full pl-8 pr-3 py-2.5 text-sm font-semibold rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-600 text-slate-900 transition-all"
                  />
                </div>
                <p className="text-[11px] text-slate-400 mt-1">
                  Khoản tiền này sẽ được khấu trừ vào số dư quyết toán tiếp theo của bãi xe.
                </p>
              </div>

              {/* Lý do hoàn tiền */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Lý do hoàn tiền / bồi hoàn <span className="text-rose-500">*</span>
                </label>
                <textarea
                  required
                  rows={3}
                  disabled={submitting}
                  value={reasonDescription}
                  onChange={(e) => setReasonDescription(e.target.value)}
                  placeholder="Ví dụ: Bồi hoàn do bãi xe cúp điện đột xuất, barie kẹt khiến khách phải chờ lâu..."
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-600 text-slate-900 placeholder:text-slate-400 transition-all resize-none"
                />
              </div>

              {/* Alert note */}
              <div className="p-3 rounded-xl bg-blue-50 border border-blue-200 text-blue-900 text-xs flex items-start gap-2 leading-relaxed">
                <ShieldCheck size={16} className="text-blue-600 shrink-0 mt-0.5" />
                <span>
                  Hệ thống thanh toán sẽ ghi nhận lý do <strong className="font-semibold">LotFault (Lỗi tại bãi)</strong> và kích hoạt quy trình hoàn tiền tự động qua cổng thanh toán ban đầu (VNPAY/VietQR).
                </span>
              </div>
            </>
          )}

          {/* Action buttons */}
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
              disabled={submitting || maxRefundable <= 0 || loadingPayments || payments.length === 0}
              className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 active:bg-emerald-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {submitting ? (
                <>
                  <RefreshCw size={14} className="animate-spin" />
                  Đang xử lý hoàn tiền...
                </>
              ) : (
                <>
                  <CheckCircle2 size={15} />
                  Xác nhận hoàn {formatVnd(refundAmount)}
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
