import React from 'react';
import { NavLink } from 'react-router-dom';

export const DriverHomePage: React.FC = () => {
  return (
    <div className="w-full">
      {/* Hero Banner */}
      <section className="relative bg-gradient-to-br from-blue-900 via-blue-800 to-indigo-950 text-white py-16 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto flex flex-col lg:flex-row items-center justify-between gap-12">
          <div className="flex flex-col gap-5 max-w-2xl">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-500/20 text-blue-200 border border-blue-400/30 w-fit text-xs font-semibold uppercase tracking-wider">
              <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              NỀN TẢNG ĐỖ XE THÔNG MINH SỐ 1 TP.HCM
            </div>
            <h1 className="text-4xl sm:text-5xl font-extrabold tracking-tight leading-tight">
              Đỗ xe nhanh chóng, <br />
              <span className="text-blue-300">Tự động mở cổng ANPR</span>
            </h1>
            <p className="text-blue-100 text-base sm:text-lg leading-relaxed">
              Tìm bãi đỗ gần nhất, đặt chỗ trước 15 phút, vào/ra bãi không cần chạm thẻ và thanh toán linh hoạt qua ví điện tử.
            </p>
            <div className="flex flex-wrap gap-4 pt-2">
              <NavLink
                to="/driver/vehicles"
                className="px-6 py-3 rounded-xl bg-blue-500 text-white font-semibold hover:bg-blue-600 transition-all shadow-lg flex items-center gap-2"
              >
                <span className="material-symbols-outlined text-[20px]">directions_car</span>
                Quản lý Garage của tôi
              </NavLink>
              <NavLink
                to="/driver/parking-lots"
                className="px-6 py-3 rounded-xl bg-white/10 text-white font-semibold hover:bg-white/20 transition-all border border-white/20 flex items-center gap-2 backdrop-blur-xs"
              >
                <span className="material-symbols-outlined text-[20px]">search</span>
                Tìm bãi & Xem chi tiết
              </NavLink>
            </div>
          </div>

          {/* Quick Telemetry Box */}
          <div className="w-full lg:w-96 bg-white/10 backdrop-blur-md rounded-2xl p-6 border border-white/15 flex flex-col gap-4 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-white/10">
              <span className="text-xs uppercase text-blue-200 font-semibold tracking-wider">Mạng lưới bãi đỗ</span>
              <span className="px-2 py-0.5 rounded bg-emerald-500/30 text-emerald-300 text-xs font-bold">24 Bãi Active</span>
            </div>
            <div className="flex items-center justify-between">
              <div>
                <div className="text-3xl font-extrabold text-white">1.250+</div>
                <div className="text-xs text-blue-200">Vị trí đỗ ô tô sẵn sàng</div>
              </div>
              <div className="text-right">
                <div className="text-3xl font-extrabold text-emerald-300">180+</div>
                <div className="text-xs text-blue-200">Trụ sạc EV siêu nhanh</div>
              </div>
            </div>
            <div className="p-3 bg-white/5 rounded-xl text-xs text-blue-100 flex items-center gap-2 border border-white/10">
              <span className="material-symbols-outlined text-emerald-400 text-[18px]">verified</span>
              <span>Độ trễ nhận diện biển số &lt; 0.3 giây</span>
            </div>
          </div>
        </div>
      </section>

      {/* Feature Highlights */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <div className="text-center max-w-2xl mx-auto mb-12">
          <h2 className="text-2xl sm:text-3xl font-bold text-slate-900">Tính năng nổi bật cho Tài xế</h2>
          <p className="text-slate-500 mt-2 text-sm">Trải nghiệm đỗ xe liền mạch từ lúc chọn bãi đến khi rời cổng</p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs hover:shadow-md transition-shadow">
            <div className="w-12 h-12 rounded-xl bg-blue-100 text-blue-700 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[28px]">garage</span>
            </div>
            <h3 className="text-lg font-bold text-slate-900">Garage Cá nhân (US-009..US-012)</h3>
            <p className="text-sm text-slate-500 mt-2 leading-relaxed">
              Đăng ký tối đa 10 phương tiện cá nhân, tự động chuẩn hóa biển số theo Thông tư 01/2021 và hỗ trợ khai báo xe điện EV/PHEV, xe quá khổ.
            </p>
            <NavLink to="/driver/vehicles" className="inline-flex items-center gap-1 text-sm font-semibold text-blue-600 mt-4 hover:underline">
              Vào Garage ngay &rarr;
            </NavLink>
          </div>

          <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs hover:shadow-md transition-shadow">
            <div className="w-12 h-12 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[28px]">local_parking</span>
            </div>
            <h3 className="text-lg font-bold text-slate-900">Chi tiết Bãi đỗ (US-018)</h3>
            <p className="text-sm text-slate-500 mt-2 leading-relaxed">
              Theo dõi số chỗ trống thời gian thực, bảng giá lũy tiến, chiều cao trần tối đa và các tiện ích (rửa xe, sạc nhanh EV, bảo vệ 24/7).
            </p>
            <NavLink to="/driver/parking-lots" className="inline-flex items-center gap-1 text-sm font-semibold text-emerald-600 mt-4 hover:underline">
              Xem chi tiết bãi &rarr;
            </NavLink>
          </div>

          <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs hover:shadow-md transition-shadow">
            <div className="w-12 h-12 rounded-xl bg-indigo-100 text-indigo-700 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[28px]">qr_code_scanner</span>
            </div>
            <h3 className="text-lg font-bold text-slate-900">Vào / Ra tự động qua Barie</h3>
            <p className="text-sm text-slate-500 mt-2 leading-relaxed">
              Camera ANPR tự động đọc biển số xe trong Garage khi xe tiến tới cổng bãi. Barie nâng tự động không cần bấm thẻ giấy.
            </p>
            <div className="text-xs font-semibold text-indigo-600 mt-4">Tích hợp ANPR & VietQR</div>
          </div>
        </div>
      </section>
    </div>
  );
};
