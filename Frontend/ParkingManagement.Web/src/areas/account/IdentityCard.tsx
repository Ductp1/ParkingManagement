import { useEffect, useRef, useState, type FormEvent } from 'react';
import { AlertCircle, BadgeCheck, Check, Clock, Fingerprint, Loader2, ShieldCheck, X } from 'lucide-react';
import { getMyKyc, submitCitizenId } from '@/api/accountApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser } from '@/store/slices/authSlice';
import type { KycStatusView } from '@/types/account';
import { kycLabels } from './accountFormat';

/** Chỉ giữ chữ số, nhóm 4-4-4 cho dễ đọc khi gõ. */
const formatNumber = (raw: string) => raw.replace(/\D/g, '').slice(0, 12).replace(/(\d{4})(?=\d)/g, '$1 ');

type Step = 'enter' | 'connecting' | 'done';

/**
 * Xác minh danh tính qua VNeID — BẢN GIẢ LẬP.
 * Hộp thoại mô phỏng bước liên kết: nhập số CCCD → "kết nối" → xong. Không gọi VNeID thật;
 * số CCCD được gửi tới UserService (POST /me/kyc/citizen-id), mã hóa khi lưu và chờ Admin duyệt.
 */
export function IdentityCard() {
  const dispatch = useAppDispatch();
  const user = useAppSelector((s) => s.auth.user);
  const dialog = useRef<HTMLDialogElement>(null);
  const [kyc, setKyc] = useState<KycStatusView | null>(null);
  const [loadError, setLoadError] = useState('');
  const [step, setStep] = useState<Step>('enter');
  const [number, setNumber] = useState('');
  const [touched, setTouched] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    getMyKyc()
      .then((data) => { if (!cancelled) setKyc(data); })
      .catch((err: unknown) => { if (!cancelled) setLoadError(err instanceof Error ? err.message : 'Không tải được trạng thái xác minh.'); });
    return () => { cancelled = true; };
  }, []);

  const digits = number.replace(/\D/g, '');
  const invalid = digits.length !== 12;
  const status = kyc?.status ?? user?.kycStatus ?? 'NotSubmitted';
  const label = kycLabels[status];
  const canSubmit = status === 'NotSubmitted' || status === 'Rejected';

  function open() {
    setStep('enter');
    setNumber('');
    setTouched(false);
    setError('');
    dialog.current?.showModal();
  }

  function close() {
    if (step === 'connecting') return;
    dialog.current?.close();
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (invalid) return;
    setError('');
    setStep('connecting');
    try {
      // Giữ màn "đang kết nối" tối thiểu ~1,2 giây cho giống một bước liên kết thật
      const [next] = await Promise.all([submitCitizenId(digits), new Promise((r) => setTimeout(r, 1200))]);
      setKyc(next);
      if (user) dispatch(setUser({ ...user, kycStatus: next.status }));
      setStep('done');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Không gửi được số CCCD. Vui lòng thử lại.');
      setStep('enter');
    }
  }

  return (
    <section aria-labelledby="identity-heading">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl" style={{ background: 'var(--signal-wash)', color: 'var(--signal)' }}>
            <Fingerprint size={23} aria-hidden="true" />
          </span>
          <div>
            <h2 id="identity-heading" className="acct-h2" style={{ margin: 0 }}>Xác minh danh tính qua VNeID</h2>
            <p className="acct-faint" style={{ margin: 0 }}>Bản giả lập, không kết nối VNeID thật</p>
          </div>
        </div>
        {label && <span className={`acct-state ${label.tone}`}>{label.text}</span>}
      </div>

      {loadError && <p role="alert" className="acct-note error mt-4"><AlertCircle size={18} />{loadError}</p>}

      {status === 'Verified' && (
        <p className="acct-note success mt-4">
          <BadgeCheck size={18} />
          Tài khoản đã được xác minh{kyc?.maskedNumber ? <> bằng CCCD <span className="acct-num">{kyc.maskedNumber}</span></> : null}.
        </p>
      )}

      {status === 'Pending' && (
        <p className="acct-note warn mt-4">
          <Clock size={18} />
          Đã liên kết CCCD {kyc?.maskedNumber ? <span className="acct-num">{kyc.maskedNumber}</span> : null}, đang chờ ParkMaster duyệt.
        </p>
      )}

      {canSubmit && (
        <>
          {status === 'Rejected' && kyc?.rejectionReason && (
            <p className="acct-note error mt-4"><AlertCircle size={18} />Lần trước chưa được duyệt: {kyc.rejectionReason}</p>
          )}
          <div className="mt-4 flex flex-wrap items-center justify-between gap-4 rounded-xl p-4" style={{ border: '1px solid var(--line)', background: 'var(--paper)' }}>
            <p className="acct-muted" style={{ margin: 0, maxWidth: '46ch' }}>Liên kết VNeID để xác minh tài khoản. Bạn chỉ cần nhập số căn cước công dân.</p>
            <button type="button" className="acct-btn" onClick={open}>
              <Fingerprint size={16} />Liên kết VNeID
            </button>
          </div>
        </>
      )}

      <dialog
        ref={dialog}
        className="vneid-sim"
        aria-labelledby="vneid-sim-title"
        onCancel={(e) => { if (step === 'connecting') e.preventDefault(); }}
      >
        <header className="vneid-sim-head">
          <span className="vneid-sim-mark" aria-hidden="true"><ShieldCheck size={20} /></span>
          <div>
            <strong id="vneid-sim-title">VNeID</strong>
            <small>Định danh điện tử · bản giả lập</small>
          </div>
          <button type="button" className="vneid-sim-close" onClick={close} aria-label="Đóng" disabled={step === 'connecting'}>
            <X size={18} />
          </button>
        </header>

        {step === 'enter' && (
          <form onSubmit={submit} noValidate className="vneid-sim-body">
            <h3>ParkMaster yêu cầu xác minh danh tính</h3>
            <p>Nhập số định danh cá nhân (số CCCD) để chia sẻ với ParkMaster.</p>
            <label className="vneid-sim-field">
              Số định danh cá nhân
              <input
                autoFocus
                inputMode="numeric"
                autoComplete="off"
                placeholder="0792 0100 8921"
                value={number}
                onChange={(e) => setNumber(formatNumber(e.target.value))}
                onBlur={() => setTouched(true)}
                aria-invalid={touched && invalid}
                aria-describedby={touched && invalid ? 'vneid-sim-error' : undefined}
              />
            </label>
            {touched && invalid && <p id="vneid-sim-error" className="vneid-sim-error">Số CCCD gồm đúng 12 chữ số (đang có {digits.length}).</p>}
            {error && <p role="alert" className="vneid-sim-error">{error}</p>}
            <p className="vneid-sim-note">Thông tin được chia sẻ: số CCCD. ParkMaster mã hoá khi lưu và chỉ hiện 4 số cuối.</p>
            <div className="vneid-sim-actions">
              <button type="button" className="acct-btn ghost" onClick={close}>Huỷ</button>
              <button type="submit" className="acct-btn"><Check size={16} />Đồng ý chia sẻ</button>
            </div>
          </form>
        )}

        {step === 'connecting' && (
          <div className="vneid-sim-body vneid-sim-center" role="status">
            <Loader2 size={36} className="vneid-sim-spin" aria-hidden="true" />
            <h3>Đang liên kết với ParkMaster…</h3>
            <p>Vui lòng không đóng cửa sổ này.</p>
          </div>
        )}

        {step === 'done' && (
          <div className="vneid-sim-body vneid-sim-center">
            <span className="vneid-sim-ok" aria-hidden="true"><Check size={30} /></span>
            <h3>Liên kết thành công</h3>
            <p>
              Đã chia sẻ CCCD {kyc?.maskedNumber ? <span className="acct-num">{kyc.maskedNumber}</span> : null} với ParkMaster.
              Hồ sơ đang chờ duyệt.
            </p>
            <div className="vneid-sim-actions" style={{ justifyContent: 'center' }}>
              <button type="button" className="acct-btn" onClick={() => dialog.current?.close()}>Quay lại ParkMaster</button>
            </div>
          </div>
        )}
      </dialog>
    </section>
  );
}
