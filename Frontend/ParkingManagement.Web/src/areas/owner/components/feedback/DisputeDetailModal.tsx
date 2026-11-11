import React, { useState, useEffect } from 'react';
import {
  X,
  Send,
  Plus,
  Trash2,
  ExternalLink,
  ShieldAlert,
  CheckCircle,
  FileText,
  AlertCircle,
  Image,
  RefreshCw,
} from 'lucide-react';
import { ownerFeedbackApi } from '@/api/ownerFeedbackApi';
import type {
  ComplaintItem,
  ComplaintDetail,
} from '@/types/feedback';
import {
  COMPLAINT_CATEGORY_LABELS,
} from '@/types/feedback';
import { DisputeCountdownBadge } from './DisputeCountdownBadge';

interface DisputeDetailModalProps {
  complaint: ComplaintItem | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (updated: ComplaintDetail) => void;
  ownerProfileId: number;
}

export const DisputeDetailModal: React.FC<DisputeDetailModalProps> = ({
  complaint,
  isOpen,
  onClose,
  onSuccess,
  ownerProfileId,
}) => {
  const [detail, setDetail] = useState<ComplaintDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Form giải trình
  const [responseText, setResponseText] = useState('');
  const [evidenceLinks, setEvidenceLinks] = useState<string[]>(['']);

  useEffect(() => {
    if (!isOpen || !complaint) {
      setDetail(null);
      setResponseText('');
      setEvidenceLinks(['']);
      setErrorMessage(null);
      return;
    }

    let isMounted = true;
    setLoading(true);
    setErrorMessage(null);

    ownerFeedbackApi
      .getComplaintDetail(complaint.code, ownerProfileId)
      .then((data) => {
        if (isMounted) {
          setDetail(data);
          if (data.ownerResponse) {
            setResponseText(data.ownerResponse);
          }
        }
      })
      .catch((err) => {
        if (isMounted) {
          console.warn('Could not fetch detail from server, using item fallback:', err);
          setDetail({
            ...complaint,
            evidenceUrls: [],
          });
        }
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, complaint, ownerProfileId]);

  if (!isOpen || !complaint) return null;

  const currentStatus = detail?.status || complaint.status;
  const isAwaiting = currentStatus === 'AwaitingOwner';

  const handleAddEvidenceLink = () => {
    if (evidenceLinks.length < 5) {
      setEvidenceLinks([...evidenceLinks, '']);
    }
  };

  const handleEvidenceChange = (index: number, value: string) => {
    const updated = [...evidenceLinks];
    updated[index] = value;
    setEvidenceLinks(updated);
  };

  const handleRemoveEvidence = (index: number) => {
    const updated = evidenceLinks.filter((_, i) => i !== index);
    setEvidenceLinks(updated.length > 0 ? updated : ['']);
  };

  const handleSubmitResponse = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = responseText.trim();
    if (trimmed.length < 10) {
      setErrorMessage('Nội dung phản hồi giải trình phải tối thiểu 10 ký tự.');
      return;
    }
    if (trimmed.length > 4000) {
      setErrorMessage('Nội dung phản hồi không được vượt quá 4000 ký tự.');
      return;
    }

    const validUrls = evidenceLinks.map((u) => u.trim()).filter((u) => u.length > 0);

    setSubmitting(true);
    setErrorMessage(null);

    try {
      const updated = await ownerFeedbackApi.respondToComplaint(
        complaint.code,
        {
          response: trimmed,
          evidenceUrls: validUrls,
        },
        ownerProfileId
      );

      setDetail(updated);
      onSuccess(updated);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Có lỗi xảy ra khi gửi phản hồi.';
      setErrorMessage(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const formatVnd = (amount?: number) => {
    if (!amount) return '0 ₫';
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

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-neutral-900/60 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl max-w-2xl w-full shadow-2xl border border-neutral-100 overflow-hidden my-8 animate-in fade-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="px-6 py-4 border-b border-neutral-100 flex items-center justify-between bg-neutral-50/70">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
              <FileText className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-neutral-900 text-lg">
                  Chi tiết tranh chấp {complaint.code}
                </h3>
                {loading && <RefreshCw className="w-3.5 h-3.5 animate-spin text-indigo-500" />}
                <DisputeCountdownBadge
                  status={currentStatus}
                  dueAtUtc={detail?.ownerResponseDueAtUtc ?? complaint.ownerResponseDueAtUtc}
                />
              </div>
              <p className="text-xs text-neutral-500 mt-0.5">
                Mã đặt chỗ: <span className="font-semibold text-neutral-700">{complaint.bookingCode}</span> • Bãi số: #{complaint.parkingLotId}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 rounded-lg transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 max-h-[75vh] overflow-y-auto space-y-6">
          {errorMessage && (
            <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
              <span>{errorMessage}</span>
            </div>
          )}

          {/* Customer claim overview card */}
          <div className="bg-neutral-50 border border-neutral-200/80 rounded-xl p-4 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-neutral-200 pb-2.5">
              <div>
                <span className="text-xs font-semibold text-neutral-500 uppercase tracking-wider block">
                  Phân loại tranh chấp
                </span>
                <span className="text-sm font-bold text-neutral-800">
                  {COMPLAINT_CATEGORY_LABELS[detail?.category ?? complaint.category] || complaint.category}
                </span>
              </div>
              <div className="text-right">
                <span className="text-xs font-semibold text-neutral-500 uppercase tracking-wider block">
                  Yêu cầu bồi hoàn
                </span>
                <span className="text-base font-bold text-rose-600">
                  {formatVnd(detail?.requestedAmount ?? complaint.requestedAmount)}
                </span>
              </div>
            </div>

            <div>
              <span className="text-xs font-semibold text-neutral-500 uppercase tracking-wider block mb-1">
                Nội dung khiếu nại từ khách hàng:
              </span>
              <p className="text-sm text-neutral-800 bg-white p-3 rounded-lg border border-neutral-200/60 leading-relaxed whitespace-pre-wrap">
                {detail?.description || complaint.description}
              </p>
            </div>

            <div className="flex items-center justify-between text-xs text-neutral-500 pt-1">
              <span>Thời gian gửi: {formatDate(detail?.createdAtUtc ?? complaint.createdAtUtc)}</span>
              {detail?.payoutHeld && (
                <span className="text-amber-700 font-semibold bg-amber-100 px-2 py-0.5 rounded-sm">
                  Đã phong tỏa quyết toán
                </span>
              )}
            </div>
          </div>

          {/* Customer evidence if any */}
          {detail?.evidenceUrls && detail.evidenceUrls.length > 0 && (
            <div>
              <h4 className="text-xs font-semibold text-neutral-600 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                <Image className="w-3.5 h-3.5" />
                <span>Bằng chứng từ khách hàng ({detail.evidenceUrls.length})</span>
              </h4>
              <div className="flex flex-wrap gap-2">
                {detail.evidenceUrls.map((url, idx) => (
                  <a
                    key={idx}
                    href={url}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1.5 text-xs text-blue-600 hover:text-blue-800 bg-blue-50 border border-blue-200 px-2.5 py-1 rounded-md"
                  >
                    <span>Bằng chứng #{idx + 1}</span>
                    <ExternalLink className="w-3 h-3" />
                  </a>
                ))}
              </div>
            </div>
          )}

          {/* Form Phản Hồi khi đang AwaitingOwner */}
          {isAwaiting ? (
            <form onSubmit={handleSubmitResponse} className="space-y-4 pt-2 border-t border-neutral-200">
              <div className="bg-indigo-50/50 border border-indigo-100 rounded-xl p-3.5 text-xs text-indigo-900 leading-relaxed">
                <p className="font-semibold flex items-center gap-1.5 mb-1 text-indigo-950">
                  <ShieldAlert className="w-4 h-4 text-indigo-600" />
                  Quyền và trách nhiệm giải trình của chủ bãi (US-089 AC1)
                </p>
                Quý chủ bãi có 48 giờ kể từ lúc khiếu nại được tạo để phản hồi giải trình kèm hình ảnh camera,
                nhật ký barrier hoặc thỏa thuận trực tiếp. Sau 48 giờ, hệ thống sẽ tự động chuyển cấp cho Admin nền tảng xử lý.
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-bold text-neutral-700 uppercase tracking-wider">
                    Nội dung giải trình / Phản hồi của bạn <span className="text-rose-500">*</span>
                  </label>
                  <span
                    className={`text-xs ${
                      responseText.trim().length < 10
                        ? 'text-rose-500 font-semibold'
                        : 'text-neutral-400'
                    }`}
                  >
                    {responseText.length}/4000 ký tự (tối thiểu 10)
                  </span>
                </div>
                <textarea
                  rows={4}
                  value={responseText}
                  onChange={(e) => setResponseText(e.target.value)}
                  placeholder="Nhập chi tiết giải trình của chủ bãi: kết quả kiểm tra camera an ninh, đối chiếu thời gian ra vào cổng, ghi chú của bảo vệ..."
                  className="w-full text-sm rounded-xl border border-neutral-300 p-3 focus:outline-hidden focus:ring-2 focus:ring-indigo-500 focus:border-transparent transition"
                  disabled={submitting}
                />
              </div>

              {/* Bằng chứng kèm theo */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-xs font-bold text-neutral-700 uppercase tracking-wider">
                    Liên kết bằng chứng xác thực (Tối đa 5 links)
                  </label>
                  {evidenceLinks.length < 5 && (
                    <button
                      type="button"
                      onClick={handleAddEvidenceLink}
                      className="text-xs font-semibold text-indigo-600 hover:text-indigo-800 flex items-center gap-1"
                    >
                      <Plus className="w-3.5 h-3.5" />
                      <span>Thêm link</span>
                    </button>
                  )}
                </div>

                <div className="space-y-2">
                  {evidenceLinks.map((link, idx) => (
                    <div key={idx} className="flex items-center gap-2">
                      <input
                        type="url"
                        value={link}
                        onChange={(e) => handleEvidenceChange(idx, e.target.value)}
                        placeholder={`https://drive.google.com/... hoặc link ảnh camera #${idx + 1}`}
                        className="flex-1 text-xs rounded-lg border border-neutral-300 px-3 py-2 focus:ring-2 focus:ring-indigo-500 focus:outline-hidden"
                        disabled={submitting}
                      />
                      {evidenceLinks.length > 1 && (
                        <button
                          type="button"
                          onClick={() => handleRemoveEvidence(idx)}
                          className="p-1.5 text-neutral-400 hover:text-rose-600 rounded-md transition"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-neutral-100">
                <button
                  type="button"
                  onClick={onClose}
                  disabled={submitting}
                  className="px-4 py-2 text-xs font-semibold text-neutral-600 hover:text-neutral-800 hover:bg-neutral-100 rounded-xl transition"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  disabled={submitting || responseText.trim().length < 10}
                  className="px-5 py-2.5 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-xl shadow-xs transition flex items-center gap-2"
                >
                  <Send className="w-4 h-4" />
                  <span>{submitting ? 'Đang gửi...' : 'Gửi giải trình cho nền tảng'}</span>
                </button>
              </div>
            </form>
          ) : (
            /* Khi đã phản hồi hoặc đã giải quyết */
            <div className="pt-3 border-t border-neutral-200 space-y-4">
              <div className="bg-emerald-50 border border-emerald-200 rounded-xl p-4">
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-xs font-bold text-emerald-900 uppercase tracking-wider flex items-center gap-1.5">
                    <CheckCircle className="w-4 h-4 text-emerald-600" />
                    <span>Giải trình của chủ bãi</span>
                  </h4>
                  <span className="text-xs text-emerald-700">
                    {formatDate(detail?.ownerRespondedAtUtc)}
                  </span>
                </div>
                <p className="text-sm text-emerald-950 bg-white/70 p-3 rounded-lg border border-emerald-200/50 leading-relaxed whitespace-pre-wrap">
                  {detail?.ownerResponse || complaint.ownerResponse || 'Chưa có văn bản giải trình.'}
                </p>
              </div>

              {detail?.status === 'Escalated' && (
                <div className="bg-purple-50 border border-purple-200 rounded-xl p-3.5 text-xs text-purple-900">
                  <span className="font-bold block mb-0.5">Trạng thái: Đã chuyển cấp xử lý</span>
                  Vụ việc đã được chuyển giao cho ban thanh tra nền tảng SmartParking để phán quyết.
                </div>
              )}

              <div className="flex justify-end pt-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-xs font-semibold text-neutral-700 bg-neutral-100 hover:bg-neutral-200 rounded-xl transition"
                >
                  Đóng cửa sổ
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
