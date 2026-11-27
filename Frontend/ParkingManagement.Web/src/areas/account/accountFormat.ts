export const money = (value: number) => new Intl.NumberFormat('vi-VN').format(value) + ' ₫';
export const dateTime = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '';
export const date = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('vi-VN') : '');
export const time = (iso: string) => new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

export const kycLabels: Record<string, { text: string; tone: 'ok' | 'wait' | 'bad' | 'idle' }> = {
  NotSubmitted: { text: 'Chưa xác minh', tone: 'idle' },
  Pending: { text: 'Đang chờ duyệt', tone: 'wait' },
  Verified: { text: 'Đã xác minh', tone: 'ok' },
  Rejected: { text: 'Cần gửi lại', tone: 'bad' },
};
export const documentLabels: Record<string, string> = { CitizenId: 'Căn cước công dân', DriverLicense: 'Giấy phép lái xe' };

export const bookingLabels: Record<string, { text: string; tone: 'ok' | 'wait' | 'bad' | 'idle' | 'live' }> = {
  Created: { text: 'Vừa tạo', tone: 'wait' },
  PendingPayment: { text: 'Chờ thanh toán', tone: 'wait' },
  PendingOwnerApproval: { text: 'Chờ bãi duyệt', tone: 'wait' },
  Confirmed: { text: 'Đã xác nhận', tone: 'live' },
  CheckedIn: { text: 'Đã vào bãi', tone: 'live' },
  Parking: { text: 'Đang gửi', tone: 'live' },
  CheckedOut: { text: 'Đã ra bãi', tone: 'ok' },
  Completed: { text: 'Hoàn tất', tone: 'ok' },
  Expired: { text: 'Hết hạn giữ chỗ', tone: 'idle' },
  Cancelled: { text: 'Đã hủy', tone: 'idle' },
  CancelledNoRefund: { text: 'Đã hủy, không hoàn tiền', tone: 'bad' },
  NoShow: { text: 'Không đến', tone: 'bad' },
  Rejected: { text: 'Bãi từ chối', tone: 'bad' },
};
export const paymentLabels: Record<string, string> = {
  Pending: 'Đang chờ', Succeeded: 'Thành công', Failed: 'Thất bại', Cancelled: 'Đã hủy',
  Refunded: 'Đã hoàn tiền', PartiallyRefunded: 'Hoàn một phần',
};
export const methodLabels: Record<string, string> = { Vnpay: 'VNPAY', Momo: 'MoMo', VietQr: 'VietQR', Cash: 'Tiền mặt' };

export const requestLabels: Record<string, string> = {
  Export: 'Tải bản sao dữ liệu', Correction: 'Yêu cầu sửa dữ liệu', Deletion: 'Xóa tài khoản',
  Access: 'Xem dữ liệu', WithdrawConsent: 'Rút lại đồng ý',
};
export const requestStatus: Record<string, { text: string; tone: 'ok' | 'wait' | 'bad' | 'idle' }> = {
  Submitted: { text: 'Đã tiếp nhận', tone: 'wait' },
  InProgress: { text: 'Đang xử lý', tone: 'wait' },
  Completed: { text: 'Đã xong', tone: 'ok' },
  Rejected: { text: 'Bị từ chối', tone: 'bad' },
};
export const roleLabels: Record<string, string> = {
  Driver: 'Tài xế', LotOwner: 'Chủ bãi', Owner: 'Chủ bãi', Staff: 'Nhân viên', Admin: 'Quản trị viên',
};
export const securityLabels: Record<string, string> = {
  LoginSucceeded: 'Đăng nhập', LoginFailed: 'Đăng nhập sai', OtpFailed: 'Nhập sai mã OTP', AccountLocked: 'Tài khoản bị khóa',
  AccountUnlocked: 'Mở khóa tài khoản', PasswordChanged: 'Đổi mật khẩu', TokenRevoked: 'Đăng xuất phiên',
  KycSubmitted: 'Gửi hồ sơ định danh', KycApproved: 'Định danh được duyệt', KycRejected: 'Định danh bị từ chối',
};
