import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { AuthScene } from './AuthScene';
import './auth.css';

/** Khung chung cho đăng nhập / đăng ký / quên mật khẩu: nền minh hoạ thành phố, thẻ kính mờ ở giữa. */
export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="auth-wrapper">
      <AuthScene />

      <header className="auth-topbar">
        <Link to="/" className="auth-brand">
          <span className="auth-brand-icon" aria-hidden="true">P</span>
          ParkMaster
        </Link>
        <nav aria-label="Liên kết" className="auth-topnav">
          <Link to="/">Trang chủ</Link>
          <a href="tel:19006868">Hỗ trợ 1900 6868</a>
        </nav>
      </header>

      <main className="auth-card">{children}</main>
    </div>
  );
}
