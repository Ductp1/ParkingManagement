import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowUpRight, CarFront, MapPin, ShieldCheck } from 'lucide-react';
import './auth.css';

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="auth-wrapper">
      <main className="auth-card">
        <aside className="auth-story">
          <Link to="/" className="auth-brand">
            <span className="auth-brand-icon">
              <CarFront size={15} />
            </span>
            ParkMaster<span className="auth-brand-dot">.</span>
          </Link>
          <div className="auth-story-copy">
            <span className="auth-eyebrow">MỖI HÀNH TRÌNH, MỘT KHỞI ĐẦU NHẸ NHÀNG</span>
            <h2>
              Bớt tìm chỗ.<br />
              Thêm thời gian<br />
              <em>cho chính bạn.</em>
            </h2>
            <p>Tìm bãi đỗ, quản lý phương tiện và theo dõi hành trình của bạn ở cùng một nơi.</p>
          </div>
          <div className="auth-visual" aria-hidden="true">
            <div className="auth-parking-grid">
              <span>P</span>
              <CarFront size={32} />
              <span>P</span>
            </div>
            <div className="auth-location">
              <span>
                <MapPin size={12} />
              </span>
              <div>
                <strong>Điểm đến tiếp theo</strong>
                <small>Một chỗ đỗ xe phù hợp với bạn</small>
              </div>
              <ArrowUpRight size={13} />
            </div>
          </div>
          <div className="auth-story-footer">
            <span>
              <ShieldCheck size={12} /> An tâm trên mỗi hành trình
            </span>
            <span>ĐỖ XE THÔNG MINH</span>
          </div>
        </aside>
        <section className="auth-content">
          <Link className="auth-back" to="/">
            <ArrowLeft size={13} /> Về trang chủ
          </Link>
          <div className="auth-form-container">{children}</div>
          <footer className="auth-footer">
            ParkMaster <span>·</span> Hành trình thuận tiện hơn mỗi ngày.
          </footer>
        </section>
      </main>
    </div>
  );
}
