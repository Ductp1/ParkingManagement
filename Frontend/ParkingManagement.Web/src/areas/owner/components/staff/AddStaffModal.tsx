import React, { useState } from 'react';
import { X, UserPlus, Mail, User, Building2, AlertCircle, Info, Loader2 } from 'lucide-react';
import type { OwnerLotOptionDto, CreateStaffRequest } from '@/api/ownerStaffApi';

interface AddStaffModalProps {
  isOpen: boolean;
  onClose: () => void;
  lots: OwnerLotOptionDto[];
  onSubmit: (data: CreateStaffRequest) => Promise<void>;
}

export const AddStaffModal: React.FC<AddStaffModalProps> = ({
  isOpen,
  onClose,
  lots,
  onSubmit,
}) => {
  const [fullName, setFullName] = useState('');
  const [contact, setContact] = useState('');
  const [parkingLotId, setParkingLotId] = useState<number | ''>(lots[0]?.id ?? '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!fullName.trim()) {
      setError('Vui lòng nhập họ và tên nhân viên.');
      return;
    }
    if (!contact.trim()) {
      setError('Vui lòng nhập địa chỉ email của nhân viên.');
      return;
    }
    if (!parkingLotId) {
      setError('Vui lòng chọn bãi đỗ xe để phân công.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await onSubmit({
        fullName: fullName.trim(),
        contact: contact.trim(),
        parkingLotId: Number(parkingLotId),
      });
      setFullName('');
      setContact('');
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể thêm nhân viên. Vui lòng thử lại.'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-2xl shadow-2xl border border-slate-100 w-full max-w-lg overflow-hidden flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-slate-50/70">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-blue-100 text-blue-700 flex items-center justify-center font-bold">
              <UserPlus size={18} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Thêm nhân viên trực cổng</h3>
              <p className="text-xs text-slate-500">Cấp quyền vận hành barie và soát vé tại bãi</p>
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

        {/* Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          <div className="p-3.5 rounded-xl bg-blue-50/80 border border-blue-100 text-blue-900 text-xs flex items-start gap-2.5 leading-relaxed">
            <Info size={16} className="shrink-0 mt-0.5 text-blue-600" />
            <div>
              <strong>Quy trình cấp tài khoản:</strong> Nhân viên mới sẽ nhận được email hướng dẫn kích hoạt kèm mật khẩu tạm thời. Nếu nhân viên đã từng làm việc tại bãi khác của bạn, họ sẽ nhận thêm quyền tại bãi này sau khi đăng nhập lại.
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Họ và tên nhân viên <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <User size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                required
                maxLength={150}
                disabled={loading}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="Ví dụ: Nguyễn Văn An"
                className="w-full pl-10 pr-3 py-2.5 text-sm rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all text-slate-900 placeholder:text-slate-400"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Email nhân viên <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <Mail size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="email"
                required
                maxLength={256}
                disabled={loading}
                value={contact}
                onChange={(e) => setContact(e.target.value)}
                placeholder="nhanvien@example.com"
                className="w-full pl-10 pr-3 py-2.5 text-sm rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all text-slate-900 placeholder:text-slate-400"
              />
            </div>
            <p className="text-[11px] text-slate-400 mt-1">Thông tin tài khoản đăng nhập sẽ được gửi tới địa chỉ email này.</p>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              Bãi đỗ xe phân công <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <Building2 size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              {lots.length > 0 ? (
                <select
                  required
                  disabled={loading}
                  value={parkingLotId}
                  onChange={(e) => setParkingLotId(Number(e.target.value))}
                  className="w-full pl-10 pr-3 py-2.5 text-sm rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all text-slate-900 bg-white"
                >
                  <option value="" disabled>-- Chọn bãi đỗ xe --</option>
                  {lots.map((lot) => (
                    <option key={lot.id} value={lot.id}>
                      {lot.name ? `${lot.name} (Mã bãi: #${lot.id})` : `Bãi đỗ #${lot.id}`}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="number"
                  required
                  min={1}
                  disabled={loading}
                  value={parkingLotId}
                  onChange={(e) => setParkingLotId(e.target.value ? Number(e.target.value) : '')}
                  placeholder="Nhập mã ID bãi xe"
                  className="w-full pl-10 pr-3 py-2.5 text-sm rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all text-slate-900 placeholder:text-slate-400"
                />
              )}
            </div>
          </div>

          {/* Actions */}
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
              type="submit"
              disabled={loading}
              className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {loading ? (
                <>
                  <Loader2 size={15} className="animate-spin" />
                  Đang thêm...
                </>
              ) : (
                <>
                  <UserPlus size={15} />
                  Thêm & Phân công
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
