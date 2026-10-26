import React from 'react';
import { Clock, CheckCircle2, AlertCircle, FileCheck, FileX } from 'lucide-react';
import type { KybStatus } from '@/types/ownerKyb';

interface DocumentStatusBadgeProps {
  kybStatus?: KybStatus | string;
  hasFile: boolean;
  className?: string;
}

/**
 * Huy hiệu trạng thái dành riêng cho từng tài liệu thẩm định (US-054).
 * Hiển thị rõ ràng:
 * - Khi hồ sơ đang nộp/duyệt: "⏳ Đang đợi kiểm tra"
 * - Khi đã duyệt: "✓ Đã thẩm định đạt chuẩn"
 * - Khi bị từ chối/bổ sung: "✕ Cần cập nhật lại"
 * - Khi chưa nộp: "Bản nháp · Sẵn sàng gửi"
 */
export const DocumentStatusBadge: React.FC<DocumentStatusBadgeProps> = ({
  kybStatus,
  hasFile,
  className = '',
}) => {
  if (!hasFile) {
    return (
      <span
        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-semibold bg-slate-100 text-slate-500 border border-slate-200 ${className}`}
      >
        <FileX size={13} className="text-slate-400" />
        Chưa có tài liệu
      </span>
    );
  }

  // Khi hồ sơ đã nộp và đang chờ/đang được thẩm định
  if (kybStatus === 'Submitted' || kybStatus === 'UnderReview') {
    return (
      <span
        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold bg-amber-50 text-amber-900 border border-amber-300 shadow-2xs ${className}`}
      >
        <Clock size={13} className="text-amber-600 animate-pulse" />
        <span>⏳ Đang đợi kiểm tra</span>
      </span>
    );
  }

  // Khi hồ sơ đã được duyệt thành công
  if (kybStatus === 'Approved') {
    return (
      <span
        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold bg-emerald-50 text-emerald-800 border border-emerald-300 shadow-2xs ${className}`}
      >
        <CheckCircle2 size={13} className="text-emerald-600" />
        <span>✓ Đã thẩm định đạt chuẩn</span>
      </span>
    );
  }

  // Khi hồ sơ bị từ chối hoặc yêu cầu bổ sung
  if (kybStatus === 'Rejected' || kybStatus === 'NeedsMoreInfo') {
    return (
      <span
        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold bg-rose-50 text-rose-800 border border-rose-300 shadow-2xs ${className}`}
      >
        <AlertCircle size={13} className="text-rose-600" />
        <span>✕ Cần kiểm tra / sửa đổi</span>
      </span>
    );
  }

  // Mặc định (Bản nháp Draft - vừa chọn file)
  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold bg-indigo-50 text-indigo-700 border border-indigo-200 ${className}`}
    >
      <FileCheck size={13} className="text-indigo-600" />
      <span>Bản nháp · Sẵn sàng gửi</span>
    </span>
  );
};
