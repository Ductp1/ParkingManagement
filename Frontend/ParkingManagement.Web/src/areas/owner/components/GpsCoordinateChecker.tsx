import React from 'react';
import { MapPin, AlertTriangle, CheckCircle2, Navigation, Copy } from 'lucide-react';
import { calculateHaversineDistanceMeters } from '@/utils/haversine';

interface GpsCoordinateCheckerProps {
  photoLat: number | null;
  photoLng: number | null;
  lotLat: number;
  lotLng: number;
  onChange: (lat: number | null, lng: number | null) => void;
  disabled?: boolean;
}

export const GpsCoordinateChecker: React.FC<GpsCoordinateCheckerProps> = ({
  photoLat,
  photoLng,
  lotLat,
  lotLng,
  onChange,
  disabled = false,
}) => {
  const hasCoordinates =
    photoLat !== null &&
    photoLng !== null &&
    !isNaN(photoLat) &&
    !isNaN(photoLng) &&
    photoLat !== 0 &&
    photoLng !== 0;

  const distanceMeters = hasCoordinates
    ? calculateHaversineDistanceMeters(lotLat, lotLng, photoLat, photoLng)
    : null;

  const isWithinBoundary = distanceMeters !== null && distanceMeters <= 500;

  // Lấy vị trí GPS hiện tại từ trình duyệt
  const handleGetCurrentLocation = () => {
    if (disabled || !navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        onChange(
          parseFloat(pos.coords.latitude.toFixed(6)),
          parseFloat(pos.coords.longitude.toFixed(6))
        );
      },
      (err) => {
        alert(`Không thể lấy vị trí thiết bị: ${err.message}`);
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  };

  // Đồng bộ theo tọa độ bãi đỗ
  const handleSyncLotCoordinates = () => {
    if (disabled) return;
    onChange(lotLat, lotLng);
  };

  return (
    <div className="rounded-2xl border border-slate-200/90 bg-white p-5 shadow-xs">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-3">
        <div>
          <div className="flex items-center gap-2">
            <h4 className="text-sm font-bold text-slate-900 flex items-center gap-1.5">
              <MapPin size={16} className="text-indigo-600" />
              Đối chiếu Tọa độ GPS Ảnh Hiện Trường
            </h4>
            <span className="text-[10px] font-extrabold uppercase px-1.5 py-0.5 rounded bg-rose-50 text-rose-700 border border-rose-200">
              Biên ≤ 500m
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-0.5">
            Tọa độ trích xuất từ ảnh hoặc vị trí chụp phải nằm trong bán kính <strong>500 mét</strong> so với tọa độ bãi xe đã đăng ký.
          </p>
        </div>

        {/* Nút tác vụ nhanh */}
        {!disabled && (
          <div className="flex items-center gap-1.5 shrink-0">
            <button
              type="button"
              onClick={handleGetCurrentLocation}
              className="px-2.5 py-1 rounded-lg bg-indigo-50 border border-indigo-200 text-indigo-700 hover:bg-indigo-100 text-xs font-semibold flex items-center gap-1 transition-colors cursor-pointer"
            >
              <Navigation size={12} />
              GPS Hiện tại
            </button>
            <button
              type="button"
              onClick={handleSyncLotCoordinates}
              className="px-2.5 py-1 rounded-lg bg-slate-100 border border-slate-200 text-slate-700 hover:bg-slate-200 text-xs font-semibold flex items-center gap-1 transition-colors cursor-pointer"
            >
              <Copy size={12} />
              Dùng tọa độ bãi
            </button>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-3">
        <div>
          <label className="block text-xs font-bold text-slate-700 mb-1">
            Vĩ độ ảnh (Latitude)
          </label>
          <input
            type="number"
            step="0.000001"
            disabled={disabled}
            value={photoLat !== null ? photoLat : ''}
            onChange={(e) => {
              const val = e.target.value === '' ? null : parseFloat(e.target.value);
              onChange(val, photoLng);
            }}
            placeholder={`VD: ${lotLat.toFixed(6)}`}
            className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-mono bg-slate-50/50 focus:bg-white focus:outline-hidden focus:border-indigo-600 disabled:opacity-60"
          />
        </div>

        <div>
          <label className="block text-xs font-bold text-slate-700 mb-1">
            Kinh độ ảnh (Longitude)
          </label>
          <input
            type="number"
            step="0.000001"
            disabled={disabled}
            value={photoLng !== null ? photoLng : ''}
            onChange={(e) => {
              const val = e.target.value === '' ? null : parseFloat(e.target.value);
              onChange(photoLat, val);
            }}
            placeholder={`VD: ${lotLng.toFixed(6)}`}
            className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs font-mono bg-slate-50/50 focus:bg-white focus:outline-hidden focus:border-indigo-600 disabled:opacity-60"
          />
        </div>
      </div>

      {/* Tọa độ bãi xe tham chiếu & Kết quả kiểm tra Haversine */}
      <div className="mt-4 pt-3.5 border-t border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
        <div className="text-slate-500 font-mono text-[11px]">
          Tọa độ bãi tham chiếu: <span className="font-bold text-slate-700">{lotLat.toFixed(5)}, {lotLng.toFixed(5)}</span>
        </div>

        <div>
          {hasCoordinates && distanceMeters !== null ? (
            isWithinBoundary ? (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold text-xs">
                <CheckCircle2 size={14} className="text-emerald-600" />
                Vị trí hợp lệ (~{distanceMeters} m so với bãi xe)
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-rose-50 text-rose-700 border border-rose-200 font-bold text-xs animate-pulse">
                <AlertTriangle size={14} className="text-rose-600" />
                Lệch {distanceMeters} m (&gt; 500 m) – Vượt quá bán kính cho phép!
              </span>
            )
          ) : (
            <span className="text-slate-400 italic">
              Chưa nhập tọa độ ảnh để đối chiếu
            </span>
          )}
        </div>
      </div>
    </div>
  );
};
