import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { logoutSession } from '@/api/authApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { logout } from '@/store/slices/authSlice';
import { AuthScene } from '@/components/auth/AuthScene';
import './landing.css';

/* ===================== Nội dung ===================== */

interface FeaturedLot {
  id: number;
  name: string;
  address: string;
  price: string;
  available: number;
  tag: string;
  image: string;
}

/** Dữ liệu minh hoạ cho trang giới thiệu (không phải số liệu trực tiếp). */
const FEATURED_LOTS: FeaturedLot[] = [
  { id: 1, name: 'Landmark 81', address: 'Bình Thạnh, TP.HCM', price: '30.000đ/giờ', available: 42, tag: 'Có trạm sạc xe điện', image: '/images/lot-1.jpg' },
  { id: 2, name: 'Vinhomes Central Park', address: 'Bình Thạnh, TP.HCM', price: '25.000đ/giờ', available: 18, tag: 'Hầm B1–B2', image: '/images/lot-2.jpg' },
  { id: 3, name: 'Saigon Centre', address: 'Quận 1, TP.HCM', price: '40.000đ/giờ', available: 9, tag: 'Đặt trước được', image: '/images/lot-3.jpg' },
  { id: 4, name: 'Bitexco Financial', address: 'Quận 1, TP.HCM', price: '35.000đ/giờ', available: 28, tag: 'Camera 24/7', image: '/images/lot-4.jpg' },
];

const FEATURES = [
  { icon: 'local_parking', title: 'Xem chỗ trống trước khi đi', desc: 'Số chỗ trống của từng bãi cập nhật theo lượt xe ra vào tại cổng.' },
  { icon: 'event_available', title: 'Đặt chỗ trước', desc: 'Chọn giờ đến và xe sẽ gửi, chỗ được giữ cho bạn trong khung giờ đã đặt.' },
  { icon: 'photo_camera', title: 'Qua cổng bằng biển số', desc: 'Camera đọc biển số và đối chiếu với lượt đặt, không cần lấy vé giấy.' },
  { icon: 'height', title: 'Lọc theo chiều cao xe', desc: 'Ẩn các hầm có trần thấp hơn xe của bạn, tránh cạ nóc khi vào bãi.' },
  { icon: 'account_balance_wallet', title: 'Thanh toán không tiền mặt', desc: 'Trả qua VNPAY, MoMo hoặc VietQR ngay trên điện thoại.' },
  { icon: 'receipt_long', title: 'Lịch sử và hoá đơn', desc: 'Mọi lượt gửi xe và khoản đã trả nằm gọn trong tài khoản của bạn.' },
];

const STEPS = [
  { title: 'Tìm bãi còn chỗ', desc: 'Nhập nơi bạn sắp đến, xem bãi gần nhất, giá theo giờ và số chỗ trống.' },
  { title: 'Đặt chỗ', desc: 'Chọn giờ đến và xe, xác nhận và thanh toán trong vài bước.' },
  { title: 'Lái xe vào cổng', desc: 'Cổng tự mở khi camera nhận ra biển số khớp với lượt đặt của bạn.' },
];

const OWNER_POINTS = [
  'Quản lý sơ đồ tầng, khu và từng ô đỗ',
  'Tự đặt bảng giá theo giờ, ngày và vé tháng',
  'Theo dõi lượt xe, doanh thu và đối soát',
  'Tạo tài khoản cho nhân viên trực cổng',
];

const FAQS = [
  {
    q: 'Đặt chỗ trên ParkMaster có mất thêm phí không?',
    a: 'Giá gửi xe do từng bãi đặt và luôn hiện rõ trước khi bạn xác nhận. Tổng tiền bạn trả được ghi trong hoá đơn của lượt đặt.',
  },
  {
    q: 'Xe tôi cao, làm sao biết bãi có vào được không?',
    a: 'Khai báo chiều cao xe trong mục Xe của tôi. Khi tìm bãi, ParkMaster ẩn các hầm có trần thấp hơn chiều cao xe cộng 10 cm.',
  },
  {
    q: 'Tôi có thể thanh toán bằng gì?',
    a: 'Bạn thanh toán qua VNPAY, MoMo hoặc VietQR. Biên nhận được lưu trong Lịch sử đặt chỗ.',
  },
  {
    q: 'Làm sao để đưa bãi đỗ của tôi lên ParkMaster?',
    a: 'Tạo tài khoản chủ bãi, khai báo bãi và nộp hồ sơ pháp lý. Sau khi đội ngũ ParkMaster duyệt hồ sơ và ký hợp đồng, bãi của bạn bắt đầu nhận đặt chỗ.',
  },
  {
    q: 'Thông tin cá nhân của tôi được bảo vệ thế nào?',
    a: 'Số giấy tờ tuỳ thân được mã hoá khi lưu và chỉ hiển thị vài số cuối. Bạn có thể tải về, yêu cầu sửa hoặc xoá dữ liệu của mình trong mục Dữ liệu của tôi.',
  },
];

const FAQ_JSON_LD = JSON.stringify({
  '@context': 'https://schema.org',
  '@type': 'FAQPage',
  mainEntity: FAQS.map((f) => ({ '@type': 'Question', name: f.q, acceptedAnswer: { '@type': 'Answer', text: f.a } })),
});

const NAV_LINKS = [
  { label: 'Tính năng', id: 'features' },
  { label: 'Cách hoạt động', id: 'how' },
  { label: 'Chủ bãi', id: 'owners' },
  { label: 'Câu hỏi', id: 'faq' },
];

/* ===================== Chế độ sáng / tối ===================== */

type ThemeChoice = 'light' | 'dark' | 'system';
const THEME_KEY = 'parkmaster-landing-theme';

const readTheme = (): ThemeChoice => {
  try {
    const v = localStorage.getItem(THEME_KEY);
    // Mặc định giao diện sáng: khung cảnh thành phố ban ngày là điểm nhấn của trang
    return v === 'dark' || v === 'system' ? v : 'light';
  } catch {
    return 'light';
  }
};

function useLandingTheme() {
  const [choice, setChoice] = useState<ThemeChoice>(readTheme);
  const [systemDark, setSystemDark] = useState(() => window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false);

  useEffect(() => {
    const mq = window.matchMedia?.('(prefers-color-scheme: dark)');
    if (!mq) return;
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);

  const update = (next: ThemeChoice) => {
    setChoice(next);
    try {
      if (next === 'light') localStorage.removeItem(THEME_KEY);
      else localStorage.setItem(THEME_KEY, next);
    } catch {
      /* trình duyệt chặn lưu trữ: vẫn đổi được trong phiên hiện tại */
    }
  };

  return { choice, dark: choice === 'dark' || (choice === 'system' && systemDark), update };
}

const THEME_OPTIONS: { value: ThemeChoice; icon: string; label: string }[] = [
  { value: 'light', icon: 'light_mode', label: 'Giao diện sáng' },
  { value: 'dark', icon: 'dark_mode', label: 'Giao diện tối' },
  { value: 'system', icon: 'contrast', label: 'Theo hệ thống' },
];

/* ===================== Thành phần nhỏ ===================== */

const Icon: React.FC<{ name: string; className?: string }> = ({ name, className = '' }) => (
  <span className={`material-symbols-outlined ${className}`} aria-hidden="true">{name}</span>
);

const BrandMark: React.FC<{ size?: number }> = ({ size = 34 }) => (
  <span
    aria-hidden="true"
    style={{ width: size, height: size }}
    className="grid shrink-0 place-items-center rounded-[9px] bg-[#2f6fd6] text-white font-extrabold text-xl shadow-[inset_0_0_0_2px_rgb(255_255_255/0.7)]"
  >
    P
  </span>
);

const ThemeSwitch: React.FC<{ choice: ThemeChoice; onChange: (v: ThemeChoice) => void }> = ({ choice, onChange }) => (
  <div role="radiogroup" aria-label="Chế độ hiển thị" className="flex rounded-xl border border-(--lp-glass-line) bg-(--lp-glass) p-0.5">
    {THEME_OPTIONS.map((o) => (
      <button
        key={o.value}
        type="button"
        role="radio"
        aria-checked={choice === o.value}
        title={o.label}
        aria-label={o.label}
        onClick={() => onChange(o.value)}
        className={`grid h-8 w-8 place-items-center rounded-lg cursor-pointer ${
          choice === o.value ? 'bg-(--lp-glass-strong) text-(--lp-text) shadow-sm' : 'text-(--lp-faint) hover:text-(--lp-text)'
        }`}
      >
        <Icon name={o.icon} className="text-[18px]" />
      </button>
    ))}
  </div>
);

/* ===================== Trang ===================== */

export const LandingPage: React.FC = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { user, isAuthenticated } = useAppSelector((state) => state.auth);
  const theme = useLandingTheme();

  const [menuOpen, setMenuOpen] = useState(false);
  const [phoneInput, setPhoneInput] = useState('');
  const [sentSuccess, setSentSuccess] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);

  useEffect(() => {
    document.title = 'ParkMaster — Tìm chỗ đỗ xe còn trống ở TP.HCM';
  }, []);

  const getPortalPath = () => {
    if (!user) return '/login';
    if (user.role === 'Admin') return '/admin';
    if (user.role === 'Owner') return '/owner';
    if (user.role === 'Staff') return '/gate';
    return '/driver';
  };

  const findLots = () => navigate(isAuthenticated ? '/driver/parking-lots' : '/register');

  const scrollToId = (id: string) => {
    setMenuOpen(false);
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  };

  const handleLogout = async () => {
    setLoggingOut(true);
    try {
      await logoutSession();
      dispatch(logout());
    } finally {
      setLoggingOut(false);
    }
  };

  const authButtons =
    isAuthenticated && user ? (
      <>
        <button type="button" onClick={() => navigate(getPortalPath())} className="lp-btn px-4 py-2 text-sm">
          Vào trang của tôi
        </button>
        <button
          type="button"
          disabled={loggingOut}
          onClick={handleLogout}
          className="rounded-lg px-3 py-2 text-sm font-semibold text-(--lp-text) hover:bg-(--lp-glass) cursor-pointer disabled:opacity-50"
        >
          Đăng xuất
        </button>
      </>
    ) : (
      <>
        <button type="button" onClick={() => navigate('/login')} className="rounded-lg px-3 py-2 text-sm font-semibold text-(--lp-text) hover:bg-(--lp-glass) cursor-pointer">
          Đăng nhập
        </button>
        <button type="button" onClick={() => navigate('/register')} className="lp-btn px-4 py-2 text-sm">
          Tạo tài khoản
        </button>
      </>
    );

  return (
    <div className={`lp min-h-screen ${theme.dark ? 'lp-dark' : ''}`}>
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[60] focus:rounded-lg focus:bg-(--lp-primary) focus:px-4 focus:py-2 focus:text-(--lp-on-primary)"
      >
        Bỏ qua tới nội dung
      </a>

      {/* ===== Thanh điều hướng: kính mờ ===== */}
      <header className="fixed inset-x-0 top-0 z-50">
        <div className="mx-auto mt-3 flex h-14 max-w-7xl items-center gap-6 rounded-2xl px-4 sm:mx-4 lg:mx-auto lg:px-6 lp-glass">
          <a href="#main" onClick={(e) => { e.preventDefault(); window.scrollTo({ top: 0, behavior: 'smooth' }); }} className="flex items-center gap-2.5">
            <BrandMark size={32} />
            <span className="text-lg font-bold">ParkMaster</span>
          </a>

          <nav aria-label="Trang chủ" className="hidden flex-1 items-center justify-center gap-1 md:flex">
            {NAV_LINKS.map((l) => (
              <a
                key={l.id}
                href={`#${l.id}`}
                onClick={(e) => { e.preventDefault(); scrollToId(l.id); }}
                className="rounded-lg px-3 py-2 text-sm font-semibold text-(--lp-muted) hover:bg-(--lp-glass-strong) hover:text-(--lp-text)"
              >
                {l.label}
              </a>
            ))}
          </nav>

          <div className="ml-auto hidden items-center gap-2 md:flex">
            <ThemeSwitch choice={theme.choice} onChange={theme.update} />
            {authButtons}
          </div>

          <button
            type="button"
            className="ml-auto grid h-10 w-10 place-items-center rounded-lg text-(--lp-text) hover:bg-(--lp-glass-strong) md:hidden cursor-pointer"
            aria-label={menuOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={menuOpen}
            aria-controls="lp-mobile-menu"
            onClick={() => setMenuOpen((v) => !v)}
          >
            <Icon name={menuOpen ? 'close' : 'menu'} className="text-[24px]" />
          </button>
        </div>

        {menuOpen && (
          <div id="lp-mobile-menu" className="mx-4 mt-2 rounded-2xl px-3 pb-4 pt-2 md:hidden lp-glass">
            <nav aria-label="Trang chủ" className="flex flex-col">
              {NAV_LINKS.map((l) => (
                <a
                  key={l.id}
                  href={`#${l.id}`}
                  onClick={(e) => { e.preventDefault(); scrollToId(l.id); }}
                  className="rounded-lg px-3 py-3 font-semibold hover:bg-(--lp-glass-strong)"
                >
                  {l.label}
                </a>
              ))}
            </nav>
            <div className="mt-2 flex flex-wrap items-center gap-2 border-t border-(--lp-line) pt-3">
              {authButtons}
              <span className="ml-auto"><ThemeSwitch choice={theme.choice} onChange={theme.update} /></span>
            </div>
          </div>
        )}
      </header>

      <main id="main">
        {/* ===== Hero: khung cảnh thành phố + thẻ kính mờ ===== */}
        <section className="relative isolate flex min-h-[100svh] items-center overflow-hidden px-4 pb-40 pt-28">
          <AuthScene className="lp-hero-scene" />
          <div className="mx-auto w-full max-w-2xl rounded-3xl px-6 py-10 text-center sm:px-12 lp-glass">
            <h1 className="mx-auto max-w-[16ch] text-4xl font-bold leading-[1.1] tracking-tight sm:text-5xl">
              Biết bãi nào còn chỗ trước khi bạn rời nhà.
            </h1>
            <p className="mx-auto mt-5 max-w-[48ch] text-lg leading-relaxed text-(--lp-muted)">
              Xem chỗ trống, đặt chỗ trước và qua cổng bằng biển số tại các bãi đỗ đối tác ở TP.HCM.
            </p>
            <form
              onSubmit={(e) => { e.preventDefault(); findLots(); }}
              className="mx-auto mt-8 flex max-w-lg flex-col gap-2 rounded-2xl bg-(--lp-glass-strong) p-2 sm:flex-row"
            >
              <label htmlFor="hero-where" className="sr-only">Bạn muốn đỗ xe ở đâu?</label>
              <div className="flex flex-1 items-center gap-2 px-3">
                <Icon name="location_on" className="text-[22px] text-(--lp-primary)" />
                <input
                  id="hero-where"
                  placeholder="Bạn muốn đỗ xe ở đâu?"
                  className="w-full bg-transparent py-3 text-base text-(--lp-text) placeholder:text-(--lp-faint) focus:outline-none"
                />
              </div>
              <button type="submit" className="lp-btn px-6 py-3">Tìm bãi đỗ</button>
            </form>
            <ul className="mt-6 flex flex-wrap justify-center gap-2" aria-label="Chỗ trống minh hoạ">
              {FEATURED_LOTS.slice(0, 3).map((lot) => (
                <li key={lot.id} className="flex items-center gap-2 rounded-full bg-(--lp-glass-strong) px-3 py-1.5 text-sm">
                  <span className="h-2 w-2 rounded-full bg-emerald-500" />
                  <span className="font-medium">{lot.name}</span>
                  <span className="lp-num text-base font-semibold text-(--lp-primary)">{lot.available}</span>
                  <span className="text-(--lp-faint)">chỗ</span>
                </li>
              ))}
            </ul>
          </div>
        </section>

        {/* ===== Tính năng ===== */}
        <section id="features" className="scroll-mt-24 mx-auto max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
          <div className="text-center">
            <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Mọi thứ cần cho một lượt gửi xe</h2>
            <p className="mx-auto mt-3 max-w-[56ch] text-lg text-(--lp-muted)">Từ lúc tìm bãi đến lúc ra cổng, bạn làm tất cả trên điện thoại.</p>
          </div>
          <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((f) => (
              <div key={f.title} className="rounded-2xl p-6 lp-glass">
                <span className="grid h-12 w-12 place-items-center rounded-xl bg-(--lp-primary-soft) text-(--lp-primary)">
                  <Icon name={f.icon} className="text-[26px]" />
                </span>
                <h3 className="mt-4 text-lg font-semibold">{f.title}</h3>
                <p className="mt-1.5 leading-relaxed text-(--lp-muted)">{f.desc}</p>
              </div>
            ))}
          </div>
        </section>

        {/* ===== Cách hoạt động: 3 bước nối tiếp ===== */}
        <section id="how" className="scroll-mt-24 mx-auto max-w-7xl px-4 pb-20 sm:px-6 lg:px-8">
          <div className="rounded-3xl px-6 py-12 sm:px-12 lp-glass">
            <h2 className="text-center text-3xl font-bold tracking-tight sm:text-4xl">Ba bước từ nhà đến chỗ đỗ</h2>
            <ol className="mt-12 grid gap-10 md:grid-cols-3 md:gap-0">
              {STEPS.map((s, i) => (
                <li key={s.title} className="text-center md:px-6">
                  <div className="relative flex items-center justify-center">
                    {i > 0 && <span aria-hidden="true" className="absolute right-1/2 hidden w-full border-t-[3px] border-dashed border-(--lp-line) md:block" />}
                    <span className="lp-btn lp-num relative h-14 w-14 text-3xl font-semibold">{i + 1}</span>
                  </div>
                  <h3 className="mt-5 text-xl font-semibold">{s.title}</h3>
                  <p className="mx-auto mt-2 max-w-[30ch] leading-relaxed text-(--lp-muted)">{s.desc}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        {/* ===== Bãi đỗ đối tác ===== */}
        <section aria-labelledby="lots-title" className="mx-auto max-w-7xl px-4 pb-20 sm:px-6 lg:px-8">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <h2 id="lots-title" className="text-3xl font-bold tracking-tight sm:text-4xl">Bãi đỗ đối tác</h2>
            <button type="button" onClick={findLots} className="font-semibold text-(--lp-primary) hover:underline cursor-pointer">
              Xem tất cả bãi đỗ
            </button>
          </div>
          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {FEATURED_LOTS.map((lot) => (
              <button key={lot.id} type="button" onClick={findLots} className="group overflow-hidden rounded-2xl text-left cursor-pointer lp-glass">
                <div className="relative">
                  <img src={lot.image} alt={`Bãi đỗ ${lot.name}`} loading="lazy" className="aspect-[4/3] w-full object-cover" />
                  <span className="absolute left-3 top-3 flex items-center gap-1.5 rounded-full bg-white/90 px-2.5 py-1 text-sm font-semibold text-[#0f1f38]">
                    <span className="h-2 w-2 rounded-full bg-emerald-500" />
                    <span className="lp-num text-base">{lot.available}</span> chỗ trống
                  </span>
                </div>
                <div className="p-4">
                  <div className="flex items-start justify-between gap-3">
                    <h3 className="text-lg font-semibold group-hover:text-(--lp-primary)">{lot.name}</h3>
                    <span className="lp-num whitespace-nowrap text-lg font-semibold">{lot.price}</span>
                  </div>
                  <p className="text-sm text-(--lp-faint)">{lot.address}</p>
                  <p className="mt-2 text-sm text-(--lp-muted)">{lot.tag}</p>
                </div>
              </button>
            ))}
          </div>
          <p className="mt-6 text-sm text-(--lp-faint)">Số chỗ trống và giá trên trang này là dữ liệu minh hoạ.</p>
        </section>

        {/* ===== Dành cho chủ bãi ===== */}
        <section id="owners" className="scroll-mt-24 mx-auto max-w-7xl px-4 pb-20 sm:px-6 lg:px-8">
          <div className="grid items-center gap-10 rounded-3xl p-6 sm:p-10 lg:grid-cols-2 lp-glass">
            <div>
              <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Bạn có bãi đỗ? Đưa bãi lên ParkMaster.</h2>
              <p className="mt-4 max-w-[52ch] text-lg leading-relaxed text-(--lp-muted)">
                Nhận khách đặt chỗ trước và quản lý toàn bộ bãi từ một trang: sơ đồ, giá, lượt xe và nhân viên.
              </p>
              <ul className="mt-6 space-y-3">
                {OWNER_POINTS.map((pt) => (
                  <li key={pt} className="flex items-start gap-3">
                    <Icon name="check_circle" className="mt-0.5 text-[22px] text-(--lp-ok)" />
                    <span>{pt}</span>
                  </li>
                ))}
              </ul>

              {sentSuccess ? (
                <p role="status" className="mt-8 font-semibold text-(--lp-ok)">
                  Đã ghi nhận số {phoneInput}. Chúng tôi sẽ gọi lại trong giờ hành chính.
                </p>
              ) : (
                <form
                  className="mt-8"
                  onSubmit={(e) => {
                    e.preventDefault();
                    if (phoneInput.trim()) setSentSuccess(true);
                  }}
                >
                  <label htmlFor="owner-phone" className="block font-semibold">Để lại số điện thoại, chúng tôi gọi lại hướng dẫn</label>
                  <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                    <input
                      id="owner-phone"
                      type="tel"
                      inputMode="tel"
                      autoComplete="tel"
                      required
                      value={phoneInput}
                      onChange={(e) => setPhoneInput(e.target.value)}
                      placeholder="0901 234 567"
                      className="w-full rounded-xl border border-(--lp-glass-line) bg-(--lp-glass-strong) px-4 py-3 text-base text-(--lp-text) placeholder:text-(--lp-faint) focus:border-(--lp-primary) focus:outline-none"
                    />
                    <button type="submit" className="lp-btn shrink-0 px-6 py-3">Gọi lại cho tôi</button>
                  </div>
                </form>
              )}
            </div>

            <figure className="relative">
              <img
                src="/images/camera-anpr.jpg"
                alt="Hầm đỗ xe có camera đọc biển số tại cổng"
                loading="lazy"
                className="aspect-[4/3] w-full rounded-2xl object-cover"
              />
              <figcaption className="absolute bottom-4 left-4 flex flex-wrap items-center gap-3 rounded-xl bg-white/90 px-4 py-3 text-[#0f1f38] backdrop-blur">
                <span className="lp-num rounded border-2 border-[#0f1f38] px-2.5 py-0.5 text-2xl font-semibold tracking-wide">51K-889.32</span>
                <span className="text-sm font-semibold text-[#0f7a4f]">Khớp lượt đặt, cổng đã mở</span>
              </figcaption>
            </figure>
          </div>
        </section>

        {/* ===== Câu hỏi thường gặp ===== */}
        <section id="faq" className="scroll-mt-24 mx-auto max-w-3xl px-4 pb-24 sm:px-6">
          <h2 className="text-center text-3xl font-bold tracking-tight sm:text-4xl">Câu hỏi thường gặp</h2>
          <p className="mt-3 text-center text-(--lp-muted)">
            Chưa thấy câu trả lời? Gọi <a href="tel:19006868" className="lp-num text-lg font-semibold text-(--lp-text) hover:underline">1900 6868</a>.
          </p>
          <div className="lp-faq mt-10 space-y-3">
            {FAQS.map((f) => (
              <details key={f.q} className="rounded-2xl px-5 lp-glass">
                <summary className="flex cursor-pointer items-center justify-between gap-6 py-4 text-lg font-semibold">
                  {f.q}
                  <Icon name="add" className="lp-faq-sign text-[24px] text-(--lp-faint)" />
                </summary>
                <p className="max-w-[62ch] pb-5 leading-relaxed text-(--lp-muted)">{f.a}</p>
              </details>
            ))}
          </div>
          <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: FAQ_JSON_LD }} />
        </section>
      </main>

      {/* ===== Chân trang: tối như mặt đường ===== */}
      <footer className="bg-(--lp-ink) text-slate-300">
        <div className="mx-auto flex max-w-7xl flex-col items-start justify-between gap-6 border-b border-white/10 px-4 py-12 sm:px-6 md:flex-row md:items-center lg:px-8">
          <h2 className="max-w-[22ch] text-2xl font-bold tracking-tight text-white sm:text-3xl">Lần tới đi đâu, bạn sẽ biết trước chỗ đỗ.</h2>
          <button
            type="button"
            onClick={() => navigate(isAuthenticated ? getPortalPath() : '/register')}
            className="lp-btn px-6 py-3"
          >
            {isAuthenticated ? 'Vào trang của tôi' : 'Tạo tài khoản miễn phí'}
          </button>
        </div>
        <div className="mx-auto grid max-w-7xl gap-10 px-4 py-12 sm:px-6 md:grid-cols-[1.4fr_1fr_1fr] lg:px-8">
          <div className="max-w-xs">
            <div className="flex items-center gap-2.5 text-white">
              <BrandMark size={30} />
              <span className="text-lg font-bold">ParkMaster</span>
            </div>
            <p className="mt-4 leading-relaxed">Tìm chỗ đỗ, đặt trước và qua cổng bằng biển số tại TP.HCM.</p>
            <p className="mt-4">
              Hỗ trợ: <a href="tel:19006868" className="lp-num text-lg text-white hover:underline">1900 6868</a>
            </p>
          </div>
          <nav aria-label="Liên kết nhanh">
            <h2 className="font-semibold text-white">ParkMaster</h2>
            <ul className="mt-3 space-y-2">
              {NAV_LINKS.map((l) => (
                <li key={l.id}>
                  <a href={`#${l.id}`} onClick={(e) => { e.preventDefault(); scrollToId(l.id); }} className="hover:text-white">
                    {l.label}
                  </a>
                </li>
              ))}
            </ul>
          </nav>
          <nav aria-label="Pháp lý">
            <h2 className="font-semibold text-white">Pháp lý</h2>
            <ul className="mt-3 space-y-2">
              <li><a href="#" className="hover:text-white">Điều khoản sử dụng</a></li>
              <li><a href="#" className="hover:text-white">Chính sách bảo mật</a></li>
              <li><a href="#" className="hover:text-white">Biểu phí dịch vụ</a></li>
            </ul>
          </nav>
        </div>
        <div className="border-t border-white/10">
          <p className="mx-auto max-w-7xl px-4 py-5 text-sm sm:px-6 lg:px-8">© {new Date().getFullYear()} ParkMaster. Đồ án quản lý bãi đỗ xe.</p>
        </div>
      </footer>
    </div>
  );
};
