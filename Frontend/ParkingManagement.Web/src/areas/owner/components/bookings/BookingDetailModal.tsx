import React, { useState, useEffect } from 'react';
import {
  X,
  Calendar,
  Car,
  Clock,
  DollarSign,
  History,
  AlertCircle,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  RefreshCw,
  ArrowRight,
} from 'lucide-react';
import {
  ownerBookingsApi,
  type BookingDetailDto,
} from '@/api/ownerBookingsApi';
import { OwnerRefundModal } from './OwnerRefundModal';
import { RejectBookingModal } from './RejectBookingModal';
import { LotCancelModal } from './LotCancelModal';

interface BookingDetailModalProps {
  isOpen: boolean;
  onClose: () => void;
  bookingCode: string | null;
  ownerProfileId: number;
  onStatusChanged?: () => void;
}

const STATUS_MAP: Record<string, { label: string; badge: string }> = {
  Created: { label: 'Khởi tạo', badge: 'bg-slate-100 text-slate-700 border-slate-200' },
  PendingPayment: { label: 'Chờ thanh toán', badge: 'bg-amber-50 text-amber-700 border-amber-200' },
  PendingOwnerApproval: { label: 'Chờ chủ bãi duyệt', badge: 'bg-blue-50 text-blue-700 border-blue-200 animate-pulse' },
  Confirmed: { label: 'Đã xác nhận', badge: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  CheckedIn: { label: 'Đã vào bãi', badge: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  Parking: { label: 'Đang đỗ xe', badge: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  CheckedOut: { label: 'Đã ra bãi', badge: 'bg-teal-50 text-teal-700 border-teal-200' },
  Completed: { label: 'Hoàn thành', badge: 'bg-slate-100 text-slate-700 border-slate-200' },
  Cancelled: { label: 'Đã hủy', badge: 'bg-rose-50 text-rose-700 border-rose-200' },
  CancelledNoRefund: { label: 'Hủy không hoàn tiền', badge: 'bg-rose-50 text-rose-700 border-rose-200' },
  NoShow: { label: 'Vắng mặt (No-Show)', badge: 'bg-amber-50 text-amber-700 border-amber-200' },
  Rejected: { label: 'Bị từ chối', badge: 'bg-rose-50 text-rose-700 border-rose-200' },
};

export const BookingDetailModal: React.FC<BookingDetailModalProps> = ({
  isOpen,
  onClose,
  bookingCode,
  ownerProfileId,
  onStatusChanged,
}) => {
  const [detail, setDetail] = useState<BookingDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [approving, setApproving] = useState(false);

  // Sub-modals
  const [isRefundOpen, setIsRefundOpen] = useState(false);
  const [isRejectOpen, setIsRejectOpen] = useState(false);
  const [isLotCancelOpen, setIsLotCancelOpen] = useState(false);

  const fetchDetail = async (code: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await ownerBookingsApi.getBookingByCode(code);
      setDetail(data);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.message ||
        'Không thể tải chi tiết đặt chỗ.'
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && bookingCode) {
      void fetchDetail(bookingCode);
    } else {
      setDetail(null);
      setError(null);
      setActionSuccess(null);
    }
  }, [isOpen, bookingCode]);

  if (!isOpen) return null;

  const formatVnd = (amount?: number) => {
    if (amount === undefined || amount === null) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(amount);
  };

  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '--';
    try {
      return new Date(isoString).toLocaleString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return isoString;
    }
  };

  // Duyệt đặt chỗ bãi mức 0
  const handleApprove = async () => {
    if (!detail) return;
    setApproving(true);
    setError(null);
    try {
      await ownerBookingsApi.approveBooking(detail.code, {
        ownerProfileId,
        reason: 'Chủ bãi đã xác nhận giữ chỗ',
      });
      setActionSuccess('Đã phê duyệt đặt chỗ thành công!');
      await fetchDetail(detail.code);
      onStatusChanged?.();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.message ||
        'Không thể phê duyệt booking.'
      );
    } finally {
      setApproving(false);
    }
  };

  const isPendingApproval = detail?.status === 'PendingOwnerApproval';
  const isActive = detail && ['Confirmed', 'CheckedIn', 'Parking'].includes(detail.status);
  const canRefund = detail && detail.paidAmount > 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-3xl shadow-2xl border border-slate-100 w-full max-w-2xl overflow-hidden flex flex-col max-h-[92vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-slate-50/70">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-blue-100 text-blue-700 flex items-center justify-center font-bold">
              <Car size={20} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-slate-900 font-mono">
                  {detail?.code || bookingCode}
                </h3>
                {detail && (
                  <span
                    className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                      STATUS_MAP[detail.status]?.badge || 'bg-slate-100 text-slate-700'
                    }`}
                  >
                    {STATUS_MAP[detail.status]?.label || detail.status}
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                {detail?.parkingLotName || 'Chi tiết đặt chỗ ParkMaster'}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-6 overflow-y-auto">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {actionSuccess && (
            <div className="p-3.5 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-start gap-2.5">
              <CheckCircle2 size={16} className="shrink-0 mt-0.5" />
              <span>{actionSuccess}</span>
            </div>
          )}

          {loading ? (
            <div className="py-16 flex flex-col items-center justify-center gap-2 text-slate-500">
              <RefreshCw size={24} className="animate-spin text-blue-600" />
              <p className="text-xs font-medium">Đang tải chi tiết đặt chỗ...</p>
            </div>
          ) : detail ? (
            <>
              {/* Thông báo bãi Mức 0 đang chờ duyệt */}
              {isPendingApproval && (
                <div className="p-4 rounded-2xl bg-blue-50 border border-blue-200 text-blue-900 text-xs space-y-2">
                  <div className="flex items-center gap-2 font-bold text-sm text-blue-800">
                    <Clock size={16} className="text-blue-600 animate-pulse" />
                    Đơn đặt chỗ đang chờ bạn phê duyệt (Bãi Mức 0 - Thủ công)
                  </div>
                  <p className="text-blue-700 leading-relaxed">
                    Khách hàng đang chờ xác nhận giữ chỗ tại bãi. Vui lòng kiểm tra thực tế chỗ trống và bấm <strong>Duyệt đặt chỗ</strong> hoặc <strong>Từ chối</strong> nếu không đủ chỗ.
                  </p>
                  <div className="flex items-center gap-2 pt-1">
                    <button
                      type="button"
                      onClick={handleApprove}
                      disabled={approving}
                      className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 transition-colors"
                    >
                      <CheckCircle2 size={14} />
                      {approving ? 'Đang duyệt...' : 'Duyệt đặt chỗ ngay'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsRejectOpen(true)}
                      disabled={approving}
                      className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold text-rose-700 bg-rose-100 hover:bg-rose-200 transition-colors"
                    >
                      <XCircle size={14} />
                      Từ chối
                    </button>
                  </div>
                </div>
              )}

              {/* Grid thông tin chung */}
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                <div className="bg-slate-50 p-3.5 rounded-2xl border border-slate-200/80">
                  <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                    Biển số xe
                  </span>
                  <span className="text-sm font-black text-slate-900 font-mono mt-0.5 block">
                    {detail.plateNumber}
                  </span>
                  <span className="text-[10px] text-slate-400">Loại: {detail.vehicleType}</span>
                </div>

                <div className="bg-slate-50 p-3.5 rounded-2xl border border-slate-200/80">
                  <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                    Vị trí ô đỗ
                  </span>
                  <span className="text-sm font-bold text-slate-900 mt-0.5 block">
                    {detail.slotCode || 'Sắp xếp linh hoạt (Dynamic)'}
                  </span>
                  <span className="text-[10px] text-slate-400">Mã bãi: #{detail.parkingLotId}</span>
                </div>

                <div className="bg-slate-50 p-3.5 rounded-2xl border border-slate-200/80">
                  <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                    Đã thanh toán
                  </span>
                  <span className="text-sm font-black text-emerald-600 mt-0.5 block">
                    {formatVnd(detail.paidAmount)}
                  </span>
                  <span className="text-[10px] text-slate-400">Tổng phí: {formatVnd(detail.totalAmount)}</span>
                </div>
              </div>

              {/* Khung giờ đỗ xe */}
              <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200/80 space-y-2 text-xs">
                <span className="font-bold text-slate-700 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                  <Calendar size={14} className="text-blue-600" />
                  Khung thời gian đặt chỗ
                </span>
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pt-1 text-slate-600">
                  <div>
                    <span className="text-slate-400 block text-[10px]">Giờ bắt đầu dự kiến:</span>
                    <strong className="text-slate-900 text-xs">{formatDate(detail.startAtUtc)}</strong>
                  </div>
                  <ArrowRight size={14} className="hidden sm:block text-slate-400" />
                  <div>
                    <span className="text-slate-400 block text-[10px]">Giờ kết thúc dự kiến:</span>
                    <strong className="text-slate-900 text-xs">{formatDate(detail.endAtUtc)}</strong>
                  </div>
                </div>
                {detail.holdExpiresAtUtc && (
                  <p className="text-[11px] text-amber-700 pt-1">
                    Thời hạn giữ chỗ: {formatDate(detail.holdExpiresAtUtc)}
                  </p>
                )}
              </div>

              {/* Chi tiết giá & Bảng giá áp dụng */}
              {detail.priceSnapshot && (
                <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200/80 space-y-2 text-xs">
                  <span className="font-bold text-slate-700 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                    <DollarSign size={14} className="text-emerald-600" />
                    Chi tiết biểu phí & Giá đã chốt
                  </span>
                  <div className="space-y-1.5 pt-1 text-slate-600">
                    <div className="flex justify-between">
                      <span>Cước cơ sở:</span>
                      <span className="font-semibold text-slate-900">{formatVnd(detail.priceSnapshot.baseAmount)}</span>
                    </div>
                    {detail.priceSnapshot.surchargeAmount > 0 && (
                      <div className="flex justify-between text-amber-700">
                        <span>Phụ thu (Đêm / Giờ cao điểm / Lễ):</span>
                        <span className="font-semibold">+{formatVnd(detail.priceSnapshot.surchargeAmount)}</span>
                      </div>
                    )}
                    {detail.discountAmount > 0 && (
                      <div className="flex justify-between text-emerald-700">
                        <span>Khuyến mãi / Giảm trừ:</span>
                        <span className="font-semibold">-{formatVnd(detail.discountAmount)}</span>
                      </div>
                    )}
                    <div className="pt-2 border-t border-slate-200 flex justify-between font-bold text-slate-900">
                      <span>Thành tiền cuối cùng:</span>
                      <span className="text-sm text-blue-600">{formatVnd(detail.priceSnapshot.finalAmount)}</span>
                    </div>
                  </div>
                </div>
              )}

              {/* Lịch sử trạng thái (Status Logs Timeline) */}
              <div className="space-y-3">
                <span className="font-bold text-slate-700 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                  <History size={14} className="text-indigo-600" />
                  Lịch sử vòng đời đơn ({detail.history?.length || 0} mốc)
                </span>
                <div className="space-y-2 border-l-2 border-slate-200 pl-4 ml-2">
                  {detail.history && detail.history.length > 0 ? (
                    detail.history.map((log, idx) => (
                      <div key={idx} className="relative text-xs">
                        <div className="absolute -left-[21px] top-1 w-2.5 h-2.5 rounded-full bg-blue-500 border-2 border-white ring-2 ring-slate-100" />
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-slate-800">
                            {STATUS_MAP[log.toStatus]?.label || log.toStatus}
                          </span>
                          <span className="text-[10px] text-slate-400 font-mono">
                            {formatDate(log.atUtc)}
                          </span>
                        </div>
                        {log.reason && (
                          <p className="text-[11px] text-slate-500 mt-0.5 italic">
                            &ldquo;{log.reason}&rdquo;
                          </p>
                        )}
                      </div>
                    ))
                  ) : (
                    <p className="text-xs text-slate-400 italic">Chưa có bản ghi lịch sử trạng thái.</p>
                  )}
                </div>
              </div>

              {/* Actions Footer */}
              <div className="pt-4 border-t border-slate-100 flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  {/* Nút Hoàn tiền (UI-24) */}
                  {canRefund && (
                    <button
                      type="button"
                      onClick={() => setIsRefundOpen(true)}
                      className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-bold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 rounded-xl transition-all"
                    >
                      <DollarSign size={14} />
                      Hoàn tiền cho khách (UI-24)
                    </button>
                  )}

                  {/* Nút Hủy sự cố bãi (UI-23) */}
                  {isActive && (
                    <button
                      type="button"
                      onClick={() => setIsLotCancelOpen(true)}
                      className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-bold text-amber-700 bg-amber-50 hover:bg-amber-100 border border-amber-200 rounded-xl transition-all"
                    >
                      <AlertTriangle size={14} />
                      Hủy sự cố bãi (Lot Cancel)
                    </button>
                  )}
                </div>

                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-xs font-semibold text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors ml-auto"
                >
                  Đóng
                </button>
              </div>
            </>
          ) : null}
        </div>
      </div>

      {/* Sub-modals */}
      {detail && (
        <>
          <OwnerRefundModal
            isOpen={isRefundOpen}
            onClose={() => setIsRefundOpen(false)}
            bookingId={detail.id}
            bookingCode={detail.code}
            ownerProfileId={ownerProfileId}
            onSuccess={() => {
              setActionSuccess('Đã thực hiện hoàn tiền bồi hoàn cho khách thành công!');
              void fetchDetail(detail.code);
              onStatusChanged?.();
            }}
          />

          <RejectBookingModal
            isOpen={isRejectOpen}
            onClose={() => setIsRejectOpen(false)}
            bookingCode={detail.code}
            plateNumber={detail.plateNumber}
            ownerProfileId={ownerProfileId}
            onSuccess={() => {
              setActionSuccess('Đã từ chối đơn đặt chỗ!');
              void fetchDetail(detail.code);
              onStatusChanged?.();
            }}
          />

          <LotCancelModal
            isOpen={isLotCancelOpen}
            onClose={() => setIsLotCancelOpen(false)}
            bookingCode={detail.code}
            plateNumber={detail.plateNumber}
            ownerProfileId={ownerProfileId}
            onSuccess={() => {
              setActionSuccess('Đã hủy booking do sự cố bãi và hoàn 100% cho khách!');
              void fetchDetail(detail.code);
              onStatusChanged?.();
            }}
          />
        </>
      )}
    </div>
  );
};
