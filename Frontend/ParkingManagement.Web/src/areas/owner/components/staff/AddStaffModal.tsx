import { useState, type FormEvent } from 'react';
import { ownerStaffApi, type OwnerLotOptionDto, type StaffItemDto } from '@/api/ownerStaffApi';
import { errorCode, errorStatus, errorText, lotStatusLabel } from '@/features/shifts/format';
import { Banner, Button, Field, Modal, ModalActions, inputClass } from '@/features/shifts/ui';

interface AddStaffModalProps {
  lots: OwnerLotOptionDto[];
  defaultLotId?: number;
  onClose: () => void;
  onInvited: (item: StaffItemDto) => void;
}

/** Lỗi thuộc về email (Figma Owner 03, 07, 08, 09) – hiện ngay dưới ô email. */
const EMAIL_CODES = ['STAFF_EMAIL_HAS_ACCOUNT', 'STAFF_EMAIL_OTHER_OWNER', 'STAFF_ALREADY_ASSIGNED', 'STAFF_EMAIL_REQUIRED'];
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function AddStaffModal({ lots, defaultLotId, onClose, onInvited }: AddStaffModalProps) {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [lotId, setLotId] = useState(defaultLotId ?? lots[0]?.id ?? 0);
  const [busy, setBusy] = useState(false);
  const [touched, setTouched] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);

  const nameError = touched && !fullName.trim() ? 'Hãy nhập họ và tên nhân viên.' : null;
  const emailInvalid = touched && !EMAIL.test(email.trim()) ? 'Hãy nhập email hợp lệ của nhân viên.' : null;
  const emailApiError = failure && EMAIL_CODES.includes(errorCode(failure) ?? '') ? errorText(failure) : null;
  const unavailable = errorStatus(failure) === 503;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);
    if (busy || !fullName.trim() || !EMAIL.test(email.trim()) || !lotId) return;
    setBusy(true);
    setFailure(null);
    try {
      onInvited(await ownerStaffApi.createStaff({ fullName: fullName.trim(), contact: email.trim(), parkingLotId: lotId }));
    } catch (error) {
      setFailure(error);
      setBusy(false);
    }
  }

  return (
    <Modal busy={busy} onClose={onClose}
      title={!failure ? 'Mời nhân viên' : unavailable ? 'Chưa thể gửi lời mời' : 'Không thể gửi lời mời'}
      subtitle={!failure ? 'Nhân viên nhận link kích hoạt để tự đặt mật khẩu. Link có hiệu lực 72 giờ.'
        : unavailable ? 'Không tạo tài khoản mới khi hệ thống chưa thể gửi email.'
          : 'Thông tin nhân viên được giữ lại để bạn chỉnh sửa.'}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        {failure != null && !emailApiError && (
          <Banner tone="danger" title={unavailable ? 'Dịch vụ chưa sẵn sàng' : 'Chưa gửi được lời mời'}>
            {errorText(failure)} Thông tin nhân viên được giữ lại để bạn không cần nhập lại.
          </Banner>
        )}
        <Field label="Họ và tên" required error={nameError}>
          <input className={inputClass} value={fullName} maxLength={150} disabled={busy} aria-invalid={!!nameError}
            placeholder="Ví dụ: Nguyễn Văn Bình" onChange={(e) => setFullName(e.target.value)} />
        </Field>
        <Field label="Email nhân viên" required error={emailInvalid ?? emailApiError}
          hint="Dùng email riêng chưa thuộc tài khoản Tài xế hoặc Chủ bãi.">
          <input className={inputClass} type="email" value={email} maxLength={256} disabled={busy}
            aria-invalid={!!(emailInvalid ?? emailApiError)} placeholder="nhanvien@example.com"
            onChange={(e) => { setEmail(e.target.value); setFailure(null); }} />
        </Field>
        <Field label="Phân công tại bãi" required>
          <select className={inputClass} value={lotId} disabled={busy} onChange={(e) => { setLotId(Number(e.target.value)); setFailure(null); }}>
            {lots.map((lot) => (
              <option key={lot.id} value={lot.id}>{lot.name ?? `Bãi #${lot.id}`} · {lotStatusLabel(lot.status)}</option>
            ))}
          </select>
        </Field>
        <Banner tone="info" title="Một nhân viên — một chủ bãi">
          Có thể gán thêm bãi cho nhân viên đã thuộc chủ bãi của bạn.
        </Banner>
        <ModalActions>
          <Button variant="secondary" onClick={onClose} disabled={busy}>{unavailable ? 'Đóng' : 'Hủy'}</Button>
          <Button type="submit" busy={busy}>{!failure ? 'Gửi lời mời' : unavailable ? 'Thử lại' : 'Gửi lại'}</Button>
        </ModalActions>
      </form>
    </Modal>
  );
}
