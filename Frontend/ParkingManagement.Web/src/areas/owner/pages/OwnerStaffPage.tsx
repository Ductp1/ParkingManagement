import { StaffCard } from '@/areas/account/StaffCard';
import '@/areas/account/account.css';

/** Cổng chủ bãi → Nhân viên (US-075): danh sách nhân viên trực cổng, nút "Thêm nhân viên" mở popup. */
export function OwnerStaffPage() {
  return (
    <div className="acct">
      <div className="mx-auto w-full max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
        <h1 className="acct-title">Nhân viên</h1>
        <p className="acct-lede">Thêm người trực cổng cho từng bãi. Nhân viên mới nhận tài khoản và mật khẩu tạm qua email.</p>
        <div className="mt-8"><StaffCard /></div>
      </div>
    </div>
  );
}
