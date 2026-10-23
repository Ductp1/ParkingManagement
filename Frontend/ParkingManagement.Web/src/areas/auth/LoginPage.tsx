import { AuthLayout } from '@/components/auth/AuthLayout';
import { ArrowRight, Eye, EyeOff } from 'lucide-react';
import React, { useState } from 'react';
import { Link, Navigate, useNavigate, useLocation } from 'react-router-dom';
import { login } from '@/api/authApi';
import { TurnstileWidget } from '@/components/auth/TurnstileWidget';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser, setToken } from '@/store/slices/authSlice';
import type { UserRole, AuthUser } from '@/types/auth';

interface DemoAccount {
  role: UserRole;
  label: string;
  name: string;
  email: string;
  roles: UserRole[];
  color: string;
  icon: string;
  targetPath: string;
}

const DEMO_ACCOUNTS: DemoAccount[] = [
  {
    role: 'Driver',
    label: 'Tài xế (Driver)',
    name: 'Nguyễn Văn An',
    email: 'driver1@smartparking.vn',
    roles: ['Driver'],
    color: 'border-blue-200 bg-blue-50/40 hover:bg-blue-50 text-blue-900',
    icon: 'directions_car',
    targetPath: '/driver',
  },
  {
    role: 'Owner',
    label: 'Chủ bãi (Owner)',
    name: 'Vincom Parking',
    email: 'owner.vincom@smartparking.vn',
    roles: ['Owner', 'Driver'],
    color: 'border-indigo-200 bg-indigo-50/40 hover:bg-indigo-50 text-indigo-900',
    icon: 'domain',
    targetPath: '/owner',
  },
  {
    role: 'Staff',
    label: 'Trực cổng (Gate Staff)',
    name: 'Lê Văn Cổng',
    email: 'staff.vincom@smartparking.vn',
    roles: ['Staff'],
    color: 'border-amber-200 bg-amber-50/40 hover:bg-amber-50 text-amber-900',
    icon: 'fence',
    targetPath: '/gate',
  },
  {
    role: 'Admin',
    label: 'Quản trị (Admin)',
    name: 'Ban Quản Trị Hệ Thống',
    email: 'admin@smartparking.vn',
    roles: ['Admin'],
    color: 'border-purple-200 bg-purple-50/40 hover:bg-purple-50 text-purple-900',
    icon: 'admin_panel_settings',
    targetPath: '/admin',
  },
];

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

  if (isLoading) {
    return (
      <main className="min-h-screen bg-white flex items-center justify-center p-4">
        <div className="flex items-center gap-3 px-6 py-3.5 rounded-2xl bg-white border border-slate-200 text-sm font-semibold text-slate-700 shadow-xl">
          <i className="fa-solid fa-circle-notch fa-spin text-violet-600" />
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

    if (fromPath) {
      if (fromPath.startsWith('/admin') && userRoles.includes('Admin')) targetRoute = fromPath;
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

  // Đăng nhập nhanh theo từng vai trò cụ thể để kiểm thử
  const handleQuickRoleLogin = async (account: DemoAccount) => {
    setBusy(true);
    setError('');
    setIdentifier(account.email);
    setPassword('Demo@123');

    try {
      // Cố gắng gọi API login thật tới UserService
      const loggedUser = await login(account.email, 'Demo@123', 'turnstile-local-test-token');
      dispatch(setToken(sessionStorage.getItem('parkmaster_token')));
      dispatch(setUser(loggedUser));
      navigateToRoleDashboard(loggedUser);
    } catch {
      // Nếu backend chưa chạy (offline / dev mode), khởi tạo phiên riêng biệt cho vai trò đó
      const mockUser: AuthUser = {
        id: account.role === 'Admin' ? 1 : account.role === 'Owner' ? 2 : account.role === 'Staff' ? 4 : 5,
        fullName: account.name,
        email: account.email,
        role: account.role,
        roles: account.roles,
      };
      sessionStorage.setItem('parkmaster_token', `demo-token-${account.role.toLowerCase()}`);
      dispatch(setToken(`demo-token-${account.role.toLowerCase()}`));
      dispatch(setUser(mockUser));
      navigateToRoleDashboard(mockUser);
    } finally {
      setBusy(false);
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
      <p className="auth-description">Đăng nhập để vào đúng phân hệ vai trò của bạn.</p>

      {/* Form đăng nhập chuẩn */}
      <form onSubmit={handleLogin} className="auth-form">
        <label className="auth-field" htmlFor="login-contact">
          Email hoặc số điện thoại
          <input
            id="login-contact"
            required
            autoComplete="username"
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
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
        <TurnstileWidget onToken={setCaptchaToken} attempt={attempt} />
        {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
        <button type="submit" disabled={busy || !captchaToken} className="auth-submit">
          {busy ? 'Đang đăng nhập…' : 'Đăng nhập'}
          {!busy && <ArrowRight size={17} />}
        </button>
      </form>

      {/* QUICK ROLE ACCOUNTS: Đăng nhập theo từng tài khoản vai trò riêng biệt */}
      <div className="mt-8 pt-6 border-t border-slate-200">
        <div className="flex items-center justify-between mb-3">
          <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500">
            Đăng nhập theo vai trò tài khoản:
          </span>
          <span className="text-[10px] text-slate-400 font-mono">Demo@123</span>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {DEMO_ACCOUNTS.map((acc) => (
            <button
              key={acc.role}
              type="button"
              disabled={busy}
              onClick={() => handleQuickRoleLogin(acc)}
              className={`p-2.5 rounded-xl border text-left transition-all hover:shadow-xs flex items-center justify-between gap-2 cursor-pointer ${acc.color}`}
            >
              <div className="flex items-center gap-2 min-w-0">
                <span className="material-symbols-outlined text-[20px] shrink-0">{acc.icon}</span>
                <div className="min-w-0">
                  <div className="text-xs font-bold truncate">{acc.label}</div>
                  <div className="text-[10px] text-slate-500 truncate">{acc.email}</div>
                </div>
              </div>
              <span className="text-[10px] font-mono font-semibold px-1.5 py-0.5 rounded bg-white/70 shrink-0">
                {acc.targetPath}
              </span>
            </button>
          ))}
        </div>
      </div>

      <p className="auth-switch mt-6">Lần đầu đến ParkMaster? <Link to="/register">Tạo tài khoản</Link></p>
      <p className="auth-secondary-link"><Link className="auth-text-link" to="/register?action=verify">Tiếp tục xác thực tài khoản bằng OTP</Link></p>
    </AuthLayout>
  );
}
