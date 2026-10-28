import React from 'react';
import {
  Clock,
  ShieldCheck,
  AlertTriangle,
  XCircle,
  FileEdit,
  RotateCcw,
} from 'lucide-react';
import type { KybStatus } from '@/types/ownerKyb';

interface KybStatusBannerProps {
  status: KybStatus | string;
  submittedAtUtc?: string | null;
  reviewedAtUtc?: string | null;
  rejectReason?: string | null;
  onResubmitClick?: () => void;
  canResubmit?: boolean;
  onEditClick?: () => void;
  onResetToDraftClick?: () => void;
  isEditMode?: boolean;
}

export const KybStatusBanner: React.FC<KybStatusBannerProps> = ({
  status,
  submittedAtUtc,
  reviewedAtUtc,
  rejectReason,
  onResubmitClick,
  canResubmit = false,
  onEditClick,
  onResetToDraftClick,
  isEditMode = false,
}) => {
  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '';
    try {
      return new Date(isoString).toLocaleString('vi-VN', {
        hour: '2-digit',
        minute: '2-digit',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return isoString;
    }
  };

  switch (status) {
    case 'Submitted':
      return (
        <div className="rounded-2xl border border-amber-200 bg-amber-50/70 p-5 shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-amber-100 text-amber-700 flex items-center justify-center shrink-0">
                <Clock size={20} />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-amber-200 text-amber-900">
                    Đã nộp · Chờ tiếp nhận
                  </span>
                  {submittedAtUtc && (
                    <span className="text-xs text-amber-700 font-medium">
                      Lúc {formatDate(submittedAtUtc)}
                    </span>
                  )}
                </div>
                <h3 className="text-base font-bold text-slate-900 mt-1">
                  Hồ sơ đang chờ Ban Quản Trị tiếp nhận
                </h3>
                <p className="text-xs text-slate-600 mt-0.5">
                  Hồ sơ đang được khóa để bảo toàn dữ liệu trong quá trình chờ xếp lịch thẩm định. Bạn có thể xem lại các tài liệu đã nộp bên dưới.
                </p>
              </div>
            </div>
            <div className="text-right shrink-0">
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white border border-amber-200 text-amber-800 text-xs font-semibold shadow-2xs">
                <span className="w-2 h-2 rounded-full bg-amber-500 animate-pulse" />
                Chờ duyệt
              </span>
            </div>
          </div>
        </div>
      );

    case 'UnderReview':
      return (
        <div className="rounded-2xl border border-blue-200 bg-blue-50/70 p-5 shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-blue-100 text-blue-700 flex items-center justify-center shrink-0">
                <Clock size={20} />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-blue-200 text-blue-900">
                    Đang thẩm định
                  </span>
                  {reviewedAtUtc && (
                    <span className="text-xs text-blue-700 font-medium">
                      Bắt đầu {formatDate(reviewedAtUtc)}
                    </span>
                  )}
                </div>
                <h3 className="text-base font-bold text-slate-900 mt-1">
                  Chuyên viên đang thẩm định hồ sơ của bạn
                </h3>
                <p className="text-xs text-slate-600 mt-0.5">
                  Ban Quản Trị đang kiểm tra đối chiếu pháp lý, giấy phép PCCC và tọa độ hiện trường. Kết quả sẽ được cập nhật sớm.
                </p>
              </div>
            </div>
            <div className="text-right shrink-0">
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white border border-blue-200 text-blue-800 text-xs font-semibold shadow-2xs">
                <span className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
                Đang xử lý
              </span>
            </div>
          </div>
        </div>
      );

    case 'Approved':
      return (
        <div className="rounded-2xl border border-emerald-200 bg-emerald-50/80 p-5 shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-emerald-600 text-white flex items-center justify-center shrink-0 shadow-xs">
                <ShieldCheck size={22} />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-emerald-200 text-emerald-900">
                    Đã phê duyệt đạt chuẩn
                  </span>
                  {reviewedAtUtc && (
                    <span className="text-xs text-emerald-700 font-medium">
                      Ngày {formatDate(reviewedAtUtc)}
                    </span>
                  )}
                </div>
                <h3 className="text-base font-bold text-slate-900 mt-1">
                  Hồ sơ KYB đã được phê duyệt thành công!
                </h3>
                <p className="text-xs text-slate-600 mt-0.5">
                  Bãi đỗ xe đã được chuyển sang trạng thái <strong>Hoạt động (Active)</strong> và mở bán chỗ đỗ công khai trên sàn thương mại điện tử.
                </p>
                {isEditMode ? (
                  <div className="mt-2 inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-emerald-100 text-emerald-800 text-[11px] font-bold">
                    <span>✏️ Đang mở chế độ chỉnh sửa: Bạn có thể thay đổi và tự tải lên tài liệu mới bên dưới.</span>
                  </div>
                ) : null}
              </div>
            </div>
            <div className="flex items-center gap-2 shrink-0 flex-wrap">
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-emerald-600 text-white text-xs font-bold shadow-xs">
                ✓ Bãi xe Active
              </span>
              {onEditClick && (
                <button
                  type="button"
                  onClick={onEditClick}
                  className="px-3.5 py-1.5 rounded-xl bg-white hover:bg-slate-50 border border-emerald-300 text-emerald-900 text-xs font-bold shadow-xs transition-colors cursor-pointer flex items-center gap-1.5"
                >
                  <FileEdit size={14} />
                  {isEditMode ? 'Đóng Chỉnh sửa' : 'Chỉnh sửa / Tải tài liệu mới'}
                </button>
              )}
              {onResetToDraftClick && (
                <button
                  type="button"
                  onClick={onResetToDraftClick}
                  className="px-3.5 py-1.5 rounded-xl bg-rose-50 hover:bg-rose-100 border border-rose-200 text-rose-700 text-xs font-bold shadow-xs transition-colors cursor-pointer flex items-center gap-1.5"
                  title="Xóa bỏ tài liệu mẫu code cứng để tự chọn tệp từ máy tính"
                >
                  <RotateCcw size={14} />
                  Xóa tài liệu mẫu & Tự chọn tệp
                </button>
              )}
            </div>
          </div>
        </div>
      );

    case 'NeedsMoreInfo':
      return (
        <div className="rounded-2xl border border-orange-300 bg-orange-50/80 p-5 shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-orange-100 text-orange-700 flex items-center justify-center shrink-0">
                <AlertTriangle size={20} />
              </div>
              <div>
                <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-orange-200 text-orange-900">
                  Cần bổ sung thông tin
                </span>
                <h3 className="text-base font-bold text-slate-900 mt-1">
                  Ban Quản Trị yêu cầu bổ sung/chỉnh sửa hồ sơ
                </h3>
                {rejectReason && (
                  <div className="mt-2.5 p-3 rounded-xl bg-white border border-orange-200 text-xs text-slate-700">
                    <strong className="text-orange-900 block mb-0.5">Nội dung yêu cầu từ Admin:</strong>
                    {rejectReason}
                  </div>
                )}
                <p className="text-xs text-slate-600 mt-2">
                  Vui lòng cập nhật các mục còn thiếu hoặc sửa đổi theo hướng dẫn phía trên, sau đó bấm nộp lại.
                </p>
              </div>
            </div>
            {canResubmit && onResubmitClick && (
              <button
                type="button"
                onClick={onResubmitClick}
                className="px-4 py-2 bg-orange-600 hover:bg-orange-700 text-white text-xs font-bold rounded-xl shadow-xs transition-all flex items-center gap-1.5 shrink-0 cursor-pointer"
              >
                <RotateCcw size={14} />
                Chỉnh sửa & Nộp lại
              </button>
            )}
          </div>
        </div>
      );

    case 'Rejected':
      return (
        <div className="rounded-2xl border border-red-300 bg-red-50/80 p-5 shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-red-100 text-red-700 flex items-center justify-center shrink-0">
                <XCircle size={20} />
              </div>
              <div>
                <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-red-200 text-red-900">
                  Hồ sơ bị từ chối
                </span>
                <h3 className="text-base font-bold text-slate-900 mt-1">
                  Hồ sơ thẩm định chưa đạt yêu cầu
                </h3>
                {rejectReason && (
                  <div className="mt-2.5 p-3 rounded-xl bg-white border border-red-200 text-xs text-slate-700">
                    <strong className="text-red-900 block mb-0.5">Lý do từ chối:</strong>
                    {rejectReason}
                  </div>
                )}
                <p className="text-xs text-slate-600 mt-2">
                  Bạn có thể điều chỉnh lại thông tin giấy tờ chưa hợp lệ và gửi lại yêu cầu thẩm định bất kỳ lúc nào.
                </p>
              </div>
            </div>
            {canResubmit && onResubmitClick && (
              <button
                type="button"
                onClick={onResubmitClick}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-xs font-bold rounded-xl shadow-xs transition-all flex items-center gap-1.5 shrink-0 cursor-pointer"
              >
                <RotateCcw size={14} />
                Làm lại & Nộp lại
              </button>
            )}
          </div>
        </div>
      );

    default: // Draft or default
      return (
        <div className="rounded-2xl border border-indigo-200 bg-indigo-50/50 p-5 shadow-xs">
          <div className="flex items-start gap-3.5">
            <div className="w-10 h-10 rounded-xl bg-indigo-100 text-indigo-700 flex items-center justify-center shrink-0">
              <FileEdit size={20} />
            </div>
            <div>
              <span className="text-xs font-extrabold uppercase tracking-wider px-2 py-0.5 rounded-full bg-indigo-200 text-indigo-900">
                Bản nháp hồ sơ
              </span>
              <h3 className="text-base font-bold text-slate-900 mt-1">
                Hoàn tất hồ sơ thẩm định KYB 4 bước
              </h3>
              <p className="text-xs text-slate-600 mt-0.5">
                Vui lòng cung cấp đầy đủ thông tin pháp lý, ảnh chụp hiện trường thực tế và giấy chứng nhận an toàn PCCC để bãi đỗ xe đủ điều kiện mở bán trên hệ thống. Bạn có thể bấm <strong>"Lưu nháp"</strong> bất kỳ lúc nào để lưu tiến độ dở dang.
              </p>
            </div>
          </div>
        </div>
      );
  }
};
