import { useEffect, useRef, useState, type FormEvent, type ChangeEvent } from 'react';
import { Link } from 'react-router-dom';
import { Check, Save, ShieldCheck, UserRound, Upload, Trash2 } from 'lucide-react';
import { avatarSource, currentUser, updateProfile, uploadAvatar, deleteAvatar } from '@/api/authApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { setUser } from '@/store/slices/authSlice';
import type { AuthUser } from '@/types/auth';
import { VneidDemoCard } from './VneidDemoCard';
import './account.css';

const roleLabels = { Driver: 'Tài xế', Owner: 'Chủ bãi', Staff: 'Nhân viên', Admin: 'Quản trị viên' };

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
    if (user) dispatch(setUser({ ...user, fullName: updated.fullName, avatarUrl: updated.avatarUrl }));
  }
  async function save(event: FormEvent) {
    event.preventDefault(); if (busy || !profile) return;
    setError(''); setMessage('');
    if (!fullName.trim()) { setError('Vui lòng nhập họ và tên.'); return; }
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
      setMessage('Đã lưu thay đổi hồ sơ của bạn.');
    } catch (err) { setError(`${nameSaved ? 'Tên đã được lưu, nhưng ảnh chưa cập nhật. ' : ''}${err instanceof Error ? err.message : 'Không thể lưu hồ sơ.'}`); }
    finally { setBusy(false); }
  }
  return <div className="account-page">
    <header className="account-header"><div><span className="account-kicker">TÀI KHOẢN CỦA BẠN</span><h1>Hồ sơ cá nhân</h1><p className="account-muted">Một chút thông tin, để mỗi hành trình gần gũi hơn.</p></div><UserRound className="text-indigo-500" size={30} /></header>
    {error && <div role="alert" className="account-notice error">{error} {!profile && <button className="underline ml-2" onClick={() => { setLoading(true); setError(''); setReload(n => n + 1); }}>Thử lại</button>}</div>}
    {message && <div role="status" className="account-notice success"><Check size={16} className="inline mr-2" />{message}</div>}
    {loading ? <div className="account-card account-empty" role="status">Đang tải hồ sơ…</div> : profile && <div className="account-grid">
      <aside className="account-card text-center">
        {displayedAvatar && !brokenImage ? <img className="account-avatar" src={displayedAvatar} alt={`Ảnh đại diện ${profile.fullName}`} referrerPolicy="no-referrer" onError={() => setBrokenImage(true)} /> : <div className="account-avatar">{profile.fullName.trim().charAt(0).toUpperCase()}</div>}
        {(file || removeAvatar) && <p className="account-muted text-xs mb-3">Ảnh xem trước · Chưa lưu</p>}
        <h2 className="break-words">{profile.fullName}</h2><p className="account-muted break-all mt-2">{profile.email || profile.phone}</p>
        <div className="flex flex-wrap justify-center gap-2 mt-4">{profile.roles?.map(role => <span className="account-badge" key={role}>{roleLabels[role]}</span>)}</div>
        <div className="border-t border-slate-100 mt-6 pt-5 account-muted text-left"><ShieldCheck size={18} className="text-indigo-500 mb-2" />Thông tin liên hệ đã xác minh giúp bảo vệ và khôi phục tài khoản của bạn.</div>
      </aside>
      <section className="account-card"><h2>Thông tin cá nhân</h2><p className="account-muted mt-1">Cập nhật cách bạn xuất hiện trên ParkMaster.</p>
        <form className="account-form" onSubmit={save}>
          <label className="account-field">Họ và tên<input required maxLength={150} autoComplete="name" value={fullName} onChange={e => { setFullName(e.target.value); setMessage(''); }} disabled={busy} /></label>
          <div className="account-field"><span>Ảnh đại diện</span>
            <input ref={fileInput} id="profile-avatar-file" className="sr-only" type="file" accept="image/jpeg,image/png,image/webp" aria-label="Chọn ảnh đại diện từ máy" onChange={chooseImage} disabled={busy} />
            <div className="flex flex-wrap items-center gap-3"><button type="button" className="account-button secondary" disabled={busy} onClick={() => fileInput.current?.click()}><Upload size={16} />{file || profile.avatarUrl ? 'Đổi ảnh' : 'Chọn ảnh từ máy'}</button>
              {(file || (profile.avatarUrl && !removeAvatar)) && <button type="button" className="account-button secondary" disabled={busy} onClick={() => { resetImage(); setRemoveAvatar(!!profile.avatarUrl); setMessage(''); }}><Trash2 size={16} />Xóa ảnh</button>}
            </div>
            <span className="account-muted text-xs break-all">{file ? `${file.name} · ${Math.ceil(file.size / 1024)} KB` : removeAvatar ? 'Ảnh sẽ được xóa khi bạn lưu thay đổi.' : 'JPG, PNG hoặc WebP · Tối đa 2 MB, 4096 × 4096 pixel.'}</span>
          </div>
          <div className="account-two-fields"><label className="account-field">Email<input value={profile.email ?? ''} placeholder="Chưa có email" disabled /></label><label className="account-field">Số điện thoại<input value={profile.phone ?? ''} placeholder="Chưa có số điện thoại" disabled /></label></div>
          <p className="account-muted text-xs">Email và số điện thoại cần được xác minh nên không thể thay đổi trực tiếp tại đây.</p>
          <div className="account-actions border-t border-slate-100 pt-5"><button type="button" className="account-button secondary" disabled={busy || !changed} onClick={() => { setFullName(profile.fullName); resetImage(); setMessage(''); setError(''); }}>Hoàn tác</button><button className="account-button" disabled={busy || !changed}><Save size={16} />{busy ? 'Đang lưu…' : 'Lưu thay đổi'}</button></div>
        </form>
        <VneidDemoCard key={profile.id} userId={profile.id} disabled={busy} />
        <div className="mt-8 pt-6 border-t border-slate-100"><h2>Bảo mật tài khoản</h2><p className="account-muted mt-2 mb-4">Bạn có thể đặt lại mật khẩu bằng mã xác minh gửi đến email hoặc số điện thoại đã xác minh.</p><Link className="account-button secondary" to="/forgot-password"><ShieldCheck size={16} />Đặt lại mật khẩu</Link></div>
      </section>
    </div>}
  </div>;
}
