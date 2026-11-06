import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { AlertCircle, CheckCircle2, Clock3, ImagePlus, Lock } from 'lucide-react';
import { getMyKyc, getMyKycImage, submitKyc } from '@/api/accountApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser } from '@/store/slices/authSlice';
import type { KycDocumentType, KycImageSide, KycStatusView } from '@/types/account';
import { DocumentCard } from './DocumentCard';
import { dateTime, documentLabels } from './accountFormat';

const MAX_BYTES = 5 * 1024 * 1024;
const TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export function KycPage() {
  const dispatch = useAppDispatch();
  const user = useAppSelector(s => s.auth.user);
  const [status, setStatus] = useState<KycStatusView | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const [settle, setSettle] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getMyKyc().then(data => { if (active) { setStatus(data); setError(''); } })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : 'Không tải được trạng thái định danh.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reload]);

  function submitted(next: KycStatusView) {
    setStatus(next);
    setSettle(true);
    if (user) dispatch(setUser({ ...user, kycStatus: next.status }));
    window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
  }

  const holder = (user?.fullName ?? '').toUpperCase();
  const canSubmit = status && (status.status === 'NotSubmitted' || status.status === 'Rejected');

  return <>
    <header className="acct-head">
      <h1 className="acct-title">Xác minh danh tính</h1>
      <p className="acct-lede">Xác minh một lần bằng căn cước công dân hoặc giấy phép lái xe để đặt chỗ tại bãi yêu cầu định danh và nhận lại xe nhanh hơn.</p>
    </header>
    {error && <div role="alert" className="acct-note error"><AlertCircle size={18} /><span>{error} <button onClick={() => setReload(n => n + 1)}>Thử lại</button></span></div>}
    {loading && !status && <p className="acct-muted" role="status">Đang tải trạng thái định danh…</p>}
    {status && (canSubmit
      ? <KycForm status={status} holder={holder} onSubmitted={submitted} />
      : <KycResult status={status} holder={holder} settle={settle} />)}
  </>;
}

function KycResult({ status, holder, settle }: { status: KycStatusView; holder: string; settle: boolean }) {
  const pending = status.status === 'Pending';
  return <div className="kyc-layout">
    <div>
      {pending
        ? <div className="acct-note warn" role="status"><Clock3 size={18} /><span>Hồ sơ {status.submittedAtUtc ? `gửi lúc ${dateTime(status.submittedAtUtc)} ` : ''}đang chờ quản trị viên duyệt, thường trong vòng 24 giờ. Bạn vẫn đặt chỗ được ở các bãi không yêu cầu định danh.</span></div>
        : <div className="acct-note success" role="status"><CheckCircle2 size={18} /><span>Danh tính đã được xác minh{status.verifiedAtUtc ? ` ngày ${dateTime(status.verifiedAtUtc)}` : ''}.</span></div>}
      <section className="acct-section">
        <h2 className="acct-h2">Giấy tờ đã gửi</h2>
        <dl className="inventory">
          <div className="inventory-row"><dt>Loại giấy tờ</dt><dd>{documentLabels[status.documentType ?? ''] ?? 'Không rõ'}</dd><span /></div>
          <div className="inventory-row"><dt>Số giấy tờ</dt><dd className="acct-num" style={{ fontSize: 17 }}>{status.maskedNumber ?? 'Đã xóa sau khi duyệt'}</dd><span /></div>
        </dl>
        {(status.hasFrontImage || status.hasBackImage) && <div className="kyc-shots mt-5">
          {status.hasFrontImage && <StoredImage side="front" label="Mặt trước" />}
          {status.hasBackImage && <StoredImage side="back" label="Mặt sau" />}
        </div>}
        <p className="acct-faint mt-4"><Lock size={13} className="inline mr-1 -mt-0.5" />Ảnh và số giấy tờ được mã hóa khi lưu. Chỉ bạn và quản trị viên duyệt hồ sơ xem được.</p>
      </section>
    </div>
    <aside className="kyc-aside">
      <DocumentCard type={status.documentType} number={status.maskedNumber} holder={holder} status={status.status} settle={settle} />
    </aside>
  </div>;
}

/** Ảnh giấy tờ chỉ tải qua phiên đăng nhập; object URL được thu hồi khi rời trang. */
function StoredImage({ side, label }: { side: KycImageSide; label: string }) {
  const [url, setUrl] = useState('');
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    let created = '';
    getMyKycImage(side, controller.signal)
      .then(blob => { created = URL.createObjectURL(blob); setUrl(created); })
      .catch(() => { if (!controller.signal.aborted) setFailed(true); });
    return () => { controller.abort(); if (created) URL.revokeObjectURL(created); };
  }, [side]);
  return <div className="kyc-shot filled" style={{ cursor: 'default' }}>
    {url ? <img src={url} alt={`${label} giấy tờ đã gửi`} /> : <span>{failed ? 'Không tải được ảnh' : 'Đang tải ảnh…'}</span>}
    <span className="kyc-shot-label">{label}</span>
  </div>;
}

interface Shot { file: File; preview: string }

function KycForm({ status, holder, onSubmitted }: { status: KycStatusView; holder: string; onSubmitted: (s: KycStatusView) => void }) {
  const [type, setType] = useState<KycDocumentType | null>(null);
  const [number, setNumber] = useState('');
  const [front, setFront] = useState<Shot | null>(null);
  const [back, setBack] = useState<Shot | null>(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const shots = useRef<Shot[]>([]);
  useEffect(() => { shots.current = [front, back].filter((s): s is Shot => !!s); }, [front, back]);
  useEffect(() => () => shots.current.forEach(s => URL.revokeObjectURL(s.preview)), []);

  const digits = number.replace(/\D/g, '').slice(0, 12);
  const numberOk = digits.length === 12;
  const ready = !!type && numberOk && !!front && !!back && consent;

  function pick(side: 'front' | 'back', event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setError('');
    if (!TYPES.includes(file.type) || file.size === 0 || file.size > MAX_BYTES) {
      setError('Chọn ảnh JPG, PNG hoặc WebP, tối đa 5 MB. Chụp đủ 4 góc giấy tờ, không lóa sáng.');
      return;
    }
    const shot = { file, preview: URL.createObjectURL(file) };
    const setter = side === 'front' ? setFront : setBack;
    setter(prev => { if (prev) URL.revokeObjectURL(prev.preview); return shot; });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!ready || busy || !type || !front || !back) return;
    setBusy(true); setError('');
    try { onSubmitted(await submitKyc(type, digits, front.file, back.file)); }
    catch (err) { setError(err instanceof Error ? err.message : 'Không gửi được hồ sơ định danh.'); }
    finally { setBusy(false); }
  }

  return <div className="kyc-layout">
    <form className="acct-form" onSubmit={submit} noValidate>
      {status.status === 'Rejected' && <div className="acct-note error" role="alert"><AlertCircle size={18} /><span>Hồ sơ trước chưa được duyệt{status.rejectionReason ? `: ${status.rejectionReason}` : '.'} Ảnh và số giấy tờ cũ đã được xóa, hãy gửi lại.</span></div>}
      {error && <div className="acct-note error" role="alert"><AlertCircle size={18} /><span>{error}</span></div>}
      <ol className="kyc-steps">
        <li className={type ? 'done' : ''}>
          <fieldset className="acct-field" style={{ border: 0, padding: 0, margin: 0 }}>
            <legend className="acct-h2" style={{ marginBottom: 10 }}>Chọn giấy tờ</legend>
            <div className="acct-choice">
              <label><input type="radio" name="docType" checked={type === 'CitizenId'} onChange={() => setType('CitizenId')} disabled={busy} />
                <span><strong>Căn cước công dân</strong><span className="acct-faint">Thẻ gắn chip, 12 số</span></span></label>
              <label><input type="radio" name="docType" checked={type === 'DriverLicense'} onChange={() => setType('DriverLicense')} disabled={busy} />
                <span><strong>Giấy phép lái xe</strong><span className="acct-faint">Thẻ PET mẫu mới, 12 số</span></span></label>
            </div>
          </fieldset>
        </li>
        <li className={numberOk ? 'done' : ''}>
          <label className="acct-field"><span className="acct-h2">Nhập số giấy tờ</span>
            <input inputMode="numeric" autoComplete="off" placeholder="12 chữ số in trên thẻ" value={digits.replace(/(\d{4})(?=\d)/g, '$1 ')}
              onChange={e => setNumber(e.target.value)} disabled={busy} aria-invalid={digits.length > 0 && !numberOk}
              className="acct-num" style={{ fontSize: 19, letterSpacing: '.06em' }} />
            <small>{digits.length > 0 && !numberOk ? `Còn thiếu ${12 - digits.length} chữ số.` : 'Số được mã hóa ngay khi lưu; sau đó chỉ hiển thị 4 số cuối.'}</small>
          </label>
        </li>
        <li className={front && back ? 'done' : ''}>
          <div className="kyc-drop">
            <span className="acct-h2">Chụp hai mặt giấy tờ</span>
            <p className="acct-muted">Đặt thẻ trên nền tối, chụp thẳng, đủ bốn góc và đọc rõ chữ. Ảnh tối đa 5 MB.</p>
            <div className="kyc-shots">
              <ShotPicker label="Mặt trước" shot={front} onPick={e => pick('front', e)} disabled={busy} />
              <ShotPicker label="Mặt sau" shot={back} onPick={e => pick('back', e)} disabled={busy} />
            </div>
          </div>
        </li>
        <li className={consent ? 'done' : ''}>
          <div className="acct-form" style={{ gap: 14 }}>
            <span className="acct-h2">Xác nhận và gửi</span>
            <label className="kyc-consent"><input type="checkbox" checked={consent} onChange={e => setConsent(e.target.checked)} disabled={busy} />
              <span>Tôi đồng ý cho ParkMaster xử lý ảnh và số giấy tờ để xác minh danh tính theo Nghị định 13/2023/NĐ-CP. Tôi có thể xem, tải bản sao hoặc yêu cầu xóa trong mục Dữ liệu của tôi.</span></label>
            <div className="acct-actions"><button className="acct-btn" disabled={!ready || busy}>{busy ? 'Đang gửi hồ sơ…' : 'Gửi hồ sơ xác minh'}</button></div>
          </div>
        </li>
      </ol>
    </form>
    <aside className="kyc-aside" aria-label="Xem trước giấy tờ">
      <DocumentCard type={type} number={digits} holder={holder} />
      <p className="acct-faint">Kiểm tra lại số trên thẻ trước khi gửi. Hồ sơ đang chờ duyệt thì không sửa được.</p>
    </aside>
  </div>;
}

function ShotPicker({ label, shot, onPick, disabled }: { label: string; shot: Shot | null; onPick: (e: ChangeEvent<HTMLInputElement>) => void; disabled: boolean }) {
  return <label className={`kyc-shot${shot ? ' filled' : ''}`}>
    <input type="file" className="sr-only" accept={TYPES.join(',')} onChange={onPick} disabled={disabled} aria-label={`Chọn ảnh ${label.toLowerCase()}`} />
    {shot ? <img src={shot.preview} alt={`${label} đã chọn`} /> : <span className="grid justify-items-center gap-1.5"><ImagePlus size={22} />Chọn ảnh {label.toLowerCase()}</span>}
    <span className="kyc-shot-label">{shot ? `${label}, bấm để đổi` : label}</span>
  </label>;
}
