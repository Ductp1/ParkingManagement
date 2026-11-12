import React, { useState, useEffect } from 'react';
import {
  X,
  Building2,
  Clock,
  AlertCircle,
  RefreshCw,
  LogOut,
  Undo2,
  ShieldAlert,
} from 'lucide-react';
import {
  ownerExitApi,
  type OwnerExitStatusDto,
  type OwnerExitImpactSummaryDto,
} from '@/api/ownerExitApi';

interface ExitPlatformModalProps {
  isOpen: boolean;
  onClose: () => void;
  ownerProfileId: number;
  onStatusChanged?: (status: OwnerExitStatusDto) => void;
}

export const ExitPlatformModal: React.FC<ExitPlatformModalProps> = ({
  isOpen,
  onClose,
  ownerProfileId,
  onStatusChanged,
}) => {
  const [exitStatus, setExitStatus] = useState<OwnerExitStatusDto | null>(null);
  const [impact, setImpact] = useState<OwnerExitImpactSummaryDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form inputs
  const [reason, setReason] = useState('');
  const [agreeTerms, setAgreeTerms] = useState(false);

  // Rút lại form
  const [cancelReason, setCancelReason] = useState('');

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [statusData, impactData] = await Promise.all([
        ownerExitApi.getExitStatus(ownerProfileId),
        ownerExitApi.getExitImpact(ownerProfileId),
      ]);
      setExitStatus(statusData);
      setImpact(impactData);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.message ||
        'Không thể tải trạng thái hợp tác của chủ bãi.'
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      void loadData();
    } else {
      setReason('');
      setAgreeTerms(false);
      setError(null);
    }
  }, [isOpen, ownerProfileId]);

  if (!isOpen) return null;

  const isLeaving = exitStatus?.status === 'Leaving';

  // Gửi yêu cầu xin rời nền tảng (30 ngày)
  const handleRequestExit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (reason.trim().length < 10) {
      setError('Vui lòng nêu rõ lý do xin rời nền tảng (tối thiểu 10 ký tự).');
      return;
    }
    if (!agreeTerms) {
      setError('Bạn cần đồng ý với các điều khoản thông báo trước 30 ngày.');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const updated = await ownerExitApi.requestExit(ownerProfileId, {
        reason: reason.trim(),
        noticeDays: 30,
      });
      setExitStatus(updated);
      onStatusChanged?.(updated);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể gửi yêu cầu rời nền tảng.'
      );
    } finally {
      setSubmitting(false);
    }
  };

  // Rút lại yêu cầu
  const handleCancelExit = async () => {
    setSubmitting(true);
    setError(null);

    try {
      const updated = await ownerExitApi.cancelExit(ownerProfileId, {
        reason: cancelReason.trim() || 'Chủ bãi quyết định tiếp tục đồng hành cùng ParkMaster',
      });
      setExitStatus(updated);
      onStatusChanged?.(updated);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể rút lại yêu cầu rời nền tảng.'
      );
    } finally {
      setSubmitting(false);
    }
  };

  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '--';
    try {
      return new Date(isoString).toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return isoString;
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-3xl shadow-2xl border border-slate-100 w-full max-w-lg overflow-hidden flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-rose-50/70">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-rose-100 text-rose-700 flex items-center justify-center font-bold">
              <LogOut size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Thủ tục xin rời nền tảng</h3>
              <p className="text-xs text-rose-700 font-medium">Hợp đồng đối tác ParkMaster (UI-21 / US-057)</p>
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
        <div className="p-6 space-y-4 overflow-y-auto">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {loading ? (
            <div className="py-16 flex flex-col items-center justify-center gap-2 text-slate-500">
              <RefreshCw size={24} className="animate-spin text-rose-600" />
              <p className="text-xs font-medium">Đang kiểm tra hồ sơ hợp tác...</p>
            </div>
          ) : isLeaving ? (
            /* TRƯỜNG HỢP 1: ĐANG TRONG THỜI HẠN THÔNG BÁO 30 NGÀY */
            <div className="space-y-4">
              <div className="p-5 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 space-y-2">
                <div className="flex items-center gap-2 text-amber-800 font-bold text-sm">
                  <Clock size={18} className="animate-pulse" />
                  Đang trong thời hạn thông báo rời nền tảng
                </div>
                <p className="text-xs text-amber-800 leading-relaxed">
                  Hồ sơ đối tác của bạn đã nộp đơn xin rời nền tảng vào ngày{' '}
                  <strong>{formatDate(exitStatus?.exitRequestedAtUtc)}</strong>.
                </p>
                <div className="pt-2 border-t border-amber-200/80 flex items-center justify-between text-xs">
                  <span>Ngày chính thức chấm dứt hợp đồng:</span>
                  <strong className="text-sm font-black text-rose-700">
                    {formatDate(exitStatus?.exitEffectiveAtUtc)}
                  </strong>
                </div>
                <div className="flex items-center justify-between text-xs">
                  <span>Số ngày còn lại trong thời gian báo trước:</span>
                  <span className="font-bold text-amber-900 bg-amber-200/60 px-2 py-0.5 rounded-md">
                    {exitStatus?.remainingDays ?? 0} ngày
                  </span>
                </div>
              </div>

              {exitStatus?.exitReason && (
                <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 text-xs">
                  <span className="text-slate-500 block text-[11px] font-semibold uppercase">Lý do đã gửi:</span>
                  <p className="text-slate-800 mt-0.5 italic">&ldquo;{exitStatus.exitReason}&rdquo;</p>
                </div>
              )}

              <div className="p-4 rounded-xl bg-blue-50 border border-blue-200 text-blue-900 text-xs space-y-1.5 leading-relaxed">
                <p className="font-bold">Thay đổi quyết định?</p>
                <p>
                  Bạn có thể rút lại yêu cầu xin rời nền tảng bất cứ lúc nào trước ngày hiệu lực để tiếp tục duy trì hoạt động của các bãi đỗ xe và nhận khách hàng qua ParkMaster.
              {exitStatus?.canCancel && (
                <div className="space-y-1">
                  <label className="text-[11px] font-semibold text-slate-700">Ghi chú khi rút lại yêu cầu (tùy chọn):</label>
                  <input
                    type="text"
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                    placeholder="Nhập lý do rút lại đơn hoặc để trống..."
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-600 text-slate-900"
                  />
                </div>
              )}
                </p>
              </div>

              <div className="pt-3 border-t border-slate-100 flex items-center justify-between gap-3">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-xs font-semibold text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
                >
                  Đóng
                </button>
                {exitStatus?.canCancel && (
                  <button
                    type="button"
                    onClick={handleCancelExit}
                    disabled={submitting}
                    className="inline-flex items-center gap-1.5 px-5 py-2.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 active:bg-emerald-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
                  >
                    {submitting ? (
                      <>
                        <RefreshCw size={14} className="animate-spin" />
                        Đang xử lý...
                      </>
                    ) : (
                      <>
                        <Undo2 size={15} />
                        Rút lại yêu cầu (Tiếp tục đồng hành)
                      </>
                    )}
                  </button>
                )}
              </div>
            </div>
          ) : (
            /* TRƯỜNG HỢP 2: NỘP ĐƠN MỚI (STATUS LÀ ACTIVE) */
            <form onSubmit={handleRequestExit} className="space-y-4">
              {/* Điều khoản hợp đồng 30 ngày */}
              <div className="p-4 rounded-2xl bg-amber-50/80 border border-amber-200 text-amber-900 text-xs space-y-2 leading-relaxed">
                <div className="font-bold flex items-center gap-1.5 text-amber-800">
                  <ShieldAlert size={16} />
                  Quy định hợp đồng hợp tác đối tác (Điều 10.2):
                </div>
                <p>
                  Chủ bãi có quyền đưa bãi đỗ xe rời khỏi nền tảng bằng thông báo gửi trước ít nhất{' '}
                  <strong className="underline">30 (ba mươi) ngày</strong>. Trong thời gian báo trước, chủ bãi có trách nhiệm tiếp tục phục vụ các lượt đặt chỗ đã xác nhận.
                </p>
              </div>

              {/* Tác động ảnh hưởng bãi xe */}
              {impact && (
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-2 text-xs">
                  <span className="font-bold text-slate-800 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                    <Building2 size={14} className="text-blue-600" />
                    Đánh giá quy mô ảnh hưởng (US-057)
                  </span>
                  <div className="grid grid-cols-3 gap-2 pt-1 text-center">
                    <div className="bg-white p-2 rounded-xl border border-slate-200">
                      <span className="text-[10px] text-slate-400 block">Tổng số bãi</span>
                      <strong className="text-sm font-bold text-slate-800">{impact.totalLots}</strong>
                    </div>
                    <div className="bg-white p-2 rounded-xl border border-slate-200">
                      <span className="text-[10px] text-slate-400 block">Đang hoạt động</span>
                      <strong className="text-sm font-bold text-emerald-600">{impact.activeLots}</strong>
                    </div>
                    <div className="bg-white p-2 rounded-xl border border-slate-200">
                      <span className="text-[10px] text-slate-400 block">Tổng số ô đỗ</span>
                      <strong className="text-sm font-bold text-indigo-600">{impact.totalSlots}</strong>
                    </div>
                  </div>
                  <p className="text-[11px] text-slate-500 pt-1">
                    Khi hết thời hạn 30 ngày, toàn bộ các bãi xe trên sẽ được tự động đình chỉ hiển thị trên ứng dụng tìm kiếm của tài xế.
                  </p>
                </div>
              )}

              {/* Lý do xin rời */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Lý do xin rời nền tảng <span className="text-rose-500">*</span>
                </label>
                <textarea
                  required
                  rows={3}
                  disabled={submitting}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Vui lòng chia sẻ lý do (ví dụ: Chuyển đổi mô hình kinh doanh, nâng cấp cơ sở vật chất, hết hạn thuê mặt bằng...)"
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-600 text-slate-900 transition-all resize-none"
                />
              </div>

              {/* Cam kết checkbox */}
              <label className="flex items-start gap-2.5 cursor-pointer text-xs text-slate-700 pt-1">
                <input
                  type="checkbox"
                  required
                  checked={agreeTerms}
                  onChange={(e) => setAgreeTerms(e.target.checked)}
                  disabled={submitting}
                  className="mt-0.5 rounded border-slate-300 text-rose-600 focus:ring-rose-500"
                />
                <span className="leading-relaxed">
                  Tôi xác nhận nộp thông báo rời nền tảng và cam kết duy trì vận hành đầy đủ cho các lượt đặt chỗ của khách trong thời hạn 30 ngày báo trước.
                </span>
              </label>

              {/* Actions Footer */}
              <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-3">
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
                  disabled={submitting || !agreeTerms || reason.trim().length < 10}
                  className="inline-flex items-center gap-1.5 px-5 py-2.5 text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 active:bg-rose-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
                >
                  {submitting ? (
                    <>
                      <RefreshCw size={14} className="animate-spin" />
                      Đang xử lý...
                    </>
                  ) : (
                    <>
                      <LogOut size={15} />
                      Xác nhận xin rời (30 ngày)
                    </>
                  )}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
};
