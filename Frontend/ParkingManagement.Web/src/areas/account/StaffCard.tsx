import { useEffect, useRef, useState, type FormEvent } from 'react';
import { UserPlus, X } from 'lucide-react';
import { apiClient } from '@/api/client';

const endpoint = '/api/v1/users/me/staff';
interface StaffItem { id: number; userId: number; fullName: string; parkingLotId: number; status: string }
interface OwnerLot { id: number; name?: string | null }
const statusLabels: Record<string, string> = { PendingVerification: 'Chờ kích hoạt', Active: 'Đang hoạt động', Locked: 'Đã khóa', Banned: 'Đã cấm' };

export function StaffCard() {
  const [items, setItems] = useState<StaffItem[]>([]);
  const [lots, setLots] = useState<OwnerLot[] | null>(null);
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [formError, setFormError] = useState('');
  const dialog = useRef<HTMLDialogElement>(null);

  function openDialog() {
    setFormError('');
    dialog.current?.showModal();
  }

  useEffect(() => {
    const controller = new AbortController();
    apiClient.get<OwnerLot[]>(endpoint + '/lots', { signal: controller.signal })
      .then(response => setLots(response.data))
      // Không lấy được danh sách bãi → vẫn cho nhập mã bãi; backend luôn kiểm tra quyền sở hữu.
      .catch(() => { if (!controller.signal.aborted) setLots(null); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    apiClient.get<StaffItem[]>(endpoint, { params: { page }, signal: controller.signal })
      .then(response => setItems(response.data))
      .catch(() => { if (!controller.signal.aborted) setError('Không thể tải danh sách nhân viên.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, revision]);

  const lotName = (id: number) => {
    const lot = lots?.find(item => item.id === id);
    return lot?.name ? `${lot.name} (#${id})` : `Bãi #${id}`;
  };

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const form = event.currentTarget;
    const data = new FormData(form);
    setBusy(true);
    setFormError('');
    setMessage('');
    try {
      const { data: created } = await apiClient.post<StaffItem>(endpoint, {
        fullName: data.get('fullName'), contact: data.get('contact'),
        parkingLotId: Number(data.get('parkingLotId')),
      });
      form.reset();
      dialog.current?.close();
      setMessage(`Đã phân công ${created.fullName} tại ${lotName(created.parkingLotId)}. Nhân viên mới sẽ nhận email gồm tài khoản và mật khẩu tạm tại ${String(data.get('contact'))}; nhân viên đã có cần đăng nhập lại để nhận quyền ở bãi mới.`);
      setPage(1);
      setRevision(value => value + 1);
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Không thể tạo nhân viên.');
    } finally {
      setBusy(false);
    }
  }

  async function revoke(item: StaffItem) {
    if (busy || !window.confirm('Gỡ quyền của ' + item.fullName + ' tại ' + lotName(item.parkingLotId) + '?')) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      await apiClient.delete(endpoint + '/' + item.id);
      setMessage('Đã gỡ quyền tại bãi và thu hồi phiên của nhân viên.');
      setRevision(value => value + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể gỡ quyền.');
    } finally {
      setBusy(false);
    }
  }

  const noLots = lots !== null && lots.length === 0;

  return <section aria-labelledby="staff-heading">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 id="staff-heading" className="acct-h2" style={{ margin: 0 }}>Quản lý nhân viên</h2>
        <p className="acct-faint" style={{ margin: '2px 0 0' }}>Mỗi dòng là một nhân viên được phân công tại một bãi.</p>
      </div>
      <button type="button" className="acct-btn" onClick={openDialog} disabled={busy || noLots}>
        <UserPlus size={16} />Thêm nhân viên
      </button>
    </div>
    {noLots && <p className="acct-note warn mt-4">Bạn chưa có bãi xe nào trên hệ thống, hãy đăng ký bãi trước khi thêm nhân viên.</p>}

    {error && <p role="alert" className="acct-note error mt-4">{error}</p>}
    {message && <p role="status" className="acct-note success mt-4">{message}</p>}

    {loading ? <p role="status" className="acct-muted mt-4">Đang tải nhân viên…</p>
      : items.length === 0 ? <p className="acct-muted mt-4">Chưa có nhân viên nào. Bấm “Thêm nhân viên” để phân công người trực cổng.</p>
      : <ul className="staff-list mt-4">
        {items.map(item => <li key={item.id}>
          <span className="staff-avatar" aria-hidden="true">{item.fullName.trim().charAt(0).toUpperCase()}</span>
          <span className="min-w-0 flex-1">
            <strong>{item.fullName}</strong>
            <small>{lotName(item.parkingLotId)}</small>
          </span>
          <span className={`acct-state ${item.status === 'Active' ? 'ok' : item.status === 'PendingVerification' ? 'wait' : 'bad'}`}>{statusLabels[item.status] ?? item.status}</span>
          <button type="button" className="acct-btn quiet" disabled={busy} onClick={() => void revoke(item)}>Gỡ quyền</button>
        </li>)}
      </ul>}

    {(page > 1 || items.length >= 20) && <div className="flex gap-2 mt-4">
      <button type="button" className="acct-btn ghost" disabled={page === 1 || loading || busy} onClick={() => setPage(value => value - 1)}>Trang trước</button>
      <button type="button" className="acct-btn ghost" disabled={items.length < 20 || loading || busy} onClick={() => setPage(value => value + 1)}>Trang sau</button>
    </div>}

    <dialog ref={dialog} className="acct-dialog" aria-labelledby="staff-dialog-title" onCancel={e => { if (busy) e.preventDefault(); }}>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 id="staff-dialog-title">Thêm nhân viên</h2>
          <p className="acct-muted" style={{ margin: 0 }}>Hệ thống gửi tài khoản và mật khẩu tạm tới email nhân viên. Nhập email của nhân viên đã có để gán thêm bãi.</p>
        </div>
        <button type="button" className="acct-btn quiet" aria-label="Đóng" onClick={() => dialog.current?.close()} disabled={busy}><X size={18} /></button>
      </div>
      <form className="acct-form mt-6" onSubmit={create} autoComplete="off">
        <label className="acct-field">Họ và tên<input name="fullName" required maxLength={150} disabled={busy} placeholder="Lê Văn Cổng" /></label>
        <label className="acct-field">Email nhân viên<input name="contact" type="email" required maxLength={256} disabled={busy} placeholder="nhanvien@example.com" />
          <small>Mật khẩu tạm được gửi tới email này; nhân viên nên đổi mật khẩu sau lần đăng nhập đầu.</small></label>
        {lots && lots.length > 0
          ? <label className="acct-field">Bãi xe
              <select name="parkingLotId" required disabled={busy} defaultValue="">
                <option value="" disabled>Chọn bãi xe</option>
                {lots.map(lot => <option key={lot.id} value={lot.id}>{lotName(lot.id)}</option>)}
              </select>
            </label>
          : <label className="acct-field">Mã bãi xe<input name="parkingLotId" type="number" required min={1} max={2147483647} disabled={busy} />
              <small>Không tải được danh sách bãi, hãy nhập mã bãi.</small></label>}
        {formError && <p role="alert" className="acct-note error" style={{ margin: 0 }}>{formError}</p>}
        <div className="acct-actions end">
          <button type="button" className="acct-btn ghost" onClick={() => dialog.current?.close()} disabled={busy}>Huỷ</button>
          <button className="acct-btn" disabled={busy}><UserPlus size={16} />{busy ? 'Đang thêm…' : 'Thêm nhân viên'}</button>
        </div>
      </form>
    </dialog>
  </section>;
}
