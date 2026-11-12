import React, { useState } from 'react';
import { AlertTriangle, X, Loader2, ShieldOff } from 'lucide-react';
import type { StaffItemDto } from '@/api/ownerStaffApi';

interface RevokeStaffModalProps {
  isOpen: boolean;
  onClose: () => void;
  staff: StaffItemDto | null;
  lotName: string;
  onConfirm: (staffId: number) => Promise<void>;
}

export const RevokeStaffModal: React.FC<RevokeStaffModalProps> = ({
  isOpen,
  onClose,
  staff,
  lotName,
  onConfirm,
}) => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen || !staff) return null;

  const handleConfirm = async () => {
    setLoading(true);
    setError(null);
    try {
      await onConfirm(staff.id);
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể gỡ quyền nhân viên. Vui lòng thử lại.'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-2xl shadow-2xl border border-slate-100 w-full max-w-md overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-rose-50/50">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-rose-100 text-rose-700 flex items-center justify-center font-bold">
              <AlertTriangle size={18} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Xác nhận gỡ quyền nhân viên</h3>
              <p className="text-xs text-rose-600 font-medium">Hành động này có hiệu lực ngay lập tức</p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={loading}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        <div className="p-6 space-y-4">
          {error && (
            <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs">
              {error}
            </div>
          )}

          <p className="text-sm text-slate-600 leading-relaxed">
            Bạn có chắc chắn muốn gỡ quyền quản lý cổng của nhân viên{' '}
            <strong className="text-slate-900 font-bold">{staff.fullName}</strong> tại{' '}
            <strong className="text-slate-900 font-bold">{lotName}</strong>?
          </p>

          <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs space-y-1.5">
            <div className="font-semibold flex items-center gap-1.5">
              <ShieldOff size={14} className="shrink-0 text-amber-700" />
              Lưu ý quan trọng:
            </div>
            <ul className="list-disc pl-4 space-y-1 text-amber-700">
              <li>Phiên làm việc hiện tại của nhân viên tại bãi này sẽ bị thu hồi ngay lập tức.</li>
              <li>Nhân viên sẽ không còn quyền truy cập Bàn điều khiển Barie của bãi xe này.</li>
              <li>Bạn luôn có thể phân công lại nhân viên này vào bãi bất cứ lúc nào.</li>
            </ul>
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              onClick={handleConfirm}
              disabled={loading}
              className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-semibold text-white bg-rose-600 hover:bg-rose-700 active:bg-rose-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {loading ? (
                <>
                  <Loader2 size={15} className="animate-spin" />
                  Đang xử lý...
                </>
              ) : (
                <>
                  <ShieldOff size={15} />
                  Gỡ quyền ngay
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
