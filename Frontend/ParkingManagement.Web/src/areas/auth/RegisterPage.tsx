import { AuthLayout } from '@/components/auth/AuthLayout';
import { ArrowRight, Check, Eye, EyeOff } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import {
  registerAccount,
  resendRegistrationOtp,
  verifyRegistration,
} from '@/api/authApi';
import { OtpInput } from '@/components/auth/OtpInput';
import { useAppSelector } from '@/store';

export function RegisterPage() {
  const [searchParams] = useSearchParams();
  const initialAction = searchParams.get('action');

  const { isAuthenticated, isLoading } = useAppSelector((s) => s.auth);

  // Tiến trình: 'register' -> 'verify' -> 'done'
  const [step, setStep] = useState<'register' | 'verify' | 'done'>(
    initialAction === 'verify' ? 'verify' : 'register'
  );

  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState(searchParams.get('email') || '');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);

  // Mã OTP 6 chữ số
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  // Đếm ngược 60s giãn cách gửi lại & 300s thời hạn OTP (TV1)
  const [retryAt, setRetryAt] = useState(0);
  const [expiresAt, setExpiresAt] = useState(0);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!retryAt && !expiresAt) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [retryAt, expiresAt]);

  // Điều kiện mật khẩu (khớp kiểm tra ở handleRegister và phía server)
  const passwordRules = [
    { label: 'Ít nhất 8 ký tự', met: password.length >= 8 && new TextEncoder().encode(password).length <= 72 },
    { label: 'Có chữ cái', met: /\p{L}/u.test(password) },
    { label: 'Có chữ số', met: /\p{Nd}/u.test(password) },
  ];
  const confirmMismatch = confirmation.length > 0 && confirmation !== password;

  const remainingResend = Math.max(0, Math.ceil((retryAt - now) / 1000));
  const remainingExpire = expiresAt > 0 ? Math.max(0, Math.ceil((expiresAt - now) / 1000)) : 0;

  const formatExpireTime = (totalSeconds: number) => {
    const mins = Math.floor(totalSeconds / 60);
    const secs = totalSeconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  const startTimers = () => {
    const time = Date.now();
    setNow(time);
    setRetryAt(time + 60_000); // 60s
    setExpiresAt(time + 300_000); // 300s (5 phút)
  };

  // Đăng ký tài khoản (Cố định vai trò Driver)
  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setError('');
    setMessage('');

    if (password !== confirmation) {
      setError('Mật khẩu xác nhận chưa khớp.');
      return;
    }
    if (
      password.length < 8 ||
      new TextEncoder().encode(password).length > 72 ||
      !/\p{L}/u.test(password) ||
      !/\p{Nd}/u.test(password)
    ) {
      setError('Mật khẩu phải có chữ và số, tối thiểu 8 ký tự.');
      return;
    }
    if (!fullName.trim()) {
      setError('Vui lòng nhập họ và tên của bạn.');
      return;
    }

    setBusy(true);
    try {
      // Chỉ đăng ký tài khoản khách hàng Driver
      await registerAccount({
        fullName: fullName.trim(),
        contact: email.trim(),
        password,
        role: 'Driver',
      });
      setEmail(email.trim());
      setPassword('');
      setConfirmation('');
      setCode('');
      setStep('verify');
      startTimers();
      setMessage('Mã OTP 6 số đã được gửi qua email. Vui lòng kiểm tra hộp thư.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Đăng ký thất bại. Vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  }

  // Gửi lại mã OTP
  async function handleResendOtp() {
    if (busy || remainingResend > 0) return;
    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setError('Vui lòng nhập email hoặc số điện thoại nhận mã.');
      return;
    }

    setBusy(true);
    setError('');
    setMessage('');
    try {
      await resendRegistrationOtp(trimmedEmail);
      setCode('');
      startTimers();
      setMessage(`Mã OTP mới đã gửi đến ${trimmedEmail}.`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể gửi lại mã OTP.');
    } finally {
      setBusy(false);
    }
  }

  // Xác thực mã OTP
  async function handleVerifyOtp(codeToVerify?: string) {
    const finalCode = (codeToVerify || code).trim();
    if (busy) return;
    setError('');
    setMessage('');

    if (!/^[0-9]{6}$/.test(finalCode)) {
      setError('Mã OTP phải gồm 6 chữ số.');
      return;
    }

    setBusy(true);
    try {
      await verifyRegistration(email.trim(), finalCode);
      setCode('');
      setStep('done');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Mã OTP không đúng hoặc đã hết hạn.');
    } finally {
      setBusy(false);
    }
  }

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

  if (isAuthenticated) {
    return <Navigate to="/driver" replace />;
  }

  return (
    <AuthLayout>
      <nav className="auth-tabs" aria-label="Tài khoản"><Link to="/login">Đăng nhập</Link><Link to="/register" aria-current="page" onClick={() => { setStep('register'); setError(''); setMessage(''); }}>Tạo tài khoản</Link></nav>
      <ol className="auth-steps" aria-label="Các bước tạo tài khoản">
        {(['Thông tin', 'Xác minh', 'Hoàn tất'] as const).map((label, i) => {
          const current = step === 'register' ? 0 : step === 'verify' ? 1 : 2;
          return (
            <li key={label} className={i < current ? 'is-done' : i === current ? 'is-current' : ''} aria-current={i === current ? 'step' : undefined}>
              {i + 1} {label}
            </li>
          );
        })}
      </ol>
      {step === 'register' && <>
        <h1 className="auth-heading">Tạo tài khoản tài xế</h1>
        <p className="auth-description">Chỉ cần tên, email và mật khẩu. Chúng tôi gửi mã xác minh về email của bạn.</p>
        <form onSubmit={handleRegister} className="auth-form">
          <label className="auth-field" htmlFor="register-name">Họ và tên<input id="register-name" required maxLength={150} autoComplete="name" value={fullName} onChange={e => setFullName(e.target.value)} placeholder="Nguyễn Văn An" disabled={busy} /></label>
          <label className="auth-field" htmlFor="register-email">Địa chỉ email<input id="register-email" required type="email" maxLength={256} autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="ban@example.com" disabled={busy} /></label>
          <label className="auth-field" htmlFor="register-password">Mật khẩu<div className="auth-input-wrap"><input id="register-password" required minLength={8} type={showPassword ? 'text' : 'password'} autoComplete="new-password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Tạo mật khẩu của bạn" disabled={busy} /><button className="auth-eye" type="button" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={showPassword}>{showPassword ? <EyeOff size={17} /> : <Eye size={17} />}</button></div><ul className="auth-rules" aria-label="Điều kiện mật khẩu">{passwordRules.map(r => <li key={r.label} className={r.met ? 'is-met' : ''}>{r.label}</li>)}</ul></label>
          <label className="auth-field" htmlFor="register-confirm">Xác nhận mật khẩu<div className="auth-input-wrap"><input id="register-confirm" required aria-invalid={confirmMismatch} aria-describedby={confirmMismatch ? 'register-confirm-error' : undefined} type={showConfirmation ? 'text' : 'password'} autoComplete="new-password" value={confirmation} onChange={e => setConfirmation(e.target.value)} placeholder="Nhập lại mật khẩu" disabled={busy} /><button className="auth-eye" type="button" onClick={() => setShowConfirmation(!showConfirmation)} aria-label={showConfirmation ? 'Ẩn mật khẩu xác nhận' : 'Hiện mật khẩu xác nhận'} aria-pressed={showConfirmation}>{showConfirmation ? <EyeOff size={17} /> : <Eye size={17} />}</button></div>{confirmMismatch && <span id="register-confirm-error" className="auth-field-error">Mật khẩu xác nhận chưa khớp.</span>}</label>
          {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
          <button className="auth-submit" type="submit" disabled={busy}>{busy ? 'Đang tạo tài khoản…' : 'Tạo tài khoản'}{!busy && <ArrowRight size={17} />}</button>
        </form>
        <p className="auth-switch">Đã có tài khoản? <Link to="/login">Đăng nhập</Link></p>
        <p className="auth-switch">Bạn là chủ bãi xe? <Link to="/partner/register">Đăng ký làm đối tác</Link></p>
      </>}
      {step === 'verify' && <>
        <h1 className="auth-heading">Nhập mã xác minh</h1>
        <p className="auth-description">Mã gồm 6 chữ số đã được gửi đến email của bạn và có hiệu lực trong 5 phút.</p>
        <form className="auth-form" onSubmit={event => { event.preventDefault(); void handleVerifyOtp(); }}>
          <label className="auth-field" htmlFor="verify-email">Email hoặc số điện thoại nhận mã<input id="verify-email" required maxLength={256} autoComplete="username" value={email} onChange={e => { setEmail(e.target.value); setCode(''); setExpiresAt(0); setRetryAt(0); }} disabled={busy || initialAction !== 'verify'} /></label>
          <div><span className="auth-field" style={{ marginBottom: 12 }}>Mã xác minh</span><OtpInput className="auth-otp" value={code} onChange={setCode} disabled={busy} hasError={!!error} autoFocus={!!email} />{expiresAt > 0 && <p className="auth-otp-timer">{remainingExpire > 0 ? <>Mã có hiệu lực trong <strong>{formatExpireTime(remainingExpire)}</strong></> : 'Mã đã hết hạn. Vui lòng gửi lại mã.'}</p>}</div>
          {message && <div role="status" className="auth-alert auth-alert-success">{message}</div>}
          {error && <div role="alert" className="auth-alert auth-alert-error">{error}</div>}
          <button className="auth-submit" type="submit" disabled={busy || code.length !== 6 || !email.trim()}>{busy ? 'Đang xác thực…' : 'Xác thực tài khoản'}{!busy && <ArrowRight size={17} />}</button>
        </form>
        <button className="auth-resend" type="button" disabled={busy || remainingResend > 0 || !email.trim()} onClick={() => void handleResendOtp()}>{remainingResend > 0 ? `Gửi lại mã sau ${remainingResend}s` : 'Chưa nhận được mã? Gửi lại OTP'}</button>
        <p className="auth-switch">Kiểm tra hộp thư, thư mục spam hoặc tin nhắn nhận mã.<br /><Link to="/login">Quay lại đăng nhập</Link></p>
      </>}
      {step === 'done' && <>
        <div className="auth-success-icon"><Check size={30} /></div>
        <h1 className="auth-heading">Tài khoản đã sẵn sàng</h1>
        <p className="auth-description"><strong>{email}</strong> đã được xác minh. Đăng nhập để tìm và đặt chỗ đỗ.</p>
        <Link className="auth-submit" to="/login">Đến trang đăng nhập <ArrowRight size={17} /></Link>
      </>}
    </AuthLayout>
  );
}

