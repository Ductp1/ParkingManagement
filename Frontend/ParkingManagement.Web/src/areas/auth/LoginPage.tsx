import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { login } from '@/api/authApi';
import { TurnstileWidget } from '@/components/auth/TurnstileWidget';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser, setToken } from '@/store/slices/authSlice';

export function LoginPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { isAuthenticated, isLoading } = useAppSelector(s => s.auth);
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [captchaToken, setCaptchaToken] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  if (isLoading) return <p role="status" className="p-8">Đang kiểm tra phiên…</p>;
  if (isAuthenticated) return <Navigate to="/driver" replace />;
  return <main className="min-h-screen bg-slate-50 flex items-center justify-center p-4">
    <form className="w-full max-w-md bg-white rounded-xl shadow p-4 sm:p-8 space-y-5" onSubmit={async e => {
      e.preventDefault();
      if (busy || !captchaToken) return;
      setBusy(true); setError('');
      try {
        const user = await login(identifier, password, captchaToken);
        dispatch(setToken(sessionStorage.getItem('parkmaster_token')));
        dispatch(setUser(user));
        navigate(user.role === 'Admin' ? '/admin' : user.role === 'Owner' ? '/owner' :
          user.role === 'Staff' ? '/gate' : '/driver', { replace: true });
      } catch (err) { setError(err instanceof Error ? err.message : 'Đăng nhập thất bại.'); }
      finally { setBusy(false); setCaptchaToken(''); setAttempt(n => n + 1); }
    }}>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Đăng nhập ParkMaster</h1>
        <Link to="/" className="text-xs font-semibold text-blue-600 hover:underline">← Trang chủ</Link>
      </div>
      <label className="block">Email hoặc số điện thoại
        <input required autoComplete="username" value={identifier} onChange={e => setIdentifier(e.target.value)}
          className="mt-2 w-full border rounded-lg p-3" disabled={busy} />
      </label>
      <label className="block">Mật khẩu
        <input required type="password" autoComplete="current-password" value={password}
          onChange={e => setPassword(e.target.value)} className="mt-2 w-full border rounded-lg p-3" disabled={busy} />
      </label>
      <TurnstileWidget onToken={setCaptchaToken} attempt={attempt} />
      <button type="button" disabled={busy} onClick={() => { setCaptchaToken(''); setAttempt(n => n + 1); }}
        className="text-sm text-blue-600">Tải lại xác thực</button>
      {error && <p role="alert" className="text-red-600">{error}</p>}
      <button disabled={busy || !captchaToken} className="w-full bg-blue-600 text-white rounded-lg p-3 disabled:opacity-50">
        {busy ? 'Đang đăng nhập…' : 'Đăng nhập'}
      </button>
    </form>
  </main>;
}
