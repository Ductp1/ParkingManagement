import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Check, Eye, EyeOff } from 'lucide-react';
import { AuthLayout } from '@/components/auth/AuthLayout';
import { OtpInput } from '@/components/auth/OtpInput';
import { forgotPassword, resetPassword, clearSession } from '@/api/authApi';
import { passwordError } from '@/utils/password';
import { useAppDispatch } from '@/store';
import { logout } from '@/store/slices/authSlice';

export function ForgotPasswordPage() {
  const dispatch = useAppDispatch();
  const [step, setStep] = useState<'contact' | 'reset' | 'done'>('contact');
  const [contact, setContact] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [visible, setVisible] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [sentAt, setSentAt] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!sentAt || step !== 'reset') return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [sentAt, step]);
  const retry = Math.max(0, Math.ceil((sentAt + 60_000 - now) / 1000));
  const expiry = Math.max(0, Math.ceil((sentAt + 300_000 - now) / 1000));

  async function send(event?: FormEvent) {
    event?.preventDefault();
    if (busy || (step === 'reset' && retry > 0)) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await forgotPassword(contact.trim());
      const time = Date.now(); setSentAt(time); setNow(time);
      setCode(''); setStep('reset');
      setMessage('Nếu tài khoản đủ điều kiện, mã xác minh sẽ được gửi. Vui lòng kiểm tra hộp thư hoặc tin nhắn.');
    } catch (err) { setError(err instanceof Error ? err.message : 'Chưa thể gửi mã. Vui lòng thử lại.'); }
    finally { setBusy(false); }
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setError(''); setMessage('');
    const invalid = passwordError(password);
    if (invalid) { setError(invalid); return; }
    if (password !== confirm) { setError('Mật khẩu xác nhận chưa khớp.'); return; }
    if (!/^[0-9]{6}$/.test(code)) { setError('Vui lòng nhập đủ 6 chữ số OTP.'); return; }
    setBusy(true);
    try {
      await resetPassword(contact.trim(), code, password);
      clearSession(); dispatch(logout());
      setPassword(''); setConfirm(''); setCode(''); setStep('done');
    } catch (err) { setError(err instanceof Error ? err.message : 'Không thể đặt lại mật khẩu.'); }
    finally { setBusy(false); }
  }
  return <AuthLayout>
    <nav className="auth-tabs" aria-label="Tài khoản"><Link to="/login">Đăng nhập</Link><Link to="/register">Tạo tài khoản</Link></nav>
    <span className="auth-kicker">KHÔI PHỤC TÀI KHOẢN</span>
    {step === 'done' ? <>
      <div className="auth-success-icon"><Check size={28} /></div>
      <h1 className="auth-heading">Sẵn sàng cho<br />hành trình mới.</h1>
      <p className="auth-description">Mật khẩu đã được cập nhật. Các phiên đăng nhập cũ đã kết thúc; hãy đăng nhập bằng mật khẩu mới.</p>
      <Link to="/login" className="auth-submit">Về đăng nhập <ArrowRight size={17} /></Link>
    </> : <>
      <h1 className="auth-heading">{step === 'contact' ? <>Quên mật khẩu?<br />Để mình giúp bạn.</> : <>Mật khẩu mới,<br />khởi đầu an tâm.</>}</h1>
      <p className="auth-description">{step === 'contact' ? 'Nhập email hoặc số điện thoại đã xác minh để nhận mã khôi phục tài khoản.' : <>Nhập mã xác minh gửi đến <strong className="break-all">{contact}</strong> và chọn mật khẩu mới.</>}</p>
      {step === 'contact' ? <form onSubmit={send} className="auth-form">
        <label className="auth-field">Email hoặc số điện thoại<input autoComplete="username" required maxLength={256} value={contact} onChange={e => setContact(e.target.value)} disabled={busy} placeholder="Email hoặc số điện thoại của bạn" /></label>
        {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
        <button className="auth-submit" disabled={busy || !contact.trim()}>{busy ? 'Đang gửi…' : 'Gửi mã xác minh'}<ArrowRight size={17} /></button>
      </form> : <form onSubmit={submit} className="auth-form">
        {message && <div role="status" className="auth-alert auth-alert-success">{message}</div>}
        <div><p className="auth-hint mb-3">Mã xác minh gồm 6 chữ số</p><OtpInput className="auth-otp" value={code} onChange={setCode} disabled={busy} hasError={!!error} /></div>
        <p className="auth-otp-timer">{expiry > 0 ? <>Thời hạn mã tối đa <strong>{Math.floor(expiry / 60)}:{String(expiry % 60).padStart(2, '0')}</strong></> : 'Mã có thể đã hết hạn. Vui lòng gửi lại để tiếp tục.'}</p>
        <label className="auth-field">Mật khẩu mới<div className="auth-input-wrap"><input type={visible ? 'text' : 'password'} autoComplete="new-password" minLength={8} required value={password} onChange={e => setPassword(e.target.value)} disabled={busy} /><button type="button" className="auth-eye" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? <EyeOff size={17} /> : <Eye size={17} />}</button></div></label>
        <p className="auth-hint">Ít nhất 8 ký tự, gồm chữ và số. Nhập sai OTP 3 lần sẽ khóa tài khoản 15 phút.</p>
        <label className="auth-field">Xác nhận mật khẩu<input type="password" autoComplete="new-password" required value={confirm} onChange={e => setConfirm(e.target.value)} disabled={busy} /></label>
        {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
        <button className="auth-submit" disabled={busy || code.length !== 6 || expiry === 0}>{busy ? 'Đang cập nhật…' : 'Đặt lại mật khẩu'}<ArrowRight size={17} /></button>
        <button type="button" className="auth-resend" disabled={busy || retry > 0} onClick={() => void send()}>{retry > 0 ? `Gửi lại sau ${retry}s` : 'Gửi lại mã xác minh'}</button>
        <button type="button" className="auth-resend" disabled={busy} onClick={() => { setStep('contact'); setCode(''); setPassword(''); setConfirm(''); setError(''); setMessage(''); }}>Đổi email hoặc số điện thoại</button>
      </form>}
      <p className="auth-switch"><Link to="/login">Quay lại đăng nhập</Link></p>
    </>}
  </AuthLayout>;
}
