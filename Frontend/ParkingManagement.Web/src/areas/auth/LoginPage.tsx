import { AuthLayout } from '@/components/auth/AuthLayout';
import { ArrowRight, Eye, EyeOff } from 'lucide-react';
import React, { useState } from 'react';
import { Link, Navigate, useNavigate, useLocation } from 'react-router-dom';
import { login } from '@/api/authApi';
import { TurnstileWidget } from '@/components/auth/TurnstileWidget';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser, setToken } from '@/store/slices/authSlice';
import type { AuthUser } from '@/types/auth';

export function LoginPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, isAuthenticated, isLoading } = useAppSelector((s) => s.auth);

  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [captchaToken, setCaptchaToken] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [openProfileAfterLogin, setOpenProfileAfterLogin] = useState(false);

  if (isLoading) {
    return (
      <main className="min-h-screen bg-white flex items-center justify-center p-4">
        <div className="flex items-center gap-3 px-6 py-3.5 rounded-2xl bg-white border border-slate-200 text-sm font-semibold text-slate-700 shadow-xl">
          <i className="fa-solid fa-circle-notch fa-spin text-blue-600" />
          <span>Đang kiểm tra phiên đăng nhập…</span>
        </div>
      </main>
    );
  }

  // Nếu đã đăng nhập, chuyển hướng chính xác đến dashboard của vai trò đó
  if (isAuthenticated && user) {
    const targetRoute =
      user.role === 'Admin'
        ? '/admin'
        : user.role === 'Owner'
          ? '/owner'
          : user.role === 'Staff'
            ? '/gate'
            : '/driver';
    return <Navigate to={targetRoute} replace />;
  }

  const navigateToRoleDashboard = (authUser: AuthUser) => {
    // Nếu có redirect state từ URL trước đó, kiểm tra quyền trước khi redirect
    const from = (location.state as { from?: { pathname?: string } | string })?.from;
    const fromPath = typeof from === 'string' ? from : from?.pathname;
    const userRoles = authUser.roles && authUser.roles.length > 0 ? authUser.roles : [authUser.role];

    let targetRoute =
      authUser.role === 'Admin'
        ? '/admin'
        : authUser.role === 'Owner'
          ? '/owner'
          : authUser.role === 'Staff'
            ? '/gate'
            : '/driver';

    if (openProfileAfterLogin) {
      targetRoute = '/account/profile';
    } else if (fromPath) {
      if (fromPath === '/account/profile') targetRoute = fromPath;
      else if (fromPath.startsWith('/admin') && userRoles.includes('Admin')) targetRoute = fromPath;
      else if (fromPath.startsWith('/owner') && (userRoles.includes('Owner') || userRoles.includes('Admin'))) targetRoute = fromPath;
      else if (fromPath.startsWith('/gate') && (userRoles.includes('Staff') || userRoles.includes('Admin'))) targetRoute = fromPath;
      else if (fromPath.startsWith('/driver') && (userRoles.includes('Driver') || userRoles.includes('Admin'))) targetRoute = fromPath;
    }

    navigate(targetRoute, { replace: true });
  };

  const handleLogin = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (busy || !captchaToken) return;
    setBusy(true);
    setError('');

    try {
      const loggedUser = await login(identifier, password, captchaToken);
      dispatch(setToken(sessionStorage.getItem('parkmaster_token')));
      dispatch(setUser(loggedUser));
      navigateToRoleDashboard(loggedUser);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Đăng nhập thất bại. Vui lòng thử lại.');
    } finally {
      setBusy(false);
      setCaptchaToken('');
      setAttempt((n) => n + 1);
    }
  };

  return (
    <AuthLayout>
      <nav className="auth-tabs" aria-label="Tài khoản">
        <Link to="/login" aria-current="page">Đăng nhập</Link>
        <Link to="/register">Tạo tài khoản</Link>
      </nav>
      <span className="auth-kicker">CHÀO MỪNG TRỞ LẠI</span>
      <h1 className="auth-heading">Hành trình mới,<br />bắt đầu từ đây.</h1>
      <p className="auth-description">Đăng nhập để tiếp tục hành trình cùng ParkMaster.</p>

      {/* Form đăng nhập chuẩn */}
      <form onSubmit={handleLogin} className="auth-form">
        {import.meta.env.DEV && (
          <details className="rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm">
            <summary className="cursor-pointer font-semibold text-blue-800">Điền nhanh tài khoản demo</summary>
            <div className="mt-3 grid grid-cols-2 gap-2">
              {[
                ['Quản trị viên', 'admin'],
                ['Chủ bãi Vincom', 'owner.vincom'],
                ['Chủ bãi Tân Sơn Nhất', 'owner.tsn'],
                ['Nhân viên cổng', 'staff.vincom'],
                ['Tài xế 1', 'driver1'],
                ['Tài xế 2', 'driver2'],
              ].map(([label, account]) => (
                <button key={account} type="button" disabled={busy}
                  className="rounded-lg border border-blue-200 bg-white px-2 py-2 hover:bg-blue-100"
                  onClick={() => {
                    setIdentifier(account + '@smartparking.vn');
                    setPassword('Demo@123');
                    setOpenProfileAfterLogin(true);
                    setError('');
                  }}>
                  {label}
                </button>
              ))}
            </div>
            <p className="mt-2 text-slate-600">Xác minh CAPTCHA rồi đăng nhập để mở hồ sơ cá nhân.</p>
          </details>
        )}
        <label className="auth-field" htmlFor="login-contact">
          Email hoặc số điện thoại
          <input
            id="login-contact"
            required
            autoComplete="username"
            value={identifier}
            onChange={(e) => {
              setIdentifier(e.target.value);
              setOpenProfileAfterLogin(false);
            }}
            placeholder="Nhập email hoặc số điện thoại"
            disabled={busy}
          />
        </label>
        <label className="auth-field" htmlFor="login-password">
          Mật khẩu
          <div className="auth-input-wrap">
            <input
              id="login-password"
              required
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Nhập mật khẩu của bạn"
              disabled={busy}
            />
            <button
              type="button"
              className="auth-eye"
              onClick={() => setShowPassword(!showPassword)}
              aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              aria-pressed={showPassword}
            >
              {showPassword ? <EyeOff size={17} /> : <Eye size={17} />}
            </button>
          </div>
        </label>
        <div className="text-right"><Link to="/forgot-password" className="auth-text-link text-sm">Quên mật khẩu?</Link></div>
        <TurnstileWidget onToken={setCaptchaToken} attempt={attempt} />
        {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
        <button type="submit" disabled={busy || !captchaToken} className="auth-submit">
          {busy ? 'Đang đăng nhập…' : 'Đăng nhập'}
          {!busy && <ArrowRight size={17} />}
        </button>
      </form>

      <p className="auth-switch">Lần đầu đến ParkMaster? <Link to="/register">Tạo tài khoản</Link></p>
      <p className="auth-secondary-link"><Link className="auth-text-link" to="/register?action=verify">Tiếp tục xác thực tài khoản bằng OTP</Link></p>
    </AuthLayout>
  );
}
