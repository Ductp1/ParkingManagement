import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Check, Circle, Loader2 } from 'lucide-react';
import '@/areas/admin/theme/admin.css';
import { activateOwnerAccount, validateOwnerActivation, type OwnerActivationInfo } from '@/api/ownerActivationApi';
import { passwordProblems, takeHashParams } from './partnerApplyForm';
import { PasswordField } from './partner-register/PasswordField';
import { PublicTopBar } from './partner-register/PublicTopBar';
import { Notice, TextInput } from './partner-register/ui';
import { buttonClass } from './partner-register/styles';

const HOTLINE = '1900 6868';

/** Mã lỗi ProblemDetails.code của U4 / U5 (UserService OwnerAccountUseCases). */
type Problem = 'invalid' | 'expired' | 'activated';

const problemOf = (err: unknown): { problem: Problem; message: string } => {
  const code = (err as { code?: string } | null)?.code;
  const message = err instanceof Error ? err.message : 'Link kích hoạt không hợp lệ.';
  if (code === 'activation_expired') return { problem: 'expired', message };
  if (code === 'activation_used' || code === 'already_activated') return { problem: 'activated', message };
  return { problem: 'invalid', message };
};

type State =
  | { status: 'loading' }
  | { status: 'problem'; problem: Problem; message: string }
  | { status: 'ready'; info: OwnerActivationInfo }
  | { status: 'done'; info: OwnerActivationInfo };

/** Không gửi Referer khi rời trang kích hoạt (token đã bị xóa khỏi URL, đây là lớp phòng thủ thứ hai). */
function useNoReferrer() {
  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'referrer';
    meta.content = 'no-referrer';
    document.head.appendChild(meta);
    return () => meta.remove();
  }, []);
}

const Rule = ({ met, children }: { met: boolean; children: string }) => (
  <li className={`flex items-center gap-2 text-xs ${met ? 'text-(--admin-status-success-text)' : 'text-(--admin-text-secondary)'}`}>
    {met ? <Check size={14} aria-hidden="true" /> : <Circle size={14} aria-hidden="true" className="text-(--admin-chart-neutral)" />}
    <span>{children}</span>
    <span className="sr-only">{met ? ' – đã đạt' : ' – chưa đạt'}</span>
  </li>
);

/**
 * /owner/activate#token=… – chủ bãi mở link trong email "Kích hoạt tài khoản chủ bãi" (US-116 U4/U5), tự đặt mật khẩu rồi đăng nhập.
 * Thiết kế cùng ngôn ngữ với trang Đăng ký đối tác (Figma 221:139): nền phẳng, card trắng 440px. Token chỉ giữ trong bộ nhớ, bị xóa khỏi
 * thanh địa chỉ ngay khi đọc; không ghi log; chống gửi hai lần.
 */
export function OwnerActivatePage() {
  useNoReferrer();
  const navigate = useNavigate();
  const [token] = useState(() => takeHashParams().get('token') ?? '');
  const [state, setState] = useState<State>(() =>
    token
      ? { status: 'loading' }
      : { status: 'problem', problem: 'invalid', message: 'Link kích hoạt không đầy đủ. Mở lại đúng link trong email ParkMaster gửi bạn.' }
  );
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!token) return undefined;
    let cancelled = false;
    validateOwnerActivation(token)
      .then((info) => {
        if (!cancelled) setState({ status: 'ready', info });
      })
      .catch((err: unknown) => {
        if (!cancelled) setState({ status: 'problem', ...problemOf(err) });
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  const rules = passwordProblems(password);
  const strong = rules.every((r) => r.met);
  const matches = confirmation.length > 0 && confirmation === password;
  const canSubmit = strong && matches && !busy;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit || state.status !== 'ready') return;
    setError('');
    setBusy(true);
    try {
      await activateOwnerAccount(token, password);
      setPassword('');
      setConfirmation('');
      setState({ status: 'done', info: state.info });
    } catch (err) {
      const { problem, message } = problemOf(err);
      // Link hỏng / hết hạn giữa chừng → màn hình riêng; lỗi khác (mật khẩu bị từ chối…) → hiện nguyên văn ngay trên form.
      if (problem !== 'invalid' || (err as { code?: string }).code === 'activation_invalid') setState({ status: 'problem', problem, message });
      else setError(message);
    } finally {
      setBusy(false);
    }
  }

  const goLogin = (email?: string) => navigate('/login', { state: email ? { email } : undefined });

  return (
    <div className="admin-scope min-h-screen bg-(--admin-surface-page)">
      <PublicTopBar />
      <main className="mx-auto flex w-full max-w-[1120px] justify-center px-4 py-10 md:px-8 md:py-14">
        <section
          className="w-full max-w-[440px] rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-6 md:p-8"
          aria-labelledby="activate-title"
        >
          {state.status === 'loading' && (
            <>
              <h1 id="activate-title" className="text-xl font-bold text-(--admin-text-primary)">
                Kích hoạt tài khoản chủ bãi
              </h1>
              <p role="status" className="mt-4 flex items-center gap-2 text-sm text-(--admin-text-secondary)">
                <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />
                Đang kiểm tra link…
              </p>
            </>
          )}

          {state.status === 'problem' && (
            <div className="flex flex-col gap-4">
              <h1 id="activate-title" className="text-xl font-bold text-(--admin-text-primary)">
                {state.problem === 'expired'
                  ? 'Link kích hoạt đã hết hạn'
                  : state.problem === 'activated'
                    ? 'Tài khoản đã được kích hoạt'
                    : 'Link kích hoạt không hợp lệ'}
              </h1>
              <div role="alert">
                <Notice tone={state.problem === 'activated' ? 'info' : 'danger'} title={state.message} />
              </div>
              {state.problem === 'activated' ? (
                <button type="button" className={buttonClass('primary')} onClick={() => goLogin()}>
                  Đăng nhập
                </button>
              ) : (
                <p className="text-sm text-(--admin-text-secondary)">
                  ParkMaster sẽ gửi link mới sau khi xác minh. Gọi{' '}
                  <a className="font-semibold text-(--admin-text-primary) underline" href="tel:19006868">
                    {HOTLINE}
                  </a>{' '}
                  để nhận link kích hoạt mới.
                </p>
              )}
              <Link to="/" className="text-sm font-semibold text-(--admin-brand-navy-900) hover:underline">
                Về trang chủ
              </Link>
            </div>
          )}

          {state.status === 'ready' && (
            <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-5">
              <div>
                <h1 id="activate-title" className="text-xl font-bold text-(--admin-text-primary)">
                  Kích hoạt tài khoản chủ bãi
                </h1>
                <p className="mt-2 truncate text-sm font-semibold text-(--admin-text-primary)" title={state.info.businessName}>
                  {state.info.businessName}
                </p>
                <p className="text-sm text-(--admin-text-secondary)">Người đại diện: {state.info.fullName}</p>
              </div>

              <TextInput id="activate-email" label="Email đăng nhập" value={state.info.email} readOnly locked />

              <PasswordField label="Mật khẩu" value={password} onChange={setPassword} disabled={busy} describedBy="activate-rules" autoFocus />
              <ul id="activate-rules" aria-label="Điều kiện mật khẩu" aria-live="polite" className="-mt-2 flex flex-col gap-1">
                {rules.map((r) => (
                  <Rule key={r.label} met={r.met}>
                    {r.label}
                  </Rule>
                ))}
              </ul>

              <PasswordField label="Xác nhận mật khẩu" value={confirmation} onChange={setConfirmation} disabled={busy} describedBy="activate-match" invalid={confirmation.length > 0 && !matches} />
              <ul id="activate-match" aria-live="polite" className="-mt-2">
                <Rule met={matches}>Mật khẩu khớp</Rule>
              </ul>

              {error && (
                <div role="alert">
                  <Notice tone="danger" title={error} />
                </div>
              )}

              <button type="submit" disabled={!canSubmit} aria-busy={busy || undefined} className={`${buttonClass('primary')} h-11 w-full`}>
                {busy ? (
                  <>
                    <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />
                    Đang kích hoạt…
                  </>
                ) : (
                  'Kích hoạt tài khoản'
                )}
              </button>
            </form>
          )}

          {state.status === 'done' && (
            <div className="flex flex-col gap-4">
              <span className="flex size-12 items-center justify-center rounded-full bg-(--admin-surface-selected) text-(--admin-brand-navy-900)" aria-hidden="true">
                <Check size={22} />
              </span>
              <h1 id="activate-title" tabIndex={-1} className="text-xl font-bold text-(--admin-text-primary)">
                Tài khoản đã sẵn sàng
              </h1>
              <p className="text-sm text-(--admin-text-secondary)">
                Đăng nhập bằng <strong className="text-(--admin-text-primary)">{state.info.email}</strong> và mật khẩu vừa đặt để khai báo bãi và nộp hồ sơ KYB.
              </p>
              <button type="button" className={`${buttonClass('primary')} h-11 w-full`} onClick={() => goLogin(state.info.email)}>
                Đăng nhập
              </button>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
