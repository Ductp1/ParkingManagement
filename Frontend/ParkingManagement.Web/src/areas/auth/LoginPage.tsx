import { AuthLayout } from '@/components/auth/AuthLayout';
import { ArrowRight, Eye, EyeOff } from 'lucide-react';
import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { login } from '@/api/authApi';
import { TurnstileWidget } from '@/components/auth/TurnstileWidget';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser, setToken } from '@/store/slices/authSlice';

export function LoginPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { isAuthenticated, isLoading } = useAppSelector((s) => s.auth);

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

  if (isAuthenticated) {
    return <Navigate to="/driver" replace />;
  }

  const handleLogin = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (busy || !captchaToken) return;
    setBusy(true);
    setError('');

    try {
      const user = await login(identifier, password, captchaToken);
      dispatch(setToken(sessionStorage.getItem('parkmaster_token')));
      dispatch(setUser(user));
      navigate(
        user.role === 'Admin'
          ? '/admin'
          : user.role === 'Owner'
            ? '/owner'
            : user.role === 'Staff'
              ? '/gate'
              : '/driver',
        { replace: true }
      );
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
      <nav className="auth-tabs" aria-label="Tài khoản"><Link to="/login" aria-current="page">Đăng nhập</Link><Link to="/register">Tạo tài khoản</Link></nav>
      <span className="auth-kicker">CHÀO MỪNG TRỞ LẠI</span>
      <h1 className="auth-heading">Hành trình mới,<br />bắt đầu từ đây.</h1>
      <p className="auth-description">Đăng nhập để tiếp tục cùng ParkMaster.</p>
      <form onSubmit={handleLogin} className="auth-form">
        <label className="auth-field" htmlFor="login-contact">Email hoặc số điện thoại<input id="login-contact" required autoComplete="username" value={identifier} onChange={e => setIdentifier(e.target.value)} placeholder="Nhập email hoặc số điện thoại" disabled={busy} /></label>
        <label className="auth-field" htmlFor="login-password">Mật khẩu<div className="auth-input-wrap"><input id="login-password" required type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Nhập mật khẩu của bạn" disabled={busy} /><button type="button" className="auth-eye" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={showPassword}>{showPassword ? <EyeOff size={17} /> : <Eye size={17} />}</button></div></label>
        <TurnstileWidget onToken={setCaptchaToken} attempt={attempt} />
        {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
        <button type="submit" disabled={busy || !captchaToken} className="auth-submit">{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}{!busy && <ArrowRight size={17} />}</button>
      </form>
      <p className="auth-switch">Lần đầu đến ParkMaster? <Link to="/register">Tạo tài khoản</Link></p>
      <p className="auth-secondary-link"><Link className="auth-text-link" to="/register?action=verify">Tiếp tục xác thực tài khoản bằng OTP</Link></p>
    </AuthLayout>
  );
}
