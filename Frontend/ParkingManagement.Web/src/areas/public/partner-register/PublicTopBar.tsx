import { Link } from 'react-router-dom';

/** Thanh trên của các trang công khai cùng ngôn ngữ thiết kế với trang đăng ký đối tác (Figma 221:139): logo, Trang chủ, Hỗ trợ. */
export function PublicTopBar() {
  const link =
    'rounded text-[13px] font-semibold text-(--admin-brand-navy-900) outline-none hover:underline focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)';
  return (
    <header className="border-b border-(--admin-border-default) bg-(--admin-surface-card)">
      <div className="mx-auto flex h-[72px] max-w-[1120px] items-center justify-between gap-4 px-4 md:px-8">
        <Link to="/" className="rounded text-lg font-bold text-(--admin-brand-navy-900) outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)">
          ParkMaster
        </Link>
        <nav aria-label="Liên kết nhanh" className="flex items-center gap-5">
          <Link to="/" className={link}>
            Trang chủ
          </Link>
          <a href="tel:19006868" className={link}>
            Hỗ trợ 1900 6868
          </a>
        </nav>
      </div>
    </header>
  );
}
