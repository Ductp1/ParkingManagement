import { useRef, useState } from 'react';
import { Check, Fingerprint, Link2, ShieldCheck, Unlink, X } from 'lucide-react';

// UI demonstration only. Never used as proof of identity or sent to an API.
export function VneidDemoCard({ userId, disabled = false }: { userId: number; disabled?: boolean }) {
  const storageKey = `parkmaster_vneid_demo_${userId}`;
  const [linked, setLinked] = useState(() => {
    try { return sessionStorage.getItem(storageKey) === 'linked'; }
    catch { return false; }
  });
  const [mode, setMode] = useState<'link' | 'unlink'>('link');
  const [message, setMessage] = useState('');
  const dialog = useRef<HTMLDialogElement>(null);

  function open(nextMode: 'link' | 'unlink') {
    setMode(nextMode);
    setMessage('');
    dialog.current?.showModal();
  }
  function confirm() {
    const next = mode === 'link';
    setLinked(next);
    let remembered = true;
    try {
      if (next) sessionStorage.setItem(storageKey, 'linked');
      else sessionStorage.removeItem(storageKey);
    } catch { remembered = false; }
    setMessage(`${next ? 'Đã liên kết VNeID trong bản giả lập.' : 'Đã hủy liên kết giả lập.'}${remembered ? '' : ' Trình duyệt không cho lưu trạng thái; trạng thái này sẽ mất khi tải lại trang.'}`);
    dialog.current?.close();
  }

  return <section className="mt-8 pt-6 border-t border-slate-100" aria-labelledby="vneid-demo-heading">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-3">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-indigo-50 text-indigo-600"><Fingerprint size={23} /></span>
        <div><h2 id="vneid-demo-heading">Liên kết VNeID</h2><p className="account-muted text-xs">Trải nghiệm liên kết định danh điện tử</p></div>
      </div>
      <span className="account-badge pending">BẢN GIẢ LẬP</span>
    </div>
    <p className="account-muted mt-4">Đây là mô phỏng giao diện, không kết nối VNeID thật và không xác thực danh tính. Bạn không cần nhập số CCCD, mật khẩu hay OTP VNeID.</p>
    <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-4 flex flex-wrap items-center justify-between gap-4">
      <div className="flex items-center gap-2">
        {linked ? <ShieldCheck size={19} className="text-emerald-600 shrink-0" /> : <Link2 size={19} className="text-slate-400 shrink-0" />}
        <div><p className="text-sm font-semibold text-slate-800">{linked ? 'Đã liên kết (giả lập)' : 'Chưa liên kết'}</p><p className="account-muted text-xs">{linked ? 'Trạng thái demo chỉ lưu trong phiên trình duyệt này.' : 'Thử quy trình liên kết bằng dữ liệu mô phỏng.'}</p></div>
      </div>
      <button type="button" className="account-button secondary" disabled={disabled} onClick={() => open(linked ? 'unlink' : 'link')}>
        {linked ? <Unlink size={16} /> : <Link2 size={16} />}{linked ? 'Hủy liên kết giả lập' : 'Liên kết VNeID giả lập'}
      </button>
    </div>
    {message && <p role="status" className="account-notice success mt-4 mb-0">{message}</p>}
    <dialog ref={dialog} className="account-dialog" aria-labelledby="vneid-demo-dialog-title" aria-describedby="vneid-demo-description">
      <div className="flex items-start justify-between gap-4"><div><span className="account-badge pending mb-3">MÔ PHỎNG · KHÔNG XÁC THỰC THẬT</span><h2 id="vneid-demo-dialog-title">{mode === 'link' ? 'Liên kết VNeID giả lập' : 'Hủy liên kết giả lập?'}</h2></div><button type="button" className="account-button secondary" aria-label="Đóng giả lập VNeID" onClick={() => dialog.current?.close()}><X size={16} /></button></div>
      <p id="vneid-demo-description" className="account-muted mt-4">{mode === 'link' ? 'Thao tác dưới đây mô phỏng bước chấp thuận liên kết. Không mở ứng dụng VNeID, không gửi dữ liệu ra ngoài và không thay đổi trạng thái KYC của tài khoản.' : 'Thao tác này chỉ xóa trạng thái demo trong trình duyệt; không ảnh hưởng hồ sơ hoặc bất kỳ tài khoản VNeID thật nào.'}</p>
      {mode === 'link' && <div className="mt-5 rounded-xl bg-indigo-50 p-4 text-sm text-indigo-800 flex gap-3"><Fingerprint size={22} className="shrink-0" /><p>Bấm xác nhận để hiển thị trạng thái <strong>Đã liên kết (giả lập)</strong> trong hồ sơ.</p></div>}
      <div className="account-actions mt-6"><button type="button" className="account-button secondary" onClick={() => dialog.current?.close()}>{mode === 'link' ? 'Để sau' : 'Giữ liên kết giả lập'}</button><button type="button" className="account-button" onClick={confirm}>{mode === 'link' ? <Check size={16} /> : <Unlink size={16} />}{mode === 'link' ? 'Xác nhận liên kết giả lập' : 'Hủy liên kết giả lập'}</button></div>
    </dialog>
  </section>;
}
