import React, { useState } from 'react';
import {
  Star,
  MessageSquare,
  Flag,
  Send,
  CornerDownRight,
  ShieldAlert,
  AlertCircle,
} from 'lucide-react';
import { ownerFeedbackApi } from '@/api/ownerFeedbackApi';
import type { ReviewItem } from '@/types/feedback';

interface ReviewCardProps {
  review: ReviewItem;
  onReplySuccess: (reviewId: number, reply: string) => void;
  onReportClick: (reviewId: number) => void;
  ownerProfileId: number;
}

export const ReviewCard: React.FC<ReviewCardProps> = ({
  review,
  onReplySuccess,
  onReportClick,
  ownerProfileId,
}) => {
  const [isReplying, setIsReplying] = useState(false);
  const [replyText, setReplyText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

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

  const handleSendReply = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = replyText.trim();
    if (trimmed.length < 5) {
      setErrorMessage('Nội dung phản hồi phải tối thiểu 5 ký tự.');
      return;
    }
    if (trimmed.length > 2000) {
      setErrorMessage('Nội dung phản hồi không được vượt quá 2000 ký tự.');
      return;
    }

    setSubmitting(true);
    setErrorMessage(null);

    try {
      await ownerFeedbackApi.replyToReview(
        review.id,
        { reply: trimmed },
        ownerProfileId
      );
      onReplySuccess(review.id, trimmed);
      setIsReplying(false);
      setReplyText('');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Có lỗi xảy ra khi gửi phản hồi.';
      setErrorMessage(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-neutral-200 p-5 shadow-2xs hover:shadow-xs transition space-y-4">
      {/* Top Header */}
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-indigo-100 text-indigo-700 font-bold flex items-center justify-center text-sm shadow-2xs">
            {review.reviewerName ? review.reviewerName.charAt(0).toUpperCase() : 'K'}
          </div>
          <div>
            <h4 className="font-bold text-neutral-900 text-sm">{review.reviewerName}</h4>
            <div className="flex items-center gap-1.5 mt-0.5">
              <div className="flex items-center">
                {[1, 2, 3, 4, 5].map((s) => (
                  <Star
                    key={s}
                    className={`w-3.5 h-3.5 ${
                      s <= review.rating
                        ? 'fill-amber-400 text-amber-400'
                        : 'text-neutral-200 fill-neutral-100'
                    }`}
                  />
                ))}
              </div>
              <span className="text-2xs text-neutral-400">•</span>
              <span className="text-2xs text-neutral-500">{formatDate(review.createdAtUtc)}</span>
            </div>
          </div>
        </div>

        {/* Flag status pill if reported */}
        {review.isFlagged && (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
            <ShieldAlert className="w-3.5 h-3.5 text-rose-600" />
            Đang chờ Admin duyệt khiếu nại
          </span>
        )}
      </div>

      {/* Customer Comment */}
      <p className="text-sm text-neutral-800 leading-relaxed whitespace-pre-wrap pl-1">
        {review.comment || <span className="italic text-neutral-400">Không có nhận xét chi tiết.</span>}
      </p>

      {/* Existing Owner Reply */}
      {review.ownerReply ? (
        <div className="bg-neutral-50 border border-neutral-200/80 rounded-xl p-4 ml-2 md:ml-6 space-y-1.5">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-indigo-700 flex items-center gap-1.5">
              <CornerDownRight className="w-3.5 h-3.5" />
              Phản hồi từ chủ bãi
            </span>
            <span className="text-2xs text-neutral-400">
              {formatDate(review.ownerRepliedAtUtc)}
            </span>
          </div>
          <p className="text-xs text-neutral-800 leading-relaxed whitespace-pre-wrap pt-0.5">
            {review.ownerReply}
          </p>
        </div>
      ) : null}

      {/* Inline Reply Form */}
      {isReplying && !review.ownerReply && (
        <form onSubmit={handleSendReply} className="pt-2 border-t border-neutral-100 space-y-3">
          {errorMessage && (
            <div className="p-2.5 bg-red-50 border border-red-200 text-red-700 rounded-lg text-xs flex items-center gap-2">
              <AlertCircle className="w-3.5 h-3.5 shrink-0 text-red-500" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div>
            <div className="flex items-center justify-between mb-1">
              <span className="text-2xs font-bold text-neutral-600 uppercase tracking-wider">
                Viết phản hồi công khai (Mỗi đánh giá 1 lần)
              </span>
              <span
                className={`text-2xs ${
                  replyText.trim().length < 5 ? 'text-rose-500' : 'text-neutral-400'
                }`}
              >
                {replyText.length}/2000 ký tự
              </span>
            </div>
            <textarea
              rows={3}
              value={replyText}
              onChange={(e) => setReplyText(e.target.value)}
              placeholder="Cảm ơn quý khách đã sử dụng dịch vụ và đóng góp ý kiến..."
              className="w-full text-xs rounded-xl border border-neutral-300 p-3 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
              disabled={submitting}
            />
          </div>

          <div className="flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => {
                setIsReplying(false);
                setErrorMessage(null);
              }}
              disabled={submitting}
              className="px-3 py-1.5 text-xs text-neutral-600 hover:bg-neutral-100 rounded-lg transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting || replyText.trim().length < 5}
              className="px-4 py-1.5 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg shadow-2xs transition flex items-center gap-1.5"
            >
              <Send className="w-3 h-3" />
              <span>{submitting ? 'Đang gửi...' : 'Gửi phản hồi'}</span>
            </button>
          </div>
        </form>
      )}

      {/* Action Footer (when not replying and no reply yet) */}
      {!review.ownerReply && !isReplying && (
        <div className="flex items-center justify-between pt-2 border-t border-neutral-100 text-xs">
          <button
            type="button"
            onClick={() => setIsReplying(true)}
            className="font-semibold text-indigo-600 hover:text-indigo-800 flex items-center gap-1.5 transition py-1"
          >
            <MessageSquare className="w-3.5 h-3.5" />
            <span>Phản hồi khách hàng</span>
          </button>

          {!review.isFlagged && (
            <button
              type="button"
              onClick={() => onReportClick(review.id)}
              className="text-neutral-400 hover:text-rose-600 flex items-center gap-1 transition py-1 text-2xs"
              title="Báo cáo đánh giá vi phạm gửi Admin"
            >
              <Flag className="w-3 h-3" />
              <span>Khiếu nại vi phạm</span>
            </button>
          )}
        </div>
      )}
    </div>
  );
};
