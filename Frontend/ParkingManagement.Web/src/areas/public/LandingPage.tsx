import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { logoutSession } from '@/api/authApi';
import { MainFooter } from '@/components/layout/MainFooter';
import { useAppDispatch, useAppSelector } from '@/store';
import { logout } from '@/store/slices/authSlice';

interface FeaturedLot {
  id: number;
  name: string;
  address: string;
  price: string;
  available: number;
  tag: string;
  image: string;
}

const FEATURED_LOTS: FeaturedLot[] = [
  {
    id: 1,
    name: 'Landmark 81',
    address: 'Bình Thạnh, TP.HCM',
    price: '30K/h',
    available: 42,
    tag: '⚡ Sạc EV',
    image: '/images/lot-1.jpg',
  },
  {
    id: 2,
    name: 'Vinhomes Central Park',
    address: 'Bình Thạnh, TP.HCM',
    price: '25K/h',
    available: 18,
    tag: 'Hầm B1-B2',
    image: '/images/lot-2.jpg',
  },
  {
    id: 3,
    name: 'Saigon Centre',
    address: 'Quận 1, TP.HCM',
    price: '40K/h',
    available: 9,
    tag: 'Giữ trước',
    image: '/images/lot-3.jpg',
  },
  {
    id: 4,
    name: 'Bitexco Financial',
    address: 'Quận 1, TP.HCM',
    price: '35K/h',
    available: 28,
    tag: 'Camera 24/7',
    image: '/images/lot-4.jpg',
  },
];

const ABOUT_PILLARS = [
  {
    icon: 'fa-microchip',
    tag: 'Tầm nhìn 4.0',
    title: 'Kiến tạo Đô thị Thông minh',
    desc: 'Giải quyết triệt để vấn đề quá tải giao thông tĩnh bằng hạ tầng điều phối bãi đỗ kỹ thuật số tập trung trên nền tảng đám mây.',
  },
  {
    icon: 'fa-network-wired',
    tag: 'Hệ sinh thái',
    title: 'Kết nối Thời gian thực',
    desc: 'Đồng bộ tức thời giữa mạng lưới cảm biến mặt đất, camera quang học và ứng dụng di động cho tài xế & đơn vị quản lý.',
  },
  {
    icon: 'fa-shield-halved',
    tag: 'Chuẩn quốc tế',
    title: 'Bảo mật & Tự động toàn trình',
    desc: 'Áp dụng định danh xe không chạm, thanh toán phi tiền mặt và mã hóa xác thực RS256 chuẩn bảo mật ngân hàng.',
  },
];

export const LandingPage: React.FC = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { user, isAuthenticated } = useAppSelector((state) => state.auth);

  const [phoneInput, setPhoneInput] = useState('');
  const [sentSuccess, setSentSuccess] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);

  const getPortalPath = () => {
    if (!user) return '/login';
    if (user.role === 'Admin') return '/admin';
    if (user.role === 'Owner') return '/owner';
    if (user.role === 'Staff') return '/gate';
    return '/driver';
  };

  const scrollToId = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <div className="relative min-h-screen flex flex-col font-sans selection:bg-blue-600 selection:text-white bg-[#fafafc] text-slate-900 overflow-x-hidden">
      {/* =====================================================================
          BACKGROUND PURE WHITE & DEEP NAVY (PHONG CÁCH APPLE / STRIPE SAAS)
      ====================================================================== */}
      <div className="fixed inset-0 z-0 pointer-events-none overflow-hidden">
        {/* Hình nền kiến trúc to hiển thị mơ màng bán trong suốt */}
        <img
          src="/images/hero-bg.jpg"
          alt="Smart Parking Background"
          className="w-full h-full object-cover object-center scale-105 opacity-16"
        />

        {/* Lớp phủ trắng sương ngọc trai sang trọng */}
        <div className="absolute inset-0 bg-gradient-to-b from-white/95 via-[#fafafc]/85 to-white/95 backdrop-blur-[2px]" />

        {/* Lưới vi điểm tối giản thanh lịch */}
        <div
          className="absolute inset-0 opacity-[0.35]"
          style={{
            backgroundImage:
              'radial-gradient(rgba(15, 23, 42, 0.12) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
          }}
        />

        {/* Vầng sáng xanh Cobalt dịu nhẹ đặt phía sau tiêu đề */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 -translate-y-1/3 w-[850px] h-[480px] bg-gradient-to-b from-blue-500/8 via-sky-400/5 to-transparent rounded-full blur-[130px]" />
      </div>

      {/* =====================================================================
          1. HEADER KÍNH TRẮNG NGỌC TRAI BÁN TRONG SUỐT — TÊN DỰ ÁN TO & NỔI BẬT
      ====================================================================== */}
      <header className="sticky top-0 z-50 w-full bg-white/85 backdrop-blur-2xl border-b border-slate-200/80 shadow-[0_2px_15px_rgba(15,23,42,0.03)] transition-all">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-20 flex items-center justify-between">
          {/* Logo & Tên dự án to nổi bật */}
          <button
            type="button"
            onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
            className="flex items-center gap-3.5 text-left cursor-pointer group"
          >
            {/* Biểu tượng Logo ParkMaster chuẩn hệ thống với đèn tín hiệu công nghệ */}
            <div className="relative w-11 h-11 sm:w-12 sm:h-12 rounded-2xl bg-gradient-to-br from-blue-600 to-blue-700 text-white flex items-center justify-center font-black text-2xl sm:text-3xl shadow-md shadow-blue-600/25 group-hover:scale-105 transition-transform select-none">
              P
              <span className="absolute -top-1 -right-1 flex h-3 w-3">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
                <span className="relative inline-flex rounded-full h-3 w-3 bg-emerald-500 ring-2 ring-white" />
              </span>
            </div>

            {/* Tên dự án to nổi bật */}
            <div>
              <span className="font-black text-slate-900 text-2xl sm:text-3xl tracking-tight block leading-none">
                ParkMaster
              </span>
              <span className="text-[11px] text-slate-500 font-bold tracking-widest uppercase block mt-1.5">
                SMART PARKING SYSTEM
              </span>
            </div>
          </button>

          {/* Menu điều hướng tối giản */}
          <nav className="hidden md:flex items-center gap-1.5">
            {[
              { label: 'Giới thiệu', id: 'about' },
              { label: 'Bãi đỗ tiêu biểu', id: 'lots' },
              { label: 'Liên hệ', id: 'contact' },
            ].map((m) => (
              <button
                key={m.id}
                type="button"
                onClick={() => scrollToId(m.id)}
                className="px-4 py-2 rounded-xl text-xs font-bold text-slate-600 hover:text-blue-700 hover:bg-slate-100/80 transition-colors cursor-pointer"
              >
                {m.label}
              </button>
            ))}
          </nav>

          {/* Nút hành động */}
          <div className="flex items-center gap-3">
            <span className="hidden sm:inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 text-xs font-bold">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              24 Bãi Online
            </span>

            {isAuthenticated && user ? (
              <div className="flex items-center gap-2.5">
                <button
                  type="button"
                  onClick={() => navigate(getPortalPath())}
                  className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold shadow-md shadow-slate-900/10 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Vào hệ thống ({user.role})
                </button>
                <button
                  type="button"
                  disabled={loggingOut}
                  onClick={async () => {
                    setLoggingOut(true);
                    try {
                      await logoutSession();
                      dispatch(logout());
                    } finally {
                      setLoggingOut(false);
                    }
                  }}
                  className="text-xs font-semibold text-slate-500 hover:text-red-600 cursor-pointer px-2"
                >
                  Thoát
                </button>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => navigate('/login')}
                className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold shadow-md shadow-slate-900/10 active:scale-[0.98] transition-all cursor-pointer"
              >
                Đăng nhập
              </button>
            )}
          </div>
        </div>
      </header>

      {/* =====================================================================
          2. HERO TRẮNG TINH KHIẾT VỚI TÊN DỰ ÁN TO & THẺ KÍNH BÁN TRONG SUỐT
      ====================================================================== */}
      <section className="relative z-10 pt-12 sm:pt-16 pb-16 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 w-full">
        <div className="rounded-3xl bg-white/80 backdrop-blur-2xl border border-slate-200/90 p-6 sm:p-12 lg:p-14 shadow-[0_20px_60px_rgba(15,23,42,0.04)] relative overflow-hidden">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
            {/* Cột trái: Tên dự án to khổng lồ & Thông điệp */}
            <div className="lg:col-span-7 space-y-5">
              <span className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200/80 text-xs font-extrabold shadow-2xs">
                <i className="fa-solid fa-bolt text-blue-600 text-xs" />
                HỆ SINH THÁI PARKMASTER · SMART PARKING SYSTEM
              </span>

              {/* Tên dự án to nổi bật */}
              <h1 className="text-4xl sm:text-6xl lg:text-7xl font-black text-slate-900 tracking-tight leading-[1.08]">
                ParkMaster <br />
                <span className="text-blue-600">Smart Parking</span>
              </h1>

              <p className="text-sm sm:text-base text-slate-600 max-w-lg leading-relaxed">
                Nền tảng số hóa bãi đỗ xe thông minh ứng dụng AI & IoT — Biết trước chỗ trống, khóa vị trí theo sơ đồ tầng và qua cổng không chạm trong 0.2 giây.
              </p>

              <div className="pt-2 flex flex-wrap items-center gap-3.5">
                <button
                  type="button"
                  onClick={() => navigate(isAuthenticated ? getPortalPath() : '/login')}
                  className="px-7 py-3.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs sm:text-sm shadow-lg shadow-blue-600/25 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Tìm bãi đỗ ngay →
                </button>
                <button
                  type="button"
                  onClick={() => scrollToId('about')}
                  className="px-6 py-3.5 rounded-xl bg-white hover:bg-slate-50 text-slate-700 border border-slate-200/90 font-bold text-xs sm:text-sm shadow-2xs transition-all cursor-pointer"
                >
                  Tìm hiểu giải pháp
                </button>
              </div>

              {/* 3 chỉ số ngắn gọn trên thẻ trắng bán trong suốt */}
              <div className="pt-4 grid grid-cols-3 gap-3 max-w-md">
                {[
                  { num: '1.482', label: 'Ô trống live' },
                  { num: '0.18s', label: 'Nhận diện AI' },
                  { num: '24 Bãi', label: 'Đô thị TP.HCM' },
                ].map((s) => (
                  <div
                    key={s.label}
                    className="p-3 rounded-2xl bg-slate-50/80 border border-slate-200/80 text-center shadow-2xs"
                  >
                    <div className="text-lg sm:text-xl font-black text-blue-700">
                      {s.num}
                    </div>
                    <div className="text-[11px] text-slate-500 font-semibold">
                      {s.label}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Cột phải: Khung camera ANPR kính bán trong suốt */}
            <div className="lg:col-span-5">
              <div className="relative rounded-2xl overflow-hidden shadow-xl border border-slate-200/90 group">
                <img
                  src="/images/camera-anpr.jpg"
                  alt="ParkMaster Camera ANPR"
                  className="w-full h-72 sm:h-84 object-cover group-hover:scale-105 transition-transform duration-700"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-slate-950/80 via-transparent to-transparent" />

                {/* Huy hiệu kính nổi trên ảnh */}
                <div className="absolute top-3 left-3 px-3 py-1.5 rounded-full bg-white/95 backdrop-blur-md border border-slate-200 text-slate-900 text-xs font-bold shadow-md flex items-center gap-2">
                  <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                  <span>Cổng ANPR #01 · Trực tuyến</span>
                </div>

                <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between text-white text-xs">
                  <span className="font-mono font-bold bg-slate-900/90 px-3 py-1.5 rounded-lg border border-white/20 text-white">
                    51K-889.32 (0.18s)
                  </span>
                  <span className="font-bold text-emerald-400">
                    ✓ Barie mở tự động
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* =====================================================================
          3. GIỚI THIỆU VỀ SMART PARKING MANAGEMENT (KÍNH BÁN TRONG SUỐT)
      ====================================================================== */}
      <section id="about" className="relative z-10 py-14 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 w-full">
        <div className="text-center max-w-2xl mx-auto mb-12">
          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-50 text-blue-700 border border-blue-200 text-xs font-bold uppercase tracking-wider">
            Về chúng tôi
          </span>
          <h2 className="text-3xl sm:text-4xl font-extrabold text-slate-900 mt-3 tracking-tight">
            Giải pháp Smart Parking Management
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 mt-2 leading-relaxed">
            Nền tảng công nghệ toàn diện giúp chuyển đổi số hạ tầng bãi gửi xe, mang lại trải nghiệm gửi xe thông minh, minh bạch và an toàn tuyệt đối.
          </p>
        </div>

        {/* 3 Cột trụ giới thiệu bán trong suốt sang trọng */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {ABOUT_PILLARS.map((item) => (
            <div
              key={item.title}
              className="p-7 rounded-2xl bg-white/80 hover:bg-white backdrop-blur-xl border border-slate-200/90 hover:border-blue-400 shadow-[0_8px_30px_rgba(15,23,42,0.03)] hover:shadow-xl hover:-translate-y-1 transition-all duration-300 flex flex-col justify-between group"
            >
              <div>
                <div className="flex items-center justify-between mb-5">
                  <div className="w-12 h-12 rounded-xl bg-blue-50 text-blue-600 border border-blue-100 flex items-center justify-center group-hover:scale-105 transition-transform">
                    <i className={`fa-solid ${item.icon} text-lg`} />
                  </div>
                  <span className="text-[11px] font-bold text-slate-600 bg-slate-100 px-3 py-1 rounded-full border border-slate-200">
                    {item.tag}
                  </span>
                </div>

                <h3 className="font-extrabold text-slate-900 text-base mb-2">
                  {item.title}
                </h3>
                <p className="text-xs sm:text-sm text-slate-600 leading-relaxed">
                  {item.desc}
                </p>
              </div>

              <div className="mt-6 pt-3.5 border-t border-slate-100 flex items-center gap-1.5 text-xs font-bold text-blue-600">
                <span>Tiêu chuẩn ParkMaster</span>
                <span className="group-hover:translate-x-1 transition-transform">→</span>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* =====================================================================
          4. BÃI ĐỖ TIÊU BIỂU (4 THẺ KÍNH TRẮNG BÁN TRONG SUỐT)
      ====================================================================== */}
      <section id="lots" className="relative z-10 py-10 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 w-full">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-2.5">
            <span className="w-2.5 h-2.5 rounded-full bg-blue-600 ring-4 ring-blue-100" />
            <h2 className="text-xl sm:text-2xl font-extrabold text-slate-900 tracking-tight">
              Bãi đỗ tiêu biểu
            </h2>
          </div>
          <button
            type="button"
            onClick={() => navigate(isAuthenticated ? '/driver/parking-lots' : '/login')}
            className="text-xs font-bold text-blue-600 hover:text-blue-800 hover:underline cursor-pointer"
          >
            Xem tất cả 24 bãi →
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {FEATURED_LOTS.map((lot) => (
            <div
              key={lot.id}
              onClick={() => navigate(isAuthenticated ? '/driver/parking-lots' : '/login')}
              className="rounded-2xl bg-white/85 hover:bg-white backdrop-blur-xl border border-slate-200/90 hover:border-blue-400 shadow-[0_8px_30px_rgba(15,23,42,0.03)] hover:shadow-xl hover:-translate-y-1 transition-all duration-300 overflow-hidden cursor-pointer group flex flex-col justify-between"
            >
              <div>
                <div className="relative h-36 overflow-hidden bg-slate-950">
                  <img
                    src={lot.image}
                    alt={lot.name}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-slate-950/70 via-transparent to-transparent" />
                  <span className="absolute top-2.5 left-2.5 px-2.5 py-0.5 rounded-full bg-white/95 backdrop-blur-xs text-slate-900 text-[10px] font-bold shadow-sm">
                    {lot.tag}
                  </span>
                  <span className="absolute bottom-2.5 right-2.5 px-2.5 py-0.5 rounded-md bg-blue-600 text-white text-[10px] font-mono font-bold shadow-sm">
                    {lot.price}
                  </span>
                </div>

                <div className="p-3.5">
                  <h3 className="font-extrabold text-slate-900 text-sm">
                    {lot.name}
                  </h3>
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    {lot.address}
                  </p>
                </div>
              </div>

              <div className="px-3.5 pb-3.5 pt-1 flex items-center justify-between border-t border-slate-100 text-xs">
                <span className="font-bold text-emerald-600 flex items-center gap-1.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                  {lot.available} chỗ trống
                </span>
                <span className="font-bold text-blue-600 group-hover:translate-x-0.5 transition-transform">
                  Đặt ngay →
                </span>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* =====================================================================
          5. LIÊN HỆ KÍNH BÁN TRONG SUỐT PHONG CÁCH APPLE / STRIPE
      ====================================================================== */}
      <section id="contact" className="relative z-10 py-10 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 w-full">
        <div className="rounded-3xl bg-white/90 backdrop-blur-2xl border border-slate-200/90 p-6 sm:p-8 shadow-[0_12px_40px_rgba(15,23,42,0.04)] flex flex-col md:flex-row items-center justify-between gap-5">
          <div>
            <h3 className="text-base sm:text-lg font-bold text-slate-900">
              Đăng ký vé tháng & Hợp tác chủ bãi ParkMaster
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Hotline hỗ trợ 24/7: <strong className="text-blue-600">1900 6868</strong>
            </p>
          </div>

          {sentSuccess ? (
            <div className="text-xs font-bold text-emerald-800 bg-emerald-50 px-4 py-2.5 rounded-xl border border-emerald-200">
              ✓ Đã ghi nhận số {phoneInput}. Chúng tôi sẽ liên hệ lại ngay!
            </div>
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (phoneInput.trim()) setSentSuccess(true);
              }}
              className="flex w-full md:w-auto items-center gap-2"
            >
              <input
                required
                value={phoneInput}
                onChange={(e) => setPhoneInput(e.target.value)}
                placeholder="Số điện thoại của bạn..."
                className="w-full sm:w-56 px-4 py-2.5 rounded-xl bg-slate-50 border border-slate-200 text-xs text-slate-900 placeholder-slate-400 focus:outline-hidden focus:border-blue-600"
              />
              <button
                type="submit"
                className="px-6 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition-all cursor-pointer shrink-0 shadow-md shadow-slate-900/10 active:scale-[0.98]"
              >
                Gửi ngay
              </button>
            </form>
          )}
        </div>
      </section>

      {/* Footer chung hệ thống */}
      <div className="relative z-10 mt-auto">
        <MainFooter />
      </div>
    </div>
  );
};
