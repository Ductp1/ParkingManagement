import React, { useState } from 'react';
import { X, Building2, MapPin, AlertCircle, Plus } from 'lucide-react';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import type { CreateParkingLotRequest } from '@/types/ownerKyb';

interface RegisterLotModalProps {
  isOpen: boolean;
  onClose: () => void;
  ownerProfileId: number;
  onSuccess: (newLotId: number, lotName: string) => void;
}

export const RegisterLotModal: React.FC<RegisterLotModalProps> = ({
  isOpen,
  onClose,
  ownerProfileId,
  onSuccess,
}) => {
  const [name, setName] = useState('');
  const [address, setAddress] = useState('');
  const [city, setCity] = useState('TP. Hồ Chí Minh');
  const [district, setDistrict] = useState('Quận 1');
  const [latitude, setLatitude] = useState<number>(10.776889);
  const [longitude, setLongitude] = useState<number>(106.700806);
  const [totalSlots, setTotalSlots] = useState<number>(60);
  const [maxHeightCm, setMaxHeightCm] = useState<number>(220);
  const [openTime, setOpenTime] = useState('06:00');
  const [closeTime, setCloseTime] = useState('23:00');
  const [hotlinePhone, setHotlinePhone] = useState('1900 6868');
  const [description, setDescription] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  if (!isOpen) return null;

  const handleGetLocation = () => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLatitude(parseFloat(pos.coords.latitude.toFixed(6)));
        setLongitude(parseFloat(pos.coords.longitude.toFixed(6)));
      },
      (err) => {
        alert(`Không thể lấy vị trí: ${err.message}`);
      }
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (busy) return;
    setError('');

    if (!name.trim()) {
      setError('Vui lòng nhập tên bãi đỗ xe.');
      return;
    }
    if (!address.trim()) {
      setError('Vui lòng nhập địa chỉ cụ thể.');
      return;
    }
    if (totalSlots <= 0) {
      setError('Tổng số chỗ đỗ phải lớn hơn 0.');
      return;
    }

    setBusy(true);
    try {
      const payload: CreateParkingLotRequest = {
        ownerProfileId,
        name: name.trim(),
        address: address.trim(),
        city: city.trim(),
        district: district.trim(),
        latitude,
        longitude,
        totalSlots,
        maxHeightCm,
        openTime: openTime.length === 5 ? `${openTime}:00` : openTime,
        closeTime: closeTime.length === 5 ? `${closeTime}:00` : closeTime,
        integrationTier: 'Manual',
        hotlinePhone: hotlinePhone.trim() || undefined,
        description: description.trim() || undefined,
      };

      const result = await ownerLotsApi.createParkingLot(payload);
      onSuccess(result.id, name.trim());
      onClose();
    } catch (err: unknown) {
      const message =
        err instanceof Error
          ? err.message
          : 'Không thể đăng ký bãi đỗ xe. Vui lòng kiểm tra lại thông tin.';
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs">
      <div className="relative w-full max-w-2xl bg-white rounded-3xl shadow-2xl border border-slate-200 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header Modal */}
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/70">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-indigo-600 text-white flex items-center justify-center font-bold">
              <Building2 size={18} />
            </div>
            <div>
              <h3 className="text-base font-extrabold text-slate-900 leading-tight">
                Đăng ký Bãi đỗ xe mới (US-053)
              </h3>
              <p className="text-[11px] text-slate-500">
                Khai báo cơ sở hạ tầng để tiến hành nộp hồ sơ thẩm định KYB
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 flex items-center justify-center transition-colors cursor-pointer"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body Modal Form */}
        <form onSubmit={handleSubmit} className="p-6 overflow-y-auto space-y-4">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-xs text-rose-800 flex items-start gap-2">
              <AlertCircle size={16} className="text-rose-600 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {/* Tên bãi đỗ */}
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              Tên bãi đỗ xe <span className="text-rose-600">*</span>
            </label>
            <input
              required
              disabled={busy}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="VD: Bãi Đỗ Xe Tòa Nhà Landmark 81"
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-indigo-600 focus:ring-2 focus:ring-indigo-500/20"
            />
          </div>

          {/* Địa chỉ & Tỉnh thành */}
          <div className="grid grid-cols-1 sm:grid-cols-4 gap-3">
            <div className="sm:col-span-2">
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Địa chỉ chi tiết <span className="text-rose-600">*</span>
              </label>
              <input
                required
                disabled={busy}
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                placeholder="VD: 720A Điện Biên Phủ, Phường 22"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-indigo-600"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Quận / Huyện
              </label>
              <input
                disabled={busy}
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
                placeholder="VD: Bình Thạnh"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-indigo-600"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Tỉnh / Thành phố
              </label>
              <input
                disabled={busy}
                value={city}
                onChange={(e) => setCity(e.target.value)}
                placeholder="TP. Hồ Chí Minh"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 focus:outline-hidden focus:border-indigo-600"
              />
            </div>
          </div>

          {/* Tọa độ GPS */}
          <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                <MapPin size={14} className="text-indigo-600" />
                Tọa độ GPS bãi đỗ xe <span className="text-rose-600">*</span>
              </span>
              <button
                type="button"
                onClick={handleGetLocation}
                className="text-[11px] font-semibold text-indigo-600 hover:underline cursor-pointer"
              >
                Lấy GPS thiết bị
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <span className="text-[10px] text-slate-500 block mb-0.5">Vĩ độ (Latitude)</span>
                <input
                  type="number"
                  step="0.000001"
                  required
                  disabled={busy}
                  value={latitude}
                  onChange={(e) => setLatitude(parseFloat(e.target.value) || 0)}
                  className="w-full px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-mono"
                />
              </div>
              <div>
                <span className="text-[10px] text-slate-500 block mb-0.5">Kinh độ (Longitude)</span>
                <input
                  type="number"
                  step="0.000001"
                  required
                  disabled={busy}
                  value={longitude}
                  onChange={(e) => setLongitude(parseFloat(e.target.value) || 0)}
                  className="w-full px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-mono"
                />
              </div>
            </div>
            <p className="text-[10px] text-slate-400 mt-1.5">
              Tọa độ này sẽ dùng để đối chiếu khoảng cách &le; 500m với ảnh chụp hiện trường tại Bước 2 KYB.
            </p>
          </div>

          {/* Quy mô & Giới hạn kỹ thuật */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Tổng số chỗ <span className="text-rose-600">*</span>
              </label>
              <input
                type="number"
                min={1}
                required
                disabled={busy}
                value={totalSlots}
                onChange={(e) => setTotalSlots(parseInt(e.target.value, 10) || 0)}
                className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold"
              />
              <span className="text-[10px] text-slate-400 block mt-0.5">
                {totalSlots > 50 ? '⚡ Bãi lớn (>50 chỗ)' : 'Bãi vừa/nhỏ (≤50)'}
              </span>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Chiều cao tối đa (cm)
              </label>
              <input
                type="number"
                min={150}
                max={500}
                disabled={busy}
                value={maxHeightCm}
                onChange={(e) => setMaxHeightCm(parseInt(e.target.value, 10) || 220)}
                className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold"
              />
              <span className="text-[10px] text-slate-400 block mt-0.5">
                Chuẩn: 220 cm
              </span>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Mở cửa
              </label>
              <div className="relative">
                <input
                  type="time"
                  disabled={busy}
                  value={openTime}
                  onChange={(e) => setOpenTime(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Đóng cửa
              </label>
              <div className="relative">
                <input
                  type="time"
                  disabled={busy}
                  value={closeTime}
                  onChange={(e) => setCloseTime(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold"
                />
              </div>
            </div>
          </div>

          {/* Hotline & Mô tả */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Số Hotline bãi xe
              </label>
              <input
                disabled={busy}
                value={hotlinePhone}
                onChange={(e) => setHotlinePhone(e.target.value)}
                placeholder="VD: 1900 6868"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Ghi chú / Mô tả ngắn
              </label>
              <input
                disabled={busy}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="VD: Gần cổng vào trung tâm thương mại"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900"
              />
            </div>
          </div>

          {/* Footer nút hành động */}
          <div className="pt-4 border-t border-slate-100 flex items-center justify-end gap-3">
            <button
              type="button"
              disabled={busy}
              onClick={onClose}
              className="px-5 py-2.5 rounded-xl border border-slate-200 text-xs font-bold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={busy}
              className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-60"
            >
              <Plus size={16} />
              {busy ? 'Đang tạo bãi xe…' : 'Đăng ký Bãi Đỗ Xe'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
