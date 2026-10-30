import React, { useState } from 'react';
import { X, Flag, AlertCircle, ShieldAlert } from 'lucide-react';
import { ownerFeedbackApi } from '@/api/ownerFeedbackApi';

interface ReportReviewModalProps {
  reviewId: number | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  ownerProfileId: number;
}

const PRESET_REASONS = [
  'Nội dung mang tính xúc phạm, công kích hoặc ngôn từ thô tục',
  'Đánh giá sai sự thật, không đúng với tình hình thực tế bãi xe',
  'Chứa thông tin cá nhân (số điện thoại, tên riêng, biển số xe khác)',
  'Spam hoặc cố tình phá hoại uy tín bãi xe',
  'Lý do khác',
];

export const ReportReviewModal: React.FC<ReportReviewModalProps> = ({
  reviewId,
  isOpen,
  onClose,
  onSuccess,
  ownerProfileId,
}) => {
  const [selectedPreset, setSelectedPreset] = useState(PRESET_REASONS[0]);
  const [customDetail, setCustomDetail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!isOpen || !reviewId) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const fullReason =
      selectedPreset === 'Lý do khác'
        ? customDetail.trim()
        : `${selectedPreset}. ${customDetail.trim()}`.trim();

    if (fullReason.length < 10) {
      setErrorMessage('Lý do khiếu nại phải có tối thiểu 10 ký tự để Admin xác minh.');
      return;
    }

    if (fullReason.length > 500) {
      setErrorMessage('Lý do khiếu nại không được vượt quá 500 ký tự.');
      return;
    }

    setSubmitting(true);
    setErrorMessage(null);

    try {
      await ownerFeedbackApi.reportReview(reviewId, { reason: fullReason }, ownerProfileId);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Có lỗi xảy ra khi gửi báo cáo.';
      setErrorMessage(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-neutral-900/60 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl max-w-lg w-full shadow-2xl border border-neutral-100 overflow-hidden animate-in fade-in zoom-in-95 duration-200">
        <div className="px-6 py-4 border-b border-neutral-100 flex items-center justify-between bg-neutral-50/70">
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-rose-50 text-rose-600 rounded-lg">
              <Flag className="w-5 h-5" />
            </div>
            <div>
              <h3 className="font-bold text-neutral-900 text-base">Khiếu nại đánh giá vi phạm</h3>
              <p className="text-2xs text-neutral-500">Mã đánh giá: #{reviewId}</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 rounded-lg transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {errorMessage && (
            <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div className="bg-amber-50 border border-amber-200 rounded-xl p-3 text-xs text-amber-900 leading-relaxed">
            <p className="font-semibold flex items-center gap-1.5 mb-1 text-amber-950">
              <ShieldAlert className="w-4 h-4 text-amber-600" />
              Quy trình kiểm duyệt đánh giá (US-094 AC2)
            </p>
            Đánh giá bị báo cáo sẽ được chuyển vào hàng đợi kiểm duyệt của Admin. Chủ bãi không thể tự ý gỡ
            đánh giá nhằm đảm bảo tính minh bạch và công bằng cho khách hàng.
          </div>

          <div>
            <label className="text-xs font-bold text-neutral-700 uppercase tracking-wider block mb-2">
              Lý do vi phạm chính
            </label>
            <div className="space-y-1.5">
              {PRESET_REASONS.map((reason) => (
                <label
                  key={reason}
                  className={`flex items-center gap-2.5 p-2.5 rounded-lg border text-xs cursor-pointer transition ${
                    selectedPreset === reason
                      ? 'border-rose-500 bg-rose-50/50 text-rose-900 font-semibold'
                      : 'border-neutral-200 hover:bg-neutral-50 text-neutral-700'
                  }`}
                >
                  <input
                    type="radio"
                    name="reportPreset"
                    checked={selectedPreset === reason}
                    onChange={() => setSelectedPreset(reason)}
                    className="text-rose-600 focus:ring-rose-500"
                  />
                  <span>{reason}</span>
                </label>
              ))}
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-xs font-bold text-neutral-700 uppercase tracking-wider">
                Mô tả chi tiết bằng chứng vi phạm
              </label>
              <span className="text-2xs text-neutral-400">
                {customDetail.length}/500 ký tự
              </span>
            </div>
            <textarea
              rows={3}
              value={customDetail}
              onChange={(e) => setCustomDetail(e.target.value)}
              placeholder="Nhập thêm chi tiết hoặc dẫn chứng để Admin nhanh chóng xem xét gỡ đánh giá..."
              className="w-full text-xs rounded-xl border border-neutral-300 p-3 focus:outline-hidden focus:ring-2 focus:ring-rose-500 focus:border-transparent transition"
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-neutral-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 text-xs font-semibold text-neutral-600 hover:bg-neutral-100 rounded-xl transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-5 py-2 text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-50 rounded-xl shadow-xs transition flex items-center gap-2"
            >
              <Flag className="w-3.5 h-3.5" />
              <span>{submitting ? 'Đang gửi...' : 'Gửi báo cáo vi phạm'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
