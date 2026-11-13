import { ArrowRight, Check, Eye, EyeOff } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { AuthLayout } from '@/components/auth/AuthLayout';
import { activateOwnerAccount, validateOwnerActivation, type OwnerActivationInfo } from '@/api/ownerActivationApi';
import { passwordProblems, takeHashParams } from './partnerApplyForm';

const HOTLINE = '1900 6868';

type State =
  | { status: 'loading' }
  | { status: 'invalid'; message: string }
  | { status: 'ready'; info: OwnerActivationInfo }
  | { status: 'done'; info: OwnerActivationInfo };

/**
 * /owner/activate#token=… – chủ bãi mở link trong email "Kích hoạt tài khoản chủ bãi" (US-116 U4/U5), tự đặt mật khẩu rồi đăng nhập.
 * Không cần đăng nhập trước; token không nằm trong query string và bị xóa khỏi thanh địa chỉ ngay khi đọc.
 */
export function OwnerActivatePage() {
  const [token] = useState(() => takeHashParams().get('token') ?? '');
  const [state, setState] = useState<State>(() =>
    token ? { status: 'loading' } : { status: 'invalid', message: 'Link kích hoạt không đầy đủ. Mở lại đúng link trong email ParkMaster gửi bạn.' }
  );
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    validateOwnerActivation(token)
      .then((info) => {
        if (!cancelled) setState({ status: 'ready', info });
      })
      .catch((err: unknown) => {
        if (!cancelled) setState({ status: 'invalid', message: err instanceof Error ? err.message : 'Link kích hoạt không hợp lệ.' });
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  const rules = passwordProblems(password);
  const mismatch = confirmation.length > 0 && confirmation !== password;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || state.status !== 'ready') return;
    setError('');
    if (rules.some((r) => !r.met)) {
      setError('Mật khẩu phải có chữ và số, tối thiểu 8 ký tự.');
      return;
    }
    if (password !== confirmation) {
      setError('Mật khẩu xác nhận chưa khớp.');
      return;
    }
    setBusy(true);
    try {
      await activateOwnerAccount(token, password);
      setPassword('');
      setConfirmation('');
      setState({ status: 'done', info: state.info });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không kích hoạt được tài khoản. Vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout>
      {state.status === 'loading' && (
        <>
          <h1 className="auth-heading">Kích hoạt tài khoản chủ bãi</h1>
          <p className="auth-description" role="status">
            Đang kiểm tra link…
          </p>
        </>
      )}

      {state.status === 'invalid' && (
        <>
          <h1 className="auth-heading">Kích hoạt tài khoản chủ bãi</h1>
          <div role="alert" className="auth-alert auth-alert-error">
            {state.message}
          </div>
          <p className="auth-switch">
            Cần link mới? Gọi <a href="tel:19006868">{HOTLINE}</a> · <Link to="/login">Đăng nhập</Link>
          </p>
        </>
      )}

      {state.status === 'ready' && (
        <>
          <h1 className="auth-heading">Kích hoạt tài khoản chủ bãi</h1>
          <p className="auth-description">
            <strong>{state.info.businessName}</strong> · {state.info.fullName}
            <br />
            Đặt mật khẩu cho tài khoản <strong>{state.info.email}</strong>.
          </p>
          <form className="auth-form" onSubmit={handleSubmit} noValidate>
            <label className="auth-field" htmlFor="activate-password">
              Mật khẩu
              <div className="auth-input-wrap">
                <input
                  id="activate-password"
                  type={show ? 'text' : 'password'}
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={busy}
                />
                <button className="auth-eye" type="button" onClick={() => setShow(!show)} aria-label={show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={show}>
                  {show ? <EyeOff size={17} /> : <Eye size={17} />}
                </button>
              </div>
              <ul className="auth-rules" aria-label="Điều kiện mật khẩu">
                {rules.map((r) => (
                  <li key={r.label} className={r.met ? 'is-met' : ''}>
                    {r.label}
                  </li>
                ))}
              </ul>
            </label>
            <label className="auth-field" htmlFor="activate-confirm">
              Xác nhận mật khẩu
              <input
                id="activate-confirm"
                type={show ? 'text' : 'password'}
                autoComplete="new-password"
                value={confirmation}
                onChange={(e) => setConfirmation(e.target.value)}
                aria-invalid={mismatch}
                aria-describedby={mismatch ? 'activate-confirm-error' : undefined}
                disabled={busy}
              />
              {mismatch && (
                <span id="activate-confirm-error" className="auth-field-error">
                  Mật khẩu xác nhận chưa khớp.
                </span>
              )}
            </label>
            {error && (
              <div role="alert" className="auth-alert auth-alert-error">
                {error}
              </div>
            )}
            <button className="auth-submit" type="submit" disabled={busy}>
              {busy ? 'Đang kích hoạt…' : 'Kích hoạt tài khoản'}
              {!busy && <ArrowRight size={17} />}
            </button>
          </form>
        </>
      )}

      {state.status === 'done' && (
        <>
          <div className="auth-success-icon">
            <Check size={30} />
          </div>
          <h1 className="auth-heading">Tài khoản đã sẵn sàng</h1>
          <p className="auth-description">
            Đăng nhập bằng <strong>{state.info.email}</strong> và mật khẩu vừa đặt để khai báo bãi và nộp hồ sơ KYB.
          </p>
          <Link className="auth-submit" to="/login">
            Đến trang đăng nhập <ArrowRight size={17} />
          </Link>
        </>
      )}
    </AuthLayout>
  );
}
