import React from 'react';

export const MainFooter: React.FC = () => {
  return (
    <footer className="bg-slate-900 text-slate-300 py-7 border-t border-slate-800 mt-auto">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-6 items-start">
          {/* Brand */}
          <div className="lg:col-span-4 flex flex-col gap-2.5">
            <div className="flex items-center gap-2">
              <div className="w-7 h-7 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-sm">
                P
              </div>
              <span className="font-bold text-base tracking-tight text-white">ParkMaster</span>
              <span className="ml-1 inline-flex items-center gap-1.5 text-[11px] text-emerald-300 font-semibold">
                <span className="flex h-2 w-2 rounded-full bg-emerald-400"></span>
                ANPR Online · 24 Bãi TP.HCM
              </span>
            </div>
            <p className="text-xs text-slate-400 leading-relaxed max-w-sm">
              Nền tảng đỗ xe thông minh ứng dụng IoT &amp; AI nhận diện biển số ANPR tự động.
            </p>
          </div>

          {/* Dịch vụ */}
          <div className="lg:col-span-3 flex flex-col gap-2">
            <h3 className="font-semibold text-white text-xs uppercase tracking-wider">Dịch vụ</h3>
            <ul className="flex flex-col gap-1.5 text-xs text-slate-400">
              <li><span className="hover:text-white transition-colors cursor-pointer">Tìm bãi &amp; Đặt chỗ</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Trạm sạc EV Fast-charge</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Vé tháng &amp; Doanh nghiệp</span></li>
            </ul>
          </div>

          {/* Hỗ trợ */}
          <div className="lg:col-span-2 flex flex-col gap-2">
            <h3 className="font-semibold text-white text-xs uppercase tracking-wider">Hỗ trợ</h3>
            <ul className="flex flex-col gap-1.5 text-xs text-slate-400">
              <li><span className="hover:text-white transition-colors cursor-pointer">Đối tác bãi đỗ</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Trung tâm trợ giúp</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Chính sách bảo mật</span></li>
            </ul>
          </div>

          {/* An toàn & Hotline */}
          <div className="lg:col-span-3 flex flex-col gap-2">
            <h3 className="font-semibold text-white text-xs uppercase tracking-wider">An toàn &amp; Liên hệ</h3>
            <div className="flex flex-wrap gap-1.5 text-[11px] text-slate-200 font-medium">
              <span className="px-2 py-0.5 rounded bg-slate-800 border border-slate-700">ISO 27001:2022</span>
              <span className="px-2 py-0.5 rounded bg-slate-800 border border-slate-700">TISAX AL3</span>
              <span className="px-2 py-0.5 rounded bg-slate-800 border border-slate-700">Đã xác thực BCT</span>
            </div>
            <div className="text-xs text-slate-400">
              Hotline 24/7: <span className="text-white font-semibold">1900 6868</span>
            </div>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="mt-6 pt-4 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-3 text-[11px] text-slate-400">
          <div className="flex flex-wrap items-center gap-5">
            <span className="hover:text-white transition-colors cursor-pointer">Quy định &amp; Chính sách</span>
            <span className="hover:text-white transition-colors cursor-pointer">Biểu phí dịch vụ</span>
            <span className="hover:text-white transition-colors cursor-pointer">Bảo mật thông tin</span>
          </div>
          <div>© 2026 ParkMaster Vietnam. All rights reserved.</div>
        </div>
      </div>
    </footer>
  );
};
