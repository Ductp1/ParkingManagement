import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Download, Eye, PencilLine, Trash2, X } from 'lucide-react';
import { downloadExport, getPersonalData, listDataRequests, submitDataRequest } from '@/api/accountApi';
import type { DataRequestItem, PersonalDataView } from '@/types/account';
import {
  date, dateTime, documentLabels, kycLabels, requestLabels, requestStatus, roleLabels, securityLabels,
} from './accountFormat';

type Mode = 'export' | 'correction' | 'deletion';

export function PrivacyPage() {
  const [data, setData] = useState<PersonalDataView | null>(null);
  const [requests, setRequests] = useState<DataRequestItem[]>([]);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [revision, setRevision] = useState(0);
  const [mode, setMode] = useState<Mode | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([getPersonalData(controller.signal), listDataRequests(1, controller.signal)])
      .then(([view, list]) => { setData(view); setRequests(list); setError(''); })
      .catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Không tải được dữ liệu cá nhân.'); });
    return () => controller.abort();
  }, [revision]);

  const pendingDeletion = requests.find(r => r.type === 'Deletion' && (r.status === 'Submitted' || r.status === 'InProgress'));
  const done = (text: string) => { setMessage(text); setMode(null); setRevision(n => n + 1); };

  return <>
    <header className="acct-head">
      <h1 className="acct-title">Dữ liệu của tôi</h1>
      <p className="acct-lede">Đây là toàn bộ thông tin ParkMaster lưu trong tài khoản của bạn. Bạn có quyền xem, sửa, tải bản sao hoặc yêu cầu xóa theo Nghị định 13/2023/NĐ-CP.</p>
    </header>
    {message && <div role="status" className="acct-note success"><CheckCircle2 size={18} /><span>{message}</span></div>}
    {error && <div role="alert" className="acct-note error"><AlertCircle size={18} /><span>{error} <button onClick={() => setRevision(n => n + 1)}>Thử lại</button></span></div>}

    <div className="rights">
      <div><h3><Eye size={18} aria-hidden="true" />Xem</h3><p className="acct-muted">Mọi trường đang lưu được liệt kê ngay bên dưới, số giấy tờ chỉ hiện 4 số cuối.</p></div>
      <div><h3><PencilLine size={18} aria-hidden="true" />Sửa</h3><p className="acct-muted">Tên và ảnh sửa trực tiếp trong hồ sơ. Thông tin đã xác minh cần gửi yêu cầu.</p>
        <button className="acct-btn ghost justify-self-start" onClick={() => setMode('correction')}>Yêu cầu sửa thông tin</button></div>
      <div><h3><Download size={18} aria-hidden="true" />Tải bản sao</h3><p className="acct-muted">Một tệp JSON gồm hồ sơ, trạng thái định danh và nhật ký bảo mật gần đây.</p>
        <button className="acct-btn ghost justify-self-start" onClick={() => setMode('export')}>Tải bản sao dữ liệu</button></div>
    </div>

    {data && <section className="acct-section mt-8" aria-labelledby="inv">
      <h2 className="acct-h2" id="inv">Thông tin đang lưu</h2>
      <p className="acct-muted mb-5">Lịch sử đặt chỗ và thanh toán do hệ thống đặt chỗ lưu riêng; xem trong <Link className="text-[var(--signal)] underline" to="/account/history">Lịch sử gửi xe</Link>.</p>
      <dl className="inventory">
        <div className="inventory-group"><h3>Liên hệ</h3>
          <Row label="Họ và tên" value={data.fullName} edit="/account/profile" />
          <Row label="Email" value={data.email ? `${data.email}${data.emailConfirmed ? ' (đã xác minh)' : ''}` : 'Chưa có'} request={() => setMode('correction')} />
          <Row label="Số điện thoại" value={data.phoneNumber ? `${data.phoneNumber}${data.phoneConfirmed ? ' (đã xác minh)' : ''}` : 'Chưa có'} request={() => setMode('correction')} />
          <Row label="Ảnh đại diện" value={data.avatarUrl ? 'Đã tải lên' : 'Chưa có'} edit="/account/profile" />
        </div>
        <div className="inventory-group"><h3>Định danh</h3>
          <Row label="Trạng thái" value={kycLabels[data.kycStatus]?.text ?? data.kycStatus} edit={data.kycStatus === 'NotSubmitted' || data.kycStatus === 'Rejected' ? '/account/kyc' : undefined} editLabel="Xác minh" />
          {data.kycDocumentType && <Row label="Loại giấy tờ" value={documentLabels[data.kycDocumentType] ?? data.kycDocumentType} />}
          {data.kycMaskedNumber && <Row label="Số giấy tờ" value={<span className="acct-num" style={{ fontSize: 16 }}>{data.kycMaskedNumber}</span>} />}
        </div>
        <div className="inventory-group"><h3>Tài khoản</h3>
          <Row label="Mã tài khoản" value={`#${data.id}`} />
          <Row label="Vai trò" value={data.roles.map(r => roleLabels[r] ?? r).join(', ')} />
          {data.businessName && <Row label="Tên doanh nghiệp" value={data.businessName} request={() => setMode('correction')} />}
          <Row label="Ngày tạo" value={date(data.createdAtUtc)} />
          <Row label="Đăng nhập gần nhất" value={data.lastLoginAtUtc ? dateTime(data.lastLoginAtUtc) : 'Chưa ghi nhận'} />
          <Row label="Phiên đang mở" value={`${data.activeSessions} thiết bị`} />
        </div>
        {data.recentSecurityEvents.length > 0 && <div className="inventory-group"><h3>Hoạt động bảo mật gần đây</h3>
          {data.recentSecurityEvents.map((e, i) => <Row key={i} label={dateTime(e.occurredAtUtc)} value={`${securityLabels[e.type] ?? e.type}${e.ipAddress ? `, IP ${e.ipAddress}` : ''}`} />)}
        </div>}
      </dl>
    </section>}

    {requests.length > 0 && <section className="acct-section" aria-labelledby="req">
      <h2 className="acct-h2" id="req">Yêu cầu đã gửi</h2>
      <ul className="request-list mt-3">
        {requests.map(r => { const st = requestStatus[r.status] ?? { text: r.status, tone: 'idle' as const };
          return <li key={r.id}>
            <span><strong className="font-semibold">{requestLabels[r.type] ?? r.type}</strong> <span className="acct-faint">gửi {dateTime(r.createdAtUtc)}{r.status === 'Submitted' ? `, hạn xử lý ${dateTime(r.dueAtUtc)}` : ''}</span></span>
            <span className={`acct-state ${st.tone}`}>{st.text}</span>
            {r.reason && <p className="acct-faint">Nội dung: {r.reason}</p>}
            {r.resultNote && <p className="acct-faint">Phản hồi: {r.resultNote}</p>}
          </li>; })}
      </ul>
    </section>}

    <section className="acct-section" aria-labelledby="del">
      <div className="danger-zone">
        <div style={{ maxWidth: '58ch' }}>
          <h2 className="acct-h2" id="del">Xóa tài khoản và dữ liệu cá nhân</h2>
          <p className="acct-muted">{pendingDeletion
            ? `Yêu cầu xóa đã gửi ngày ${date(pendingDeletion.createdAtUtc)} và đang chờ quản trị viên xử lý trước ${dateTime(pendingDeletion.dueAtUtc)}.`
            : 'Tên, liên hệ, ảnh và giấy tờ sẽ bị ẩn danh hóa, bạn bị đăng xuất trên mọi thiết bị. Hóa đơn và giao dịch vẫn được giữ theo quy định kế toán.'}</p>
        </div>
        {!pendingDeletion && <button className="acct-btn danger" onClick={() => setMode('deletion')}><Trash2 size={16} />Yêu cầu xóa tài khoản</button>}
      </div>
    </section>

    {mode && <RequestDialog mode={mode} onClose={() => setMode(null)} onDone={done} />}
  </>;
}

function Row({ label, value, edit, editLabel = 'Sửa', request }: { label: string; value: ReactNode; edit?: string; editLabel?: string; request?: () => void }) {
  return <div className="inventory-row">
    <dt>{label}</dt><dd>{value}</dd>
    {edit ? <Link className="acct-btn quiet" to={edit}>{editLabel}</Link>
      : request ? <button type="button" className="acct-btn quiet" onClick={request}>Yêu cầu sửa</button> : <span />}
  </div>;
}

const copy: Record<Mode, { title: string; body: string; action: string; busy: string }> = {
  export: { title: 'Tải bản sao dữ liệu', body: 'Nhập mật khẩu để xác nhận đúng là bạn. Tệp được tải về máy ngay, không gửi qua email.', action: 'Tải bản sao', busy: 'Đang tạo bản sao…' },
  correction: { title: 'Yêu cầu sửa thông tin', body: 'Mô tả thông tin đang sai và giá trị đúng. Quản trị viên xử lý trong 72 giờ và phản hồi tại trang này.', action: 'Gửi yêu cầu sửa', busy: 'Đang gửi…' },
  deletion: { title: 'Xóa tài khoản?', body: 'Sau khi quản trị viên duyệt, bạn không thể đăng nhập hay khôi phục tài khoản này. Bạn có thể đăng ký lại bằng email hoặc số điện thoại cũ như tài khoản mới.', action: 'Gửi yêu cầu xóa', busy: 'Đang gửi…' },
};

function RequestDialog({ mode, onClose, onDone }: { mode: Mode; onClose: () => void; onDone: (message: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [ack, setAck] = useState(false);
  useEffect(() => { dialog.current?.showModal(); }, []);
  const text = copy[mode];

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const form = new FormData(event.currentTarget);
    const password = String(form.get('password') ?? '');
    const reason = String(form.get('reason') ?? '').trim();
    setBusy(true); setError('');
    try {
      if (mode === 'export') {
        const request = await submitDataRequest('Export', password);
        await downloadExport(request.id, password);
        onDone('Đã tải bản sao dữ liệu về máy.');
      } else if (mode === 'correction') {
        await submitDataRequest('Correction', password, reason);
        onDone('Đã gửi yêu cầu sửa thông tin. Phản hồi sẽ hiện trong mục Yêu cầu đã gửi.');
      } else {
        await submitDataRequest('Deletion', password);
        onDone('Đã gửi yêu cầu xóa tài khoản. Quản trị viên sẽ xử lý trong 72 giờ.');
      }
    } catch (err) {
      const status = err instanceof Error && 'status' in err ? (err as { status?: number }).status : undefined;
      setError(status === 403 ? 'Mật khẩu không đúng. Sai 5 lần liên tiếp tài khoản sẽ bị khóa 15 phút.' : err instanceof Error ? err.message : 'Không gửi được yêu cầu.');
    } finally { setBusy(false); }
  }

  return <dialog ref={dialog} className="acct-dialog" aria-labelledby="req-title" onClose={onClose} onCancel={e => { if (busy) e.preventDefault(); }}>
    <form className="acct-form" onSubmit={submit}>
      <div className="flex justify-between items-start gap-4">
        <h2 id="req-title">{text.title}</h2>
        <button type="button" className="acct-btn ghost" style={{ padding: 8 }} aria-label="Đóng" onClick={() => dialog.current?.close()} disabled={busy}><X size={16} /></button>
      </div>
      <p className="acct-muted" style={{ marginTop: -10 }}>{text.body}</p>
      {error && <div className="acct-note error" role="alert" style={{ margin: 0 }}><AlertCircle size={18} /><span>{error}</span></div>}
      {mode === 'correction' && <label className="acct-field">Nội dung cần sửa
        <textarea name="reason" required maxLength={1000} placeholder="Ví dụ: Số điện thoại cũ 0901 234 567 không còn dùng, số mới là 0912 345 678." disabled={busy} /></label>}
      <label className="acct-field">Mật khẩu hiện tại
        <input type="password" name="password" autoComplete="current-password" maxLength={72} required disabled={busy} autoFocus={mode !== 'correction'} /></label>
      {mode === 'deletion' && <label className="kyc-consent"><input type="checkbox" checked={ack} onChange={e => setAck(e.target.checked)} disabled={busy} />
        <span>Tôi hiểu tài khoản sẽ bị ẩn danh hóa vĩnh viễn và không thể khôi phục.</span></label>}
      <div className="acct-actions end">
        <button type="button" className="acct-btn ghost" onClick={() => dialog.current?.close()} disabled={busy}>Hủy</button>
        <button className={`acct-btn${mode === 'deletion' ? ' danger' : ''}`} disabled={busy || (mode === 'deletion' && !ack)}>{busy ? text.busy : text.action}</button>
      </div>
    </form>
  </dialog>;
}
