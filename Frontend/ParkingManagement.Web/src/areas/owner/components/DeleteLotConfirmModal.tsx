import React, { useState } from 'react';
import { Trash2, AlertTriangle, Building2, MapPin, Loader2 } from 'lucide-react';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';

interface DeleteLotConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  lot: OwnerParkingLotItemDto | null;
  onConfirm: (lotId: number) => Promise<void>;
}

export const DeleteLotConfirmModal: React.FC<DeleteLotConfirmModalProps> = ({
  isOpen,
  onClose,
  lot,
  onConfirm,
}) => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen || !lot) return null;

  const handleDelete = async () => {
    setLoading(true);
    setError(null);
    try {
      await onConfirm(lot.id);
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string; detail?: string } } };
      setError(
        apiErr?.response?.data?.message ||
        apiErr?.response?.data?.detail ||
        (err instanceof Error ? err.message : 'Không thể xóa bãi đỗ xe. Vui lòng thử lại.')
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div className="w-full max-w-md bg-white rounded-3xl shadow-2xl border border-slate-100 overflow-hidden transform animate-in zoom-in-95 duration-200">
        {/* Header với Icon cảnh báo nguy hiểm */}
        <div className="p-6 pb-0 flex items-start gap-4">
          <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center shrink-0 border border-rose-100">
            <Trash2 size={24} />
          </div>
          <div>
            <h3 className="text-lg font-bold text-slate-900">
              Xóa bãi đỗ xe
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Bạn có chắc chắn muốn xóa vĩnh viễn bãi đỗ xe này?
            </p>
          </div>
        </div>

        {/* Thông tin bãi đỗ bị xóa */}
        <div className="p-6 space-y-4">
          <div className="rounded-2xl bg-slate-50 border border-slate-200/80 p-4 space-y-2">
            <div className="flex items-center gap-2 text-sm font-bold text-slate-800">
              <Building2 size={16} className="text-blue-600 shrink-0" />
              <span className="truncate">{lot.name}</span>
            </div>
            <div className="flex items-start gap-2 text-xs text-slate-500">
              <MapPin size={14} className="text-slate-400 shrink-0 mt-0.5" />
              <span className="line-clamp-2">{lot.address}, {lot.city}</span>
            </div>
            <div className="pt-2 border-t border-slate-200/60 flex items-center justify-between text-xs text-slate-600">
              <span>Quy mô: <strong>{lot.totalSlots} chỗ đỗ</strong></span>
              <span>Trạng thái: <strong className="text-slate-700">{lot.status}</strong></span>
            </div>
          </div>

          <div className="rounded-2xl bg-rose-50 border border-rose-100 p-3.5 text-xs text-rose-800 flex items-start gap-2.5">
            <AlertTriangle size={18} className="text-rose-600 shrink-0 mt-0.5" />
            <div>
              <strong className="block mb-0.5 font-bold">Lưu ý quan trọng:</strong>
              Thao tác xóa bãi sẽ gỡ bỏ bãi đỗ khỏi tài khoản của bạn, bao gồm cấu trúc phân tầng (Zone, Floor, Slot) và hồ sơ thẩm định KYB của bãi.
            </div>
          </div>

          {error && (
            <div className="p-3 rounded-xl bg-rose-100 border border-rose-200 text-rose-800 text-xs font-medium">
              {error}
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="p-6 pt-0 flex items-center justify-end gap-3">
          <button
            type="button"
            disabled={loading}
            onClick={onClose}
            className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 text-xs font-bold transition-colors cursor-pointer disabled:opacity-50"
          >
            Hủy bỏ
          </button>
          <button
            type="button"
            disabled={loading}
            onClick={handleDelete}
            className="px-5 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold transition-all shadow-sm hover:shadow flex items-center gap-2 cursor-pointer disabled:opacity-50"
          >
            {loading ? (
              <>
                <Loader2 size={14} className="animate-spin" />
                <span>Đang xóa...</span>
              </>
            ) : (
              <>
                <Trash2 size={14} />
                <span>Xác nhận xóa</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
