import { useState } from 'react';
import { ownerStaffApi, type StaffItemDto } from '@/api/ownerStaffApi';
import { errorText } from '@/features/shifts/format';
import { Banner, Button, Modal, ModalActions } from '@/features/shifts/ui';

interface RevokeStaffModalProps {
  staff: StaffItemDto;
  lotName: string;
  onClose: () => void;
  onRevoked: () => void;
}

export function RevokeStaffModal({ staff, lotName, onClose, onRevoked }: RevokeStaffModalProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function confirm() {
    setBusy(true);
    setError('');
    try {
      await ownerStaffApi.revokeStaff(staff.id);
      onRevoked();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <Modal title="Gỡ nhân viên khỏi bãi?" subtitle={`${staff.fullName} · ${lotName}`} onClose={onClose} busy={busy}>
      <Banner tone="warning" title="Ca đang trực sẽ bị đóng tự động">
        Quyền vận hành tại bãi bị thu hồi ngay. Ca và tiền mặt cần Chủ bãi kiểm tra.
      </Banner>
      <p className="text-[13px] text-[#10172a]">Phân công tại các bãi khác không thay đổi. Lịch sử thao tác vẫn được giữ.</p>
      {error && <Banner tone="danger" title="Chưa gỡ được nhân viên">{error}</Banner>}
      <ModalActions>
        <Button variant="secondary" onClick={onClose} disabled={busy}>Giữ phân công</Button>
        <Button variant="danger" busy={busy} onClick={() => void confirm()}>Gỡ khỏi bãi</Button>
      </ModalActions>
    </Modal>
  );
}
