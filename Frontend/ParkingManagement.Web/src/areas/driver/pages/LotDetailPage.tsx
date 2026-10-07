import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { parkingApi } from '@/api/parkingApi';
import type { ParkingLotDto } from '@/types/parking';

export const LotDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const lotId = id ? parseInt(id, 10) : 1;

  const [lot, setLot] = useState<ParkingLotDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    setIsLoading(true);
    setError(null);

    parkingApi
      .getById(lotId)
      .then((data) => {
        if (isMounted) {
          setLot(data);
          setIsLoading(false);
        }
      })
      .catch((err) => {
        if (isMounted) {
          // If backend isn't up, fallback to standard mock lot for demo UI preview
          setError(err.message);
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [lotId]);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* Breadcrumb & Header */}
      <div>
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-100 text-emerald-800 text-xs font-bold uppercase tracking-wider mb-2">
          📍 BÃI ĐỖ XE ĐỐI TÁC • THÔNG TIN THỜI GIAN THỰC (US-018)
        </div>
        <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
          {lot ? lot.name : 'Bãi đỗ xe Landmark 81 Central'}
        </h1>
        <p className="text-slate-500 text-sm mt-1 flex items-center gap-1.5">
          <span className="material-symbols-outlined text-[18px] text-slate-400">location_on</span>
          {lot ? lot.address : '720A Điện Biên Phủ, Phường 22, Bình Thạnh, TP.HCM'}
        </p>
      </div>

      {error && (
        <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-xs text-amber-800 flex items-center gap-2">
          <span className="material-symbols-outlined text-[18px] text-amber-600">info</span>
          <span>
            Thông báo: Không thể tải từ backend ({error}). Đang hiển thị bản xem trước giao diện US-018.
          </span>
        </div>
      )}

      {isLoading ? (
        <div className="py-12 flex justify-center items-center text-slate-400">
          <div className="w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Main Info */}
          <div className="lg:col-span-2 flex flex-col gap-6">
            {/* Gallery Image */}
            <div className="relative h-80 rounded-2xl overflow-hidden shadow-md">
              <img
                src="https://images.unsplash.com/photo-1506521781263-d8422e82f27a?auto=format&fit=crop&w=1200&q=80"
                alt="Parking Lot"
                className="w-full h-full object-cover"
              />
              <div className="absolute top-4 right-4 flex items-center gap-2">
                <span className="px-3 py-1 rounded-full bg-emerald-600 text-white text-xs font-bold shadow-md flex items-center gap-1">
                  <span className="w-2 h-2 rounded-full bg-white animate-pulse"></span>
                  Đang mở cửa (24/7)
                </span>
              </div>
            </div>

            {/* Quick Metrics */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
                <span className="text-[11px] text-slate-400 font-semibold uppercase block">Chỗ trống hiện tại</span>
                <span className="text-2xl font-extrabold text-emerald-600 mt-1 block">
                  {lot?.availableSlots ?? 32}{' '}
                  <span className="text-xs font-normal text-slate-400">/ {lot?.totalSlots ?? 120}</span>
                </span>
              </div>

              <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
                <span className="text-[11px] text-slate-400 font-semibold uppercase block">Chiều cao giới hạn</span>
                <span className="text-2xl font-extrabold text-slate-900 mt-1 block">
                  {lot?.maxHeightCm ?? 220} <span className="text-xs font-normal text-slate-400">cm</span>
                </span>
              </div>

              <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
                <span className="text-[11px] text-slate-400 font-semibold uppercase block">Đánh giá khách hàng</span>
                <span className="text-2xl font-extrabold text-amber-500 mt-1 block flex items-center gap-1">
                  4.9 <span className="material-symbols-outlined text-[18px]">star</span>
                </span>
              </div>

              <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
                <span className="text-[11px] text-slate-400 font-semibold uppercase block">Hotline chung (AC2)</span>
                <span className="text-base font-extrabold text-blue-600 mt-1 block">
                  1900 6868
                </span>
              </div>
            </div>

            {/* Amenities & Attributes */}
            <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs">
              <h3 className="text-base font-bold text-slate-900 mb-4">Tiện ích & Thuộc tính bãi</h3>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-emerald-600">ev_station</span>
                  Trạm sạc EV Siêu nhanh
                </div>
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-blue-600">videocam</span>
                  Camera ANPR giám sát 24/7
                </div>
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-amber-600">local_car_wash</span>
                  Dịch vụ rửa xe thông minh
                </div>
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-purple-600">roofing</span>
                  Hầm đỗ xe mái che 100%
                </div>
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-red-600">fire_extinguisher</span>
                  Hệ thống PCCC tự động
                </div>
                <div className="flex items-center gap-2.5 p-3 rounded-xl bg-slate-50 border border-slate-100 text-xs font-semibold text-slate-700">
                  <span className="material-symbols-outlined text-[20px] text-indigo-600">security</span>
                  Bảo vệ chuyên nghiệp 24/24
                </div>
              </div>
            </div>
          </div>

          {/* Pricing & Booking Card */}
          <div className="flex flex-col gap-6">
            <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-md flex flex-col gap-5 sticky top-24">
              <div>
                <span className="text-xs uppercase text-slate-400 font-bold tracking-wider">Bảng giá tiêu chuẩn</span>
                <div className="flex items-baseline gap-1 mt-1">
                  <span className="text-3xl font-extrabold text-blue-600">20.000đ</span>
                  <span className="text-xs text-slate-500 font-medium">/ 2 giờ đầu</span>
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Miễn phí 15 phút đầu (Grace Period US-046). Sau 2 giờ: 10.000đ / giờ tiếp theo.
                </p>
              </div>

              <div className="p-3.5 rounded-xl bg-blue-50 border border-blue-100 text-xs text-blue-800 space-y-1">
                <div className="font-bold flex items-center gap-1">
                  <span className="material-symbols-outlined text-[16px]">verified</span>
                  Chính sách giữ chỗ 15 phút
                </div>
                <div>Chỗ đỗ được đảm bảo giữ riêng cho xe của bạn kể từ thời điểm bấm đặt chỗ.</div>
              </div>

              <button
                type="button"
                className="w-full py-3.5 rounded-xl bg-blue-600 text-white font-bold hover:bg-blue-700 shadow-md transition-all flex items-center justify-center gap-2"
              >
                <span className="material-symbols-outlined text-[20px]">calendar_month</span>
                Đặt chỗ giữ slot ngay
              </button>

              <div className="text-center text-[11px] text-slate-400">
                Thanh toán online qua VNPAY / VietQR khi rời cổng
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
