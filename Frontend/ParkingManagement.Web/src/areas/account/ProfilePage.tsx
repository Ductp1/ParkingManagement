import { useEffect, useRef, useState, type FormEvent, type ChangeEvent } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Check, Database, Save, ShieldCheck, Upload, Trash2 } from 'lucide-react';
import { avatarSource, currentUser, updateProfile, uploadAvatar, deleteAvatar } from '@/api/authApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser } from '@/store/slices/authSlice';
import type { AuthUser } from '@/types/auth';
import { IdentityCard } from './IdentityCard';
import { kycLabels, roleLabels } from './accountFormat';

const accountStatusLabels: Record<string, string> = {
  Active: 'Đang hoạt động', PendingVerification: 'Chờ xác minh', Locked: 'Đã khóa', Banned: 'Đã cấm',
};

export function ProfilePage() {
  const dispatch = useAppDispatch();
  const user = useAppSelector(s => s.auth.user);
  const [profile, setProfile] = useState<AuthUser | null>(null);
  const [fullName, setFullName] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState('');
  const [removeAvatar, setRemoveAvatar] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reload, setReload] = useState(0);
  const [brokenImage, setBrokenImage] = useState(false);
  useEffect(() => {
    let active = true;
    currentUser().then(data => {
      if (!active) return;
      setProfile(data); setFullName(data.fullName);
    }).catch(err => { if (active) setError(err instanceof Error ? err.message : 'Không thể tải hồ sơ.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reload]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  const changed = profile && (fullName.trim() !== profile.fullName || file !== null || removeAvatar);
  const displayedAvatar = preview || (!removeAvatar ? avatarSource(profile?.avatarUrl) : undefined);
  function resetImage() {
    setFile(null); setPreview(''); setRemoveAvatar(false); setBrokenImage(false);
    if (fileInput.current) fileInput.current.value = '';
  }
  function chooseImage(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0];
    if (!selected) return;
    setError(''); setMessage('');
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(selected.type) || selected.size === 0 || selected.size > 2 * 1024 * 1024) {
      setError('Chọn ảnh JPG, PNG hoặc WebP, dung lượng tối đa 2 MB.'); event.target.value = ''; return;
    }
    setFile(selected); setPreview(URL.createObjectURL(selected)); setRemoveAvatar(false); setBrokenImage(false);
  }
  function syncProfile(updated: AuthUser) {
    setProfile(updated);
    if (user) dispatch(setUser({ ...user, fullName: updated.fullName, avatarUrl: updated.avatarUrl, kycStatus: updated.kycStatus }));
  }
  async function save(event: FormEvent) {
    event.preventDefault(); if (busy || !profile) return;
    setError(''); setMessage('');
    if (!fullName.trim()) { setError('Nhập họ và tên.'); return; }
    setBusy(true);
    let nameSaved = false;
    try {
      let updated = { ...profile };
      if (fullName.trim() !== profile.fullName) {
        await updateProfile(fullName.trim());
        updated = { ...updated, fullName: fullName.trim() }; syncProfile(updated); nameSaved = true;
      }
      if (file) updated = { ...updated, avatarUrl: await uploadAvatar(file) };
      else if (removeAvatar) { await deleteAvatar(); updated = { ...updated, avatarUrl: null }; }
      syncProfile(updated); setFullName(updated.fullName); resetImage();
      setMessage('Đã lưu hồ sơ.');
    } catch (err) { setError(`${nameSaved ? 'Tên đã được lưu, nhưng ảnh chưa cập nhật. ' : ''}${err instanceof Error ? err.message : 'Không thể lưu hồ sơ.'}`); }
    finally { setBusy(false); }
  }

  const kyc = kycLabels[profile?.kycStatus ?? ''];
  return <div className="account-page">
    <header className="acct-head">
      <h1 className="acct-title">Hồ sơ cá nhân</h1>
      <p className="acct-lede">Tên và ảnh đại diện hiển thị với bãi đỗ khi bạn đặt chỗ và với nhân viên trực cổng khi nhận xe.</p>
    </header>
    {error && <div role="alert" className="acct-note error"><AlertCircle size={18} /><span>{error} {!profile && <button onClick={() => { setLoading(true); setError(''); setReload(n => n + 1); }}>Thử lại</button>}</span></div>}
    {message && <div role="status" className="acct-note success"><Check size={18} /><span>{message}</span></div>}
    {loading ? <p className="acct-muted" role="status">Đang tải hồ sơ…</p> : profile && <>
      <form className="acct-form" onSubmit={save}>
        <div className="flex flex-wrap items-center gap-5">
          {displayedAvatar && !brokenImage
            ? <img className="account-avatar" src={displayedAvatar} alt={`Ảnh đại diện ${profile.fullName}`} referrerPolicy="no-referrer" onError={() => setBrokenImage(true)} />
            : <div className="account-avatar" aria-hidden="true">{profile.fullName.trim().charAt(0).toUpperCase()}</div>}
          <div className="grid gap-2">
            <input ref={fileInput} id="profile-avatar-file" className="sr-only" type="file" accept="image/jpeg,image/png,image/webp" aria-label="Chọn ảnh đại diện từ máy" onChange={chooseImage} disabled={busy} />
            <div className="acct-actions">
              <button type="button" className="acct-btn ghost" disabled={busy} onClick={() => fileInput.current?.click()}><Upload size={16} />{file || profile.avatarUrl ? 'Đổi ảnh' : 'Chọn ảnh'}</button>
              {(file || (profile.avatarUrl && !removeAvatar)) && <button type="button" className="acct-btn quiet" disabled={busy} onClick={() => { resetImage(); setRemoveAvatar(!!profile.avatarUrl); setMessage(''); }}><Trash2 size={15} />Xóa ảnh</button>}
            </div>
            <span className="acct-faint break-all">{file ? `${file.name}, ${Math.ceil(file.size / 1024)} KB, chưa lưu` : removeAvatar ? 'Ảnh sẽ bị xóa khi bạn lưu.' : 'JPG, PNG hoặc WebP, tối đa 2 MB.'}</span>
          </div>
        </div>
        <label className="acct-field">Họ và tên<input required maxLength={150} autoComplete="name" value={fullName} onChange={e => { setFullName(e.target.value); setMessage(''); }} disabled={busy} /></label>
        <div className="acct-two">
          <label className="acct-field">Email<input value={profile.email ?? ''} placeholder="Chưa có email" disabled /></label>
          <label className="acct-field">Số điện thoại<input value={profile.phone ?? ''} placeholder="Chưa có số điện thoại" disabled /></label>
        </div>
        <p className="acct-faint" style={{ marginTop: -8 }}>Email và số điện thoại đã xác minh nên không sửa trực tiếp. Cần thay đổi, gửi <Link className="underline" to="/account/privacy">yêu cầu sửa thông tin</Link>.</p>
        <div className="acct-actions end">
          <button type="button" className="acct-btn ghost" disabled={busy || !changed} onClick={() => { setFullName(profile.fullName); resetImage(); setMessage(''); setError(''); }}>Hoàn tác</button>
          <button className="acct-btn" disabled={busy || !changed}><Save size={16} />{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button>
        </div>
      </form>

      <section className="acct-section mt-8">
        <h2 className="acct-h2">Tài khoản</h2>
        <dl className="inventory mt-2">
          <div className="inventory-row"><dt>Mã tài khoản</dt><dd>#{profile.id}</dd><span /></div>
          <div className="inventory-row"><dt>Vai trò</dt><dd>{profile.roles?.map(r => roleLabels[r] ?? r).join(', ')}</dd><span /></div>
          <div className="inventory-row"><dt>Trạng thái</dt><dd>{accountStatusLabels[profile.status ?? ''] ?? profile.status ?? 'Chưa có thông tin'}</dd><span /></div>
          <div className="inventory-row"><dt>Xác minh danh tính</dt><dd>{kyc ? <span className={`acct-state ${kyc.tone}`}>{kyc.text}</span> : profile.kycStatus}</dd>
            {(profile.kycStatus === 'NotSubmitted' || profile.kycStatus === 'Rejected') ? <a className="acct-btn quiet" href="#identity" onClick={e => { e.preventDefault(); document.getElementById('identity')?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }}>Xác minh</a> : <span />}</div>
        </dl>
      </section>

      <div className="acct-section" id="identity"><IdentityCard key={profile.id} /></div>

      <section className="acct-section">
        <h2 className="acct-h2">Mật khẩu</h2>
        <p className="acct-muted mb-4">Đặt lại mật khẩu bằng mã gửi đến email hoặc số điện thoại đã xác minh. Mọi phiên đăng nhập khác sẽ bị đăng xuất.</p>
        <Link className="acct-btn ghost" to="/forgot-password"><ShieldCheck size={16} />Đặt lại mật khẩu</Link>
      </section>

      <section className="acct-section">
        <h2 className="acct-h2">Dữ liệu của tôi</h2>
        <p className="acct-muted mb-4">Xem toàn bộ thông tin ParkMaster đang lưu, tải bản sao hoặc yêu cầu sửa, xoá dữ liệu theo Nghị định 13/2023/NĐ-CP.</p>
        <Link className="acct-btn ghost" to="/account/privacy"><Database size={16} />Quản lý dữ liệu của tôi</Link>
      </section>

      {profile.roles?.includes('Owner') && <section className="acct-section">
        <h2 className="acct-h2">Nhân viên trực cổng</h2>
        <p className="acct-muted mb-4">Thêm, phân công và gỡ quyền nhân viên cho các bãi của bạn.</p>
        <Link className="acct-btn ghost" to="/owner/staff">Quản lý nhân viên</Link>
      </section>}
    </>}
  </div>;
}
