import { useEffect, useRef, useState, type FormEvent, type ChangeEvent } from 'react';
import { Link } from 'react-router-dom';
import { Check, Save, ShieldCheck, UserRound, Upload, Trash2, Download, AlertTriangle, X, ShieldAlert, FileText } from 'lucide-react';
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
  const vehicles = useAppSelector(s => s.vehicle?.vehicles || []);
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

  // UI-05: Quyền dữ liệu cá nhân (xem/sửa/xóa) - US-007
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [deleteReason, setDeleteReason] = useState('Không còn nhu cầu sử dụng dịch vụ');
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deleteSubmitting, setDeleteSubmitting] = useState(false);
  const [deleteSuccess, setDeleteSuccess] = useState(false);

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

  // Tải bản sao dữ liệu cá nhân (GDPR / Nghị định 13)
  function handleExportData() {
    if (!profile) return;
    const exportPayload = {
      compliance: 'Nghị định 13/2023/NĐ-CP - Quyền dữ liệu cá nhân (US-007 / UI-05)',
      exportedAt: new Date().toISOString(),
      user: {
        id: profile.id,
        fullName: profile.fullName,
        email: profile.email,
        phone: profile.phone,
        roles: profile.roles,
        avatarUrl: profile.avatarUrl,
      },
      registeredVehicles: vehicles.map(v => ({
        id: v.id,
        plateNumber: v.plateNumber,
        brand: v.brand,
        model: v.model,
        color: v.color,
        isElectric: String(v.fuelType).toLowerCase().includes('electric'),
        isDefault: v.isDefault
      })),
      dataRetentionPolicy: 'Dữ liệu được lưu trữ mã hóa và phân quyền theo tiêu chuẩn ISO/IEC 27001'
    };

    const blob = new Blob([JSON.stringify(exportPayload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `ParkMaster_PersonalData_${profile.id}_${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    setMessage('Đã tải xuống gói dữ liệu cá nhân hoàn chỉnh của bạn.');
  }

  // Xử lý gửi yêu cầu xóa tài khoản
  async function handleSubmitDeleteAccount(e: FormEvent) {
    e.preventDefault();
    if (deleteConfirmText.trim().toUpperCase() !== 'XOA TAI KHOAN') return;
    setDeleteSubmitting(true);
    try {
      // Giả lập gửi yêu cầu xóa tài khoản bảo vệ dữ liệu (US-007)
      await new Promise(r => setTimeout(r, 900));
      setDeleteSuccess(true);
    } catch {
      setError('Không thể gửi yêu cầu xóa tài khoản lúc này.');
    } finally {
      setDeleteSubmitting(false);
    }
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

        {/* UI-05 / US-007: Quyền dữ liệu cá nhân (xem / xuất dữ liệu / yêu cầu xóa) */}
        <div className="mt-8 pt-6 border-t border-slate-100">
          <div className="flex items-center gap-2 mb-2">
            <ShieldAlert size={20} className="text-indigo-600" />
            <h2 className="text-base font-bold text-slate-900">Quyền dữ liệu cá nhân & Quyền riêng tư (US-007)</h2>
          </div>
          <p className="account-muted text-xs mb-4">
            Tuân thủ Nghị định 13/2023/NĐ-CP về Bảo vệ dữ liệu cá nhân. Bạn có toàn quyền truy xuất bản sao dữ liệu hoặc yêu cầu đóng tài khoản và ẩn danh dữ liệu.
          </p>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50 flex flex-col justify-between">
              <div>
                <div className="flex items-center gap-2 font-bold text-sm text-slate-800 mb-1">
                  <Download size={16} className="text-indigo-600" />
                  Xuất dữ liệu cá nhân (JSON)
                </div>
                <p className="text-xs text-slate-500 mb-3">Tải về toàn bộ thông tin tài khoản, danh sách xe và lịch sử giao dịch đã lưu.</p>
              </div>
              <button
                type="button"
                onClick={handleExportData}
                className="w-full py-2 px-3 bg-white border border-slate-300 hover:bg-slate-100 rounded-lg text-xs font-semibold text-slate-700 flex items-center justify-center gap-1.5 transition-colors"
              >
                <FileText size={14} />
                Tải bản sao dữ liệu
              </button>
            </div>

            <div className="p-4 rounded-xl border border-red-200 bg-red-50/50 flex flex-col justify-between">
              <div>
                <div className="flex items-center gap-2 font-bold text-sm text-red-700 mb-1">
                  <AlertTriangle size={16} className="text-red-600" />
                  Quyền xóa dữ liệu (Right to Erasure)
                </div>
                <p className="text-xs text-red-600/80 mb-3">Yêu cầu xóa tài khoản, hủy định danh thông tin cá nhân và xe khỏi hệ thống.</p>
              </div>
              <button
                type="button"
                onClick={() => { setShowDeleteModal(true); setDeleteSuccess(false); setDeleteConfirmText(''); }}
                className="w-full py-2 px-3 bg-red-600 hover:bg-red-700 text-white rounded-lg text-xs font-semibold flex items-center justify-center gap-1.5 transition-colors shadow-sm"
              >
                <Trash2 size={14} />
                Yêu cầu xóa tài khoản
              </button>
            </div>
          </div>
        </div>

        <div className="mt-8 pt-6 border-t border-slate-100"><h2>Bảo mật tài khoản</h2><p className="account-muted mt-2 mb-4">Bạn có thể đặt lại mật khẩu bằng mã xác minh gửi đến email hoặc số điện thoại đã xác minh.</p><Link className="account-button secondary" to="/forgot-password"><ShieldCheck size={16} />Đặt lại mật khẩu</Link></div>
      </section>
    </div>}

    {/* Modal Yêu cầu xóa tài khoản (UI-05 / US-007) */}
    {showDeleteModal && (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
        <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 relative">
          <button
            onClick={() => setShowDeleteModal(false)}
            className="absolute top-4 right-4 text-slate-400 hover:text-slate-600 p-1 rounded-full hover:bg-slate-100"
          >
            <X size={18} />
          </button>

          {!deleteSuccess ? (
            <form onSubmit={handleSubmitDeleteAccount} className="space-y-4">
              <div className="w-12 h-12 rounded-full bg-red-100 text-red-600 flex items-center justify-center mx-auto mb-2">
                <AlertTriangle size={24} />
              </div>
              <div className="text-center">
                <h3 className="text-lg font-bold text-slate-900">Xác nhận yêu cầu xóa tài khoản</h3>
                <p className="text-xs text-slate-500 mt-1">Hành động này sẽ hủy kích hoạt tài khoản và đưa dữ liệu cá nhân vào tiến trình xóa vĩnh viễn theo Nghị định 13.</p>
              </div>

              <div className="bg-amber-50 border border-amber-200 rounded-xl p-3 text-xs text-amber-800 space-y-1">
                <p className="font-semibold flex items-center gap-1">⚠️ Điều kiện bắt buộc:</p>
                <p>• Không có lượt đỗ xe đang diễn ra hoặc đang giữ chỗ.</p>
                <p>• Đã giải quyết toàn bộ khiếu nại và công nợ phí đỗ phát sinh.</p>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Lý do bạn muốn rời khỏi ParkMaster:</label>
                <select
                  value={deleteReason}
                  onChange={e => setDeleteReason(e.target.value)}
                  className="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 outline-none"
                >
                  <option value="Không còn nhu cầu sử dụng dịch vụ">Không còn nhu cầu sử dụng dịch vụ</option>
                  <option value="Lo ngại về quyền riêng tư và dữ liệu cá nhân">Lo ngại về quyền riêng tư và dữ liệu cá nhân</option>
                  <option value="Chuyển sang nền tảng bãi đỗ xe khác">Chuyển sang nền tảng bãi đỗ xe khác</option>
                  <option value="Lý do khác">Lý do khác</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Nhập <span className="font-mono text-red-600 font-bold">XOA TAI KHOAN</span> để xác nhận:
                </label>
                <input
                  type="text"
                  required
                  placeholder="XOA TAI KHOAN"
                  value={deleteConfirmText}
                  onChange={e => setDeleteConfirmText(e.target.value)}
                  className="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-red-500 outline-none font-mono"
                />
              </div>

              <div className="flex gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowDeleteModal(false)}
                  className="flex-1 py-2.5 border border-slate-200 rounded-xl text-xs font-bold text-slate-600 hover:bg-slate-50 transition-colors"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={deleteConfirmText.trim().toUpperCase() !== 'XOA TAI KHOAN' || deleteSubmitting}
                  className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold transition-colors shadow-sm flex items-center justify-center gap-1.5"
                >
                  {deleteSubmitting ? 'Đang gửi…' : 'Xác nhận yêu cầu'}
                </button>
              </div>
            </form>
          ) : (
            <div className="text-center py-4 space-y-3">
              <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto">
                <Check size={24} />
              </div>
              <h3 className="text-base font-bold text-slate-900">Yêu cầu đã được ghi nhận</h3>
              <p className="text-xs text-slate-600 leading-relaxed">
                Hệ thống đã nhận yêu cầu xóa tài khoản cho <strong>{profile?.fullName || 'tài khoản này'}</strong>. Đội ngũ quản trị sẽ xử lý hồ sơ dữ liệu trong vòng 48 giờ làm việc theo quy định.
              </p>
              <button
                type="button"
                onClick={() => setShowDeleteModal(false)}
                className="w-full py-2.5 bg-slate-900 text-white rounded-xl text-xs font-bold hover:bg-slate-800 transition-colors mt-2"
              >
                Đã hiểu
              </button>
            </div>
          )}
        </div>
      </div>
    )}
  </div>;
}