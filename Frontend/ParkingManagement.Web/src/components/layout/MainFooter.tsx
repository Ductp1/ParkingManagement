import React from 'react';

export const MainFooter: React.FC = () => {
  return (
    <footer className="bg-slate-900 text-slate-300 py-12 border-t border-slate-800 mt-auto">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
          {/* Col 1 */}
          <div className="flex flex-col gap-4">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-base">
                P
              </div>
              <span className="font-bold text-lg tracking-tight text-white">ParkMaster</span>
            </div>
            <p className="text-sm text-slate-400 leading-relaxed">
              Hệ sinh thái bãi đỗ thông minh hàng đầu, ứng dụng công nghệ IoT và AI nhận diện tự động ANPR cho trải nghiệm gửi xe xuyên suốt.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="flex h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
              <span className="text-xs text-emerald-300 font-semibold tracking-wide">
                Hệ thống ANPR Optical trực tuyến: 24 Bãi đỗ TP.HCM
              </span>
            </div>
          </div>

          {/* Col 2 */}
          <div className="flex flex-col gap-3">
            <h3 className="font-semibold text-white text-base">Dịch vụ & Tính năng</h3>
            <ul className="flex flex-col gap-2 text-sm text-slate-400">
              <li><span className="hover:text-white transition-colors cursor-pointer">Tìm bãi thông minh</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Đặt chỗ & Giữ trước 15'</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Trạm sạc EV Fast-charge</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Vé tháng & Doanh nghiệp</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Cổng Barie ANPR</span></li>
            </ul>
          </div>

          {/* Col 3 */}
          <div className="flex flex-col gap-3">
            <h3 className="font-semibold text-white text-base">Về ParkMaster</h3>
            <ul className="flex flex-col gap-2 text-sm text-slate-400">
              <li><span className="hover:text-white transition-colors cursor-pointer">Giới thiệu nền tảng</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Tin tức & Sự kiện</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Đối tác bãi đỗ</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Chính sách bảo mật</span></li>
              <li><span className="hover:text-white transition-colors cursor-pointer">Trung tâm hỗ trợ 24/7</span></li>
            </ul>
          </div>

          {/* Col 4 */}
          <div className="flex flex-col gap-4">
            <h3 className="font-semibold text-white text-base">Tiêu chuẩn An toàn</h3>
            <div className="flex flex-col gap-2">
              <span className="text-xs text-slate-400">Chứng nhận an ninh & bảo mật:</span>
              <div className="flex flex-wrap gap-2 text-xs text-white font-medium">
                <span className="px-2.5 py-1 rounded bg-slate-800 border border-slate-700">ISO 27001:2022</span>
                <span className="px-2.5 py-1 rounded bg-slate-800 border border-slate-700">TISAX AL3</span>
                <span className="px-2.5 py-1 rounded bg-slate-800 border border-slate-700">Đã xác thực BCT</span>
              </div>
            </div>
            <div className="text-xs text-slate-400">
              Hotline hỗ trợ: <span className="text-white font-semibold">1900 6868 (24/7)</span>
            </div>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="mt-10 pt-6 border-t border-slate-800 flex flex-col md:flex-row items-center justify-between gap-4 text-xs text-slate-400">
          <div className="flex flex-wrap items-center gap-6">
            <span className="hover:text-white transition-colors cursor-pointer">Quy định & Chính sách</span>
            <span className="hover:text-white transition-colors cursor-pointer">Biểu phí dịch vụ</span>
            <span className="hover:text-white transition-colors cursor-pointer">Bảo mật thông tin</span>
          </div>
          <div>Bản quyền © 2026 ParkMaster Vietnam. All rights reserved.</div>
        </div>
      </div>
    </footer>
  );
};
