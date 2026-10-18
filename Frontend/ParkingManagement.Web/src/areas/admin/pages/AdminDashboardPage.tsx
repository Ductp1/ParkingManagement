import React, { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

interface SystemUser {
  id: number;
  fullName: string;
  email: string;
  phone: string;
  role: 'Driver' | 'Owner' | 'Staff' | 'Admin';
  status: 'Active' | 'Locked';
  createdAt: string;
}

interface KybApplication {
  id: number;
  lotName: string;
  companyName: string;
  taxCode: string;
  capacity: number;
  address: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  submittedAt: string;
}

interface FeatureToggle {
  id: string;
  name: string;
  description: string;
  category: string;
  enabled: boolean;
  stage: 'Production' | 'Beta' | 'Experimental';
}

const INITIAL_USERS: SystemUser[] = [
  { id: 1, fullName: 'Nguyễn Văn An', email: 'driver1@smartparking.vn', phone: '0901234567', role: 'Driver', status: 'Active', createdAt: '2026-01-15' },
  { id: 2, fullName: 'Trần Thị Bình', email: 'driver2@smartparking.vn', phone: '0912345678', role: 'Driver', status: 'Active', createdAt: '2026-02-10' },
  { id: 3, fullName: 'Vincom Mega Mall Parking', email: 'owner.vincom@smartparking.vn', phone: '0283822123', role: 'Owner', status: 'Active', createdAt: '2025-11-20' },
  { id: 4, fullName: 'Bitexco Parking Management', email: 'owner.bitexco@smartparking.vn', phone: '0283911009', role: 'Owner', status: 'Active', createdAt: '2025-12-05' },
  { id: 5, fullName: 'Lê Văn Cổng', email: 'staff.vincom@smartparking.vn', phone: '0933445566', role: 'Staff', status: 'Active', createdAt: '2026-03-01' },
  { id: 6, fullName: 'Phạm Thị Soát', email: 'staff.bitexco@smartparking.vn', phone: '0944556677', role: 'Staff', status: 'Active', createdAt: '2026-03-15' },
  { id: 7, fullName: 'Smart Parking Admin', email: 'admin@smartparking.vn', phone: '0988889999', role: 'Admin', status: 'Active', createdAt: '2025-10-01' },
  { id: 8, fullName: 'Tài khoản Vi phạm', email: 'spam.user@temporary.com', phone: '0999000111', role: 'Driver', status: 'Locked', createdAt: '2026-04-02' },
];

const INITIAL_KYB: KybApplication[] = [
  { id: 101, lotName: 'Vincom Mega Mall Thảo Điền', companyName: 'Công ty Cổ phần Vincom Retail', taxCode: '0101535451', capacity: 450, address: '161 Xa lộ Hà Nội, P. Thảo Điền, TP. Thủ Đức', status: 'Approved', submittedAt: '2026-03-20' },
  { id: 102, lotName: 'Bitexco Financial Tower Parking', companyName: 'Tập đoàn Bitexco', taxCode: '0302821123', capacity: 280, address: '2 Hải Triều, P. Bến Nghé, Quận 1, TP.HCM', status: 'Approved', submittedAt: '2026-03-22' },
  { id: 103, lotName: 'Landmark 81 Central Park Yard', companyName: 'Công ty CP Đầu tư Xây dựng Tân Liên Phát', taxCode: '0313123890', capacity: 620, address: '720A Điện Biên Phủ, Phường 22, Bình Thạnh', status: 'Pending', submittedAt: '2026-04-10' },
  { id: 104, lotName: 'Hầm gửi xe Saigon Centre', companyName: 'Keppel Land Watco Co., Ltd', taxCode: '0301443219', capacity: 350, address: '65 Lê Lợi, P. Bến Nghé, Quận 1', status: 'Pending', submittedAt: '2026-04-12' },
  { id: 105, lotName: 'Bãi đỗ ngoài trời SaiGon Zoo', companyName: 'Công ty TNHH MTV Thảo Cầm Viên', taxCode: '0300468301', capacity: 120, address: '2 Nguyễn Bỉnh Khiêm, P. Bến Nghé, Quận 1', status: 'Rejected', submittedAt: '2026-03-18' },
];

const INITIAL_TOGGLES: FeatureToggle[] = [
  { id: 'anpr_camera', name: 'Nhận diện biển số tự động ANPR (AI Camera)', description: 'Tự động đọc biển số xe tại làn vào/ra và khớp vé đặt chỗ trong vòng 300ms.', category: 'Phần cứng & Trực cổng', enabled: true, stage: 'Production' },
  { id: 'ai_advisor', name: 'Trợ lý AI Đề xuất Bãi đỗ tối ưu (AI Parking Advisor)', description: 'Sử dụng mô hình dự báo mật độ để đề xuất bãi đỗ gần nhất còn chỗ trống và giá tốt.', category: 'Trải nghiệm Người dùng', enabled: true, stage: 'Beta' },
  { id: 'map_3d', name: 'Sơ đồ bãi đỗ không gian tương tác 3D Map', description: 'Hiển thị mô hình 3D ô đỗ xe đa tầng theo thời gian thực cho tài xế và chủ bãi.', category: 'Giao diện & Trực quan', enabled: false, stage: 'Beta' },
  { id: 'dynamic_vietqr', name: 'Thanh toán tự động VietQR động SePay', description: 'Sinh mã VietQR chuyển khoản tức thì có kèm nội dung đối soát tự động.', category: 'Thanh toán & Ví', enabled: true, stage: 'Production' },
  { id: 'biometric_auth', name: 'Đăng nhập sinh trắc học FaceID / WebAuthn', description: 'Cho phép tài xế đăng nhập nhanh vào ứng dụng qua vân tay hoặc FaceID trên di động.', category: 'Bảo mật & Xác thực', enabled: false, stage: 'Experimental' },
];

export const AdminDashboardPage: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();

  // Xác định active tab từ đường dẫn URL
  const getTabFromPath = (pathname: string): 'overview' | 'users' | 'kyb' | 'parameters' | 'toggles' | 'settlements' => {
    if (pathname.includes('/admin/users')) return 'users';
    if (pathname.includes('/admin/kyb-approvals')) return 'kyb';
    if (pathname.includes('/admin/parameters')) return 'parameters';
    if (pathname.includes('/admin/toggles')) return 'toggles';
    if (pathname.includes('/admin/settlements')) return 'settlements';
    return 'overview';
  };

  const activeTab = getTabFromPath(location.pathname);

  const setTab = (tab: 'overview' | 'users' | 'kyb' | 'parameters' | 'toggles' | 'settlements') => {
    const routeMap = {
      overview: '/admin',
      users: '/admin/users',
      kyb: '/admin/kyb-approvals',
      parameters: '/admin/parameters',
      toggles: '/admin/toggles',
      settlements: '/admin/settlements',
    };
    navigate(routeMap[tab]);
  };

  // State
  const [users, setUsers] = useState<SystemUser[]>(INITIAL_USERS);
  const [userSearch, setUserSearch] = useState('');
  const [userRoleFilter, setUserRoleFilter] = useState<string>('All');

  const [kybList, setKybList] = useState<KybApplication[]>(INITIAL_KYB);
  const [toggles, setToggles] = useState<FeatureToggle[]>(INITIAL_TOGGLES);

  // System parameters
  const [params, setParams] = useState({
    holdDurationMinutes: 15,
    gracePeriodMinutes: 10,
    platformFeePercent: 10,
    minDepositPercent: 30,
    freeCancelHours: 2,
  });

  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // User Actions
  const handleToggleUserStatus = (id: number) => {
    setUsers((prev) =>
      prev.map((u) => {
        if (u.id === id) {
          const nextStatus = u.status === 'Active' ? 'Locked' : 'Active';
          showToast(`Đã ${nextStatus === 'Active' ? 'mở khóa' : 'khóa'} tài khoản ${u.fullName}!`);
          return { ...u, status: nextStatus };
        }
        return u;
      })
    );
  };

  // KYB Actions
  const handleApproveKyb = (id: number) => {
    setKybList((prev) =>
      prev.map((k) => (k.id === id ? { ...k, status: 'Approved' } : k))
    );
    showToast('Đã phê duyệt hồ sơ KYB đối tác thành công!');
  };

  const handleRejectKyb = (id: number) => {
    setKybList((prev) =>
      prev.map((k) => (k.id === id ? { ...k, status: 'Rejected' } : k))
    );
    showToast('Đã từ chối hồ sơ đối tác (đã gửi thông báo yêu cầu bổ sung giấy tờ).');
  };

  // Toggle Actions
  const handleSwitchToggle = (id: string) => {
    setToggles((prev) =>
      prev.map((t) => {
        if (t.id === id) {
          const next = !t.enabled;
          showToast(`Đã ${next ? 'kích hoạt' : 'tắt'} tính năng: ${t.name}`);
          return { ...t, enabled: next };
        }
        return t;
      })
    );
  };

  // Filtered Users
  const filteredUsers = users.filter((u) => {
    const matchSearch =
      u.fullName.toLowerCase().includes(userSearch.toLowerCase()) ||
      u.email.toLowerCase().includes(userSearch.toLowerCase()) ||
      u.phone.includes(userSearch);
    const matchRole = userRoleFilter === 'All' || u.role === userRoleFilter;
    return matchSearch && matchRole;
  });

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 bg-slate-900 text-white rounded-xl shadow-xl text-sm font-semibold animate-bounce">
          <span className="material-symbols-outlined text-emerald-400 text-[20px]">check_circle</span>
          <span>{toastMessage}</span>
        </div>
      )}

      {/* ===== HEADER ===== */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-purple-100 text-purple-800 text-xs font-semibold uppercase tracking-wider mb-2">
            ⚙️ Cổng Quản Trị Hệ Thống Toàn Sàn (Admin Portal)
          </div>
          <h1 className="text-2xl md:text-3xl font-extrabold text-slate-900 tracking-tight">
            Trung tâm Điều phối & Quản trị Hệ thống
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Giám sát vận hành microservices, kiểm soát tài khoản RBAC, thẩm định hồ sơ KYB chủ bãi và cấu hình tham số sàn ParkMaster.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 text-xs font-bold border border-emerald-200">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            HẠ TẦNG 5/5 SERVICE HOẠT ĐỘNG
          </span>
        </div>
      </div>


      {/* ===== TAB 1: OVERVIEW & SYSTEM MONITORING ===== */}
      {activeTab === 'overview' && (
        <div className="space-y-6">
          {/* KPI Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Tổng người dùng</span>
                <div className="w-9 h-9 rounded-xl bg-purple-100 text-purple-700 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[20px]">group</span>
                </div>
              </div>
              <div className="mt-3 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900">14,280</span>
                <span className="text-xs font-bold text-emerald-600">+12.4% tháng này</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-1">13,920 Tài xế • 52 Chủ bãi • 308 Soát vé</p>
            </div>

            <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Bãi đỗ liên kết</span>
                <div className="w-9 h-9 rounded-xl bg-blue-100 text-blue-700 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[20px]">local_parking</span>
                </div>
              </div>
              <div className="mt-3 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900">52 bãi</span>
                <span className="text-xs font-bold text-amber-600">2 bãi chờ duyệt KYB</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-1">Tổng sức chứa 18,450 ô đỗ xe</p>
            </div>

            <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Doanh số toàn sàn</span>
                <div className="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[20px]">payments</span>
                </div>
              </div>
              <div className="mt-3 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900">842.5 Tr ₫</span>
                <span className="text-xs font-bold text-purple-700">Hoa hồng sàn: 84.2 Tr</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-1">Áp dụng tỷ lệ hoa hồng cố định 10%</p>
            </div>

            <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Trạng thái Hạ tầng</span>
                <div className="w-9 h-9 rounded-xl bg-teal-100 text-teal-700 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[20px]">dns</span>
                </div>
              </div>
              <div className="mt-3 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900">99.98%</span>
                <span className="text-xs font-bold text-emerald-600">Uptime SLA</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-1">Độ trễ phản hồi TB: 21ms</p>
            </div>
          </div>

          {/* Microservices Health Grid */}
          <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
                  <span className="material-symbols-outlined text-purple-600">monitor_heart</span>
                  Giám sát Sức khỏe Microservices Hệ thống
                </h3>
                <p className="text-xs text-slate-500 mt-0.5">Kiểm tra liveness/readiness probes tự động qua API Gateway :5000</p>
              </div>
              <button
                onClick={() => showToast('Đã làm mới dữ liệu telemetry hệ thống thành công!')}
                className="px-3 py-1.5 text-xs font-bold text-purple-700 bg-purple-50 rounded-lg hover:bg-purple-100 transition-colors flex items-center gap-1 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">refresh</span>
                Kiểm tra lại
              </button>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
              {[
                { name: 'AuthService', port: ':5101', role: 'Xác thực & JWT', status: 'Healthy', latency: '14ms', ok: true },
                { name: 'ParkingService', port: ':5102', role: 'Bãi đỗ & Ô trống', status: 'Healthy', latency: '22ms', ok: true },
                { name: 'GateService', port: ':5107', role: 'Barie & ANPR', status: 'Healthy', latency: '18ms', ok: true },
                { name: 'PaymentService', port: ':5105', role: 'Ví & VietQR', status: 'Healthy', latency: '38ms', ok: true },
                { name: 'NotificationService', port: ':5106', role: 'Push & SSE', status: 'Healthy', latency: '12ms', ok: true },
              ].map((svc) => (
                <div key={svc.name} className="p-4 rounded-xl border border-slate-100 bg-slate-50/70 hover:border-purple-200 transition-all">
                  <div className="flex items-center justify-between mb-2">
                    <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
                    <span className="text-[10px] font-mono font-bold text-slate-400">{svc.port}</span>
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">{svc.name}</h4>
                  <p className="text-[11px] text-slate-500">{svc.role}</p>
                  <div className="mt-3 pt-2 border-t border-slate-200 flex items-center justify-between text-[11px]">
                    <span className="font-bold text-emerald-700 bg-emerald-50 px-1.5 py-0.5 rounded">{svc.status}</span>
                    <span className="font-mono text-slate-500">{svc.latency}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Quick Links & Recent Audit Logs */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Quick Actions */}
            <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-3">
              <h3 className="font-bold text-slate-900 text-base mb-2">Thao tác Quản trị Nhanh</h3>
              <button
                onClick={() => setTab('users')}
                className="w-full text-left p-3 rounded-xl border border-slate-100 hover:border-purple-200 hover:bg-purple-50/50 transition-all flex items-center justify-between group cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-purple-100 text-purple-700 flex items-center justify-center">
                    <span className="material-symbols-outlined text-[18px]">manage_accounts</span>
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">Quản lý Tài khoản & Phân quyền</h4>
                    <p className="text-[11px] text-slate-500">Khóa/mở tài khoản, cấp quyền Admin</p>
                  </div>
                </div>
                <span className="material-symbols-outlined text-[18px] text-slate-400 group-hover:text-purple-600">chevron_right</span>
              </button>

              <button
                onClick={() => setTab('kyb')}
                className="w-full text-left p-3 rounded-xl border border-slate-100 hover:border-purple-200 hover:bg-purple-50/50 transition-all flex items-center justify-between group cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-blue-100 text-blue-700 flex items-center justify-center">
                    <span className="material-symbols-outlined text-[18px]">verified_user</span>
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">Phê duyệt Hồ sơ KYB Đối tác</h4>
                    <p className="text-[11px] text-slate-500">2 hồ sơ mới đang chờ thẩm định</p>
                  </div>
                </div>
                <span className="material-symbols-outlined text-[18px] text-slate-400 group-hover:text-purple-600">chevron_right</span>
              </button>

              <button
                onClick={() => setTab('parameters')}
                className="w-full text-left p-3 rounded-xl border border-slate-100 hover:border-purple-200 hover:bg-purple-50/50 transition-all flex items-center justify-between group cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center">
                    <span className="material-symbols-outlined text-[18px]">tune</span>
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">Cấu hình Tham số Vận hành</h4>
                    <p className="text-[11px] text-slate-500">Thời gian giữ chỗ 15p, Phí hoa hồng 10%</p>
                  </div>
                </div>
                <span className="material-symbols-outlined text-[18px] text-slate-400 group-hover:text-purple-600">chevron_right</span>
              </button>

              <button
                onClick={() => setTab('toggles')}
                className="w-full text-left p-3 rounded-xl border border-slate-100 hover:border-purple-200 hover:bg-purple-50/50 transition-all flex items-center justify-between group cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-amber-100 text-amber-700 flex items-center justify-center">
                    <span className="material-symbols-outlined text-[18px]">toggle_on</span>
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">Feature Toggles (Bật/Tắt Tính năng)</h4>
                    <p className="text-[11px] text-slate-500">ANPR OCR, AI Advisor, 3D Map</p>
                  </div>
                </div>
                <span className="material-symbols-outlined text-[18px] text-slate-400 group-hover:text-purple-600">chevron_right</span>
              </button>
            </div>

            {/* Audit Logs */}
            <div className="lg:col-span-2 p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
              <h3 className="font-bold text-slate-900 text-base mb-1 flex items-center gap-2">
                <span className="material-symbols-outlined text-slate-600">history</span>
                Nhật ký Kiểm toán Hệ thống (Audit Log)
              </h3>
              <p className="text-xs text-slate-500 mb-4">Lưu vết tất cả thao tác quản trị viên và thay đổi cấu hình bảo mật</p>

              <div className="space-y-3">
                {[
                  { time: '10:45 Hôm nay', actor: 'Smart Parking Admin', action: 'Phê duyệt hồ sơ KYB', detail: 'Vincom Mega Mall Thảo Điền (450 ô)', status: 'Thành công', color: 'text-emerald-700 bg-emerald-50' },
                  { time: '09:12 Hôm nay', actor: 'Smart Parking Admin', action: 'Cập nhật Tham số Sàn', detail: 'Thời gian giữ chỗ booking = 15 phút', status: 'Đã lưu', color: 'text-blue-700 bg-blue-50' },
                  { time: '16:30 Hôm qua', actor: 'Smart Parking Admin', action: 'Khóa tài khoản vi phạm', detail: 'spam.user@temporary.com do vi phạm spam đặt chỗ', status: 'Đã khóa', color: 'text-red-700 bg-red-50' },
                  { time: '14:20 Hôm qua', actor: 'Smart Parking Admin', action: 'Bật Feature Toggle', detail: 'Kích hoạt ANPR AI Camera cho toàn cụm bãi Quận 1', status: 'Kích hoạt', color: 'text-purple-700 bg-purple-50' },
                ].map((log, idx) => (
                  <div key={idx} className="p-3 rounded-xl border border-slate-100 flex items-center justify-between text-xs">
                    <div className="space-y-0.5">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-900">{log.action}</span>
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${log.color}`}>{log.status}</span>
                      </div>
                      <p className="text-slate-500">{log.detail} • Thực hiện bởi <span className="font-medium text-slate-700">{log.actor}</span></p>
                    </div>
                    <span className="text-[11px] text-slate-400 font-mono shrink-0">{log.time}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===== TAB 2: USER MANAGEMENT ===== */}
      {activeTab === 'users' && (
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-6">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h2 className="text-lg font-bold text-slate-900">Quản lý Tài khoản & Phân quyền RBAC</h2>
              <p className="text-xs text-slate-500 mt-0.5">Tìm kiếm, phân quyền tài xế, chủ bãi, nhân viên trực cổng hoặc khóa tài khoản vi phạm</p>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="text"
                placeholder="Tìm theo tên, email, SĐT..."
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
                className="px-3 py-2 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-purple-500 w-64"
              />
            </div>
          </div>

          {/* Role Filters */}
          <div className="flex items-center gap-1.5 pb-2">
            {['All', 'Driver', 'Owner', 'Staff', 'Admin'].map((role) => (
              <button
                key={role}
                onClick={() => setUserRoleFilter(role)}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                  userRoleFilter === role
                    ? 'bg-purple-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                {role === 'All' ? 'Tất cả tài khoản' : role}
              </button>
            ))}
          </div>

          {/* User Table */}
          <div className="overflow-x-auto border border-slate-200 rounded-xl">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Người dùng</th>
                  <th className="px-4 py-3">Số điện thoại</th>
                  <th className="px-4 py-3">Vai trò</th>
                  <th className="px-4 py-3">Trạng thái</th>
                  <th className="px-4 py-3">Ngày tạo</th>
                  <th className="px-4 py-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredUsers.map((u) => {
                  const roleColors = {
                    Driver: 'bg-emerald-50 text-emerald-700 border-emerald-200',
                    Owner: 'bg-indigo-50 text-indigo-700 border-indigo-200',
                    Staff: 'bg-amber-50 text-amber-700 border-amber-200',
                    Admin: 'bg-purple-50 text-purple-700 border-purple-200',
                  };
                  return (
                    <tr key={u.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="px-4 py-3">
                        <div className="font-bold text-slate-900">{u.fullName}</div>
                        <div className="text-slate-400 font-mono text-[11px]">{u.email}</div>
                      </td>
                      <td className="px-4 py-3 font-mono text-slate-600">{u.phone}</td>
                      <td className="px-4 py-3">
                        <span className={`px-2 py-0.5 rounded-md text-[10px] font-bold border ${roleColors[u.role]}`}>
                          {u.role}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        {u.status === 'Active' ? (
                          <span className="inline-flex items-center gap-1 text-emerald-600 font-bold text-[11px]">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                            Đang hoạt động
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 text-red-600 font-bold text-[11px]">
                            <span className="w-1.5 h-1.5 rounded-full bg-red-500"></span>
                            Đã khóa
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-slate-500 font-mono">{u.createdAt}</td>
                      <td className="px-4 py-3 text-right">
                        <button
                          onClick={() => handleToggleUserStatus(u.id)}
                          className={`px-3 py-1 rounded-lg text-[11px] font-bold cursor-pointer transition-colors ${
                            u.status === 'Active'
                              ? 'bg-red-50 text-red-600 hover:bg-red-100'
                              : 'bg-emerald-50 text-emerald-600 hover:bg-emerald-100'
                          }`}
                        >
                          {u.status === 'Active' ? 'Khóa' : 'Mở khóa'}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ===== TAB 3: KYB APPROVALS ===== */}
      {activeTab === 'kyb' && (
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-bold text-slate-900">Thẩm định & Phê duyệt Hồ sơ Đối tác (KYB)</h2>
              <p className="text-xs text-slate-500 mt-0.5">Xác minh giấy phép kinh doanh, điều kiện PCCC và thông tin đăng ký bãi đỗ trước khi mở cổng nhận khách</p>
            </div>
            <span className="text-xs font-bold px-3 py-1 bg-amber-100 text-amber-800 rounded-full">
              {kybList.filter((k) => k.status === 'Pending').length} hồ sơ chờ duyệt
            </span>
          </div>

          <div className="space-y-4">
            {kybList.map((app) => (
              <div key={app.id} className="p-5 rounded-2xl border border-slate-200 hover:border-purple-200 transition-all bg-slate-50/40">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-bold text-slate-900 text-base">{app.lotName}</h3>
                      <span
                        className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                          app.status === 'Approved'
                            ? 'bg-emerald-100 text-emerald-800'
                            : app.status === 'Pending'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-red-100 text-red-800'
                        }`}
                      >
                        {app.status === 'Approved' ? 'ĐÃ DUYỆT' : app.status === 'Pending' ? 'CHỜ DUYỆT' : 'TỪ CHỐI'}
                      </span>
                    </div>
                    <p className="text-xs text-slate-600 font-medium">{app.companyName} • MST: <span className="font-mono">{app.taxCode}</span></p>
                    <p className="text-xs text-slate-500 flex items-center gap-1">
                      <span className="material-symbols-outlined text-[16px] text-slate-400">location_on</span>
                      {app.address}
                    </p>
                    <div className="flex items-center gap-4 text-xs text-slate-500 pt-1">
                      <span>Sức chứa: <strong className="text-slate-800">{app.capacity} ô</strong></span>
                      <span>Ngày nộp: <strong className="text-slate-800 font-mono">{app.submittedAt}</strong></span>
                    </div>
                  </div>

                  {app.status === 'Pending' ? (
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleApproveKyb(app.id)}
                        className="px-4 py-2 bg-emerald-600 text-white rounded-xl text-xs font-bold hover:bg-emerald-700 shadow-xs transition-colors flex items-center gap-1 cursor-pointer"
                      >
                        <span className="material-symbols-outlined text-[16px]">check</span>
                        Phê duyệt Bãi
                      </button>
                      <button
                        onClick={() => handleRejectKyb(app.id)}
                        className="px-3 py-2 bg-red-50 text-red-600 rounded-xl text-xs font-bold hover:bg-red-100 transition-colors flex items-center gap-1 cursor-pointer"
                      >
                        <span className="material-symbols-outlined text-[16px]">close</span>
                        Từ chối
                      </button>
                    </div>
                  ) : (
                    <div className="text-right">
                      <span className="text-xs font-semibold text-slate-400">Đã hoàn tất xử lý hồ sơ</span>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ===== TAB 4: SYSTEM PARAMETERS ===== */}
      {activeTab === 'parameters' && (
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-6">
          <div>
            <h2 className="text-lg font-bold text-slate-900">Cấu hình Tham số Vận hành Hệ thống Sàn</h2>
            <p className="text-xs text-slate-500 mt-0.5">Các tham số toàn cục áp dụng cho quy trình đặt chỗ, giữ chỗ, hoàn tiền và thu phí sàn ParkMaster</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-2">
            <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50/50 space-y-2">
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider">
                Thời gian Giữ chỗ Tối đa khi Đặt cọc (Phút)
              </label>
              <p className="text-xs text-slate-500">
                Khoảng thời gian hệ thống khóa ô đỗ tạm thời chờ tài xế thanh toán và di chuyển đến bãi.
              </p>
              <div className="flex items-center gap-2 pt-2">
                <input
                  type="number"
                  value={params.holdDurationMinutes}
                  onChange={(e) => setParams({ ...params, holdDurationMinutes: Number(e.target.value) })}
                  className="w-32 px-3 py-2 text-sm font-bold border border-slate-300 rounded-xl focus:border-purple-500 font-mono"
                />
                <span className="text-xs font-bold text-slate-600">phút (Mặc định: 15 phút)</span>
              </div>
            </div>

            <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50/50 space-y-2">
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider">
                Grace Period - Miễn phí Trễ giờ / Ra vào (Phút)
              </label>
              <p className="text-xs text-slate-500">
                Khoảng thời gian ân hạn không tính phí phạt khi xe vào trễ hoặc chuẩn bị rời khỏi bãi.
              </p>
              <div className="flex items-center gap-2 pt-2">
                <input
                  type="number"
                  value={params.gracePeriodMinutes}
                  onChange={(e) => setParams({ ...params, gracePeriodMinutes: Number(e.target.value) })}
                  className="w-32 px-3 py-2 text-sm font-bold border border-slate-300 rounded-xl focus:border-purple-500 font-mono"
                />
                <span className="text-xs font-bold text-slate-600">phút (Mặc định: 10 phút)</span>
              </div>
            </div>

            <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50/50 space-y-2">
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider">
                Tỷ lệ Hoa hồng Nền tảng Sàn ParkMaster (%)
              </label>
              <p className="text-xs text-slate-500">
                Tỷ lệ phần trăm sàn thu trên mỗi giao dịch đặt chỗ thành công của đối tác chủ bãi.
              </p>
              <div className="flex items-center gap-2 pt-2">
                <input
                  type="number"
                  value={params.platformFeePercent}
                  onChange={(e) => setParams({ ...params, platformFeePercent: Number(e.target.value) })}
                  className="w-32 px-3 py-2 text-sm font-bold border border-slate-300 rounded-xl focus:border-purple-500 font-mono"
                />
                <span className="text-xs font-bold text-slate-600">% (Mặc định: 10%)</span>
              </div>
            </div>

            <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50/50 space-y-2">
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider">
                Tỷ lệ Tiền cọc Tối thiểu (%)
              </label>
              <p className="text-xs text-slate-500">
                Số tiền đặt cọc tối thiểu tài xế cần thanh toán để xác nhận giữ ô đỗ giờ cao điểm.
              </p>
              <div className="flex items-center gap-2 pt-2">
                <input
                  type="number"
                  value={params.minDepositPercent}
                  onChange={(e) => setParams({ ...params, minDepositPercent: Number(e.target.value) })}
                  className="w-32 px-3 py-2 text-sm font-bold border border-slate-300 rounded-xl focus:border-purple-500 font-mono"
                />
                <span className="text-xs font-bold text-slate-600">% trên tổng tiền ước tính</span>
              </div>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-200 flex justify-end">
            <button
              onClick={() => showToast('Đã lưu và cập nhật cấu hình tham số hệ thống thành công!')}
              className="px-6 py-2.5 bg-purple-600 text-white rounded-xl text-xs font-bold hover:bg-purple-700 shadow-sm transition-all flex items-center gap-2 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">save</span>
              Lưu Cấu hình Tham số
            </button>
          </div>
        </div>
      )}

      {/* ===== TAB 5: FEATURE TOGGLES ===== */}
      {activeTab === 'toggles' && (
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-bold text-slate-900">Feature Toggles (Bật / Tắt Tính năng Hệ thống)</h2>
              <p className="text-xs text-slate-500 mt-0.5">Kiểm soát việc phát hành tính năng mới theo cơ chế Canary Release mà không cần triển khai lại code</p>
            </div>
          </div>

          <div className="space-y-4">
            {toggles.map((item) => (
              <div key={item.id} className="p-5 rounded-2xl border border-slate-200 hover:border-purple-200 bg-slate-50/40 transition-all flex items-center justify-between gap-4">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-bold text-slate-900 text-sm">{item.name}</h3>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        item.stage === 'Production'
                          ? 'bg-emerald-100 text-emerald-800'
                          : item.stage === 'Beta'
                          ? 'bg-blue-100 text-blue-800'
                          : 'bg-amber-100 text-amber-800'
                      }`}
                    >
                      {item.stage}
                    </span>
                  </div>
                  <p className="text-xs text-slate-500">{item.description}</p>
                  <span className="text-[11px] text-slate-400 font-medium">Nhóm: {item.category}</span>
                </div>

                <button
                  type="button"
                  onClick={() => handleSwitchToggle(item.id)}
                  className={`w-14 h-8 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-200 ease-in-out shrink-0 ${
                    item.enabled ? 'bg-purple-600 justify-end' : 'bg-slate-300 justify-start'
                  }`}
                >
                  <span className="w-6 h-6 rounded-full bg-white shadow-md block"></span>
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ===== TAB 6: SETTLEMENTS ===== */}
      {activeTab === 'settlements' && (
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-6">
          <div>
            <h2 className="text-lg font-bold text-slate-900">Đối soát Tài chính & Hoa hồng Toàn sàn</h2>
            <p className="text-xs text-slate-500 mt-0.5">Tổng hợp dòng tiền đặt cọc, thanh toán thực tế và chi trả doanh thu cho chủ bãi theo chu kỳ tuần</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50">
              <span className="text-xs text-slate-500 font-bold uppercase">Tổng thu tháng 04/2026</span>
              <p className="text-xl font-extrabold text-slate-900 mt-1">842,500,000 ₫</p>
            </div>
            <div className="p-4 rounded-xl border border-slate-200 bg-purple-50">
              <span className="text-xs text-purple-700 font-bold uppercase">Hoa hồng sàn 10%</span>
              <p className="text-xl font-extrabold text-purple-900 mt-1">84,250,000 ₫</p>
            </div>
            <div className="p-4 rounded-xl border border-slate-200 bg-emerald-50">
              <span className="text-xs text-emerald-700 font-bold uppercase">Đã chi trả đối tác</span>
              <p className="text-xl font-extrabold text-emerald-900 mt-1">758,250,000 ₫</p>
            </div>
          </div>

          <div className="overflow-x-auto border border-slate-200 rounded-xl">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
                <tr>
                  <th className="px-4 py-3">Đối tác Chủ bãi</th>
                  <th className="px-4 py-3">Kỳ đối soát</th>
                  <th className="px-4 py-3">Tổng lượt đỗ</th>
                  <th className="px-4 py-3">Tổng doanh số</th>
                  <th className="px-4 py-3">Phí sàn (10%)</th>
                  <th className="px-4 py-3">Thực nhận</th>
                  <th className="px-4 py-3 text-right">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {[
                  { name: 'Vincom Mega Mall Thảo Điền', period: 'Tuần 15 (06/04 - 12/04)', bookings: 1240, gross: '148,500,000 ₫', fee: '14,850,000 ₫', net: '133,650,000 ₫', status: 'Đã quyết toán' },
                  { name: 'Bitexco Financial Tower Parking', period: 'Tuần 15 (06/04 - 12/04)', bookings: 890, gross: '98,200,000 ₫', fee: '9,820,000 ₫', net: '88,380,000 ₫', status: 'Đã quyết toán' },
                  { name: 'Landmark 81 Central Park Yard', period: 'Tuần 15 (06/04 - 12/04)', bookings: 1650, gross: '210,000,000 ₫', fee: '21,000,000 ₫', net: '189,000,000 ₫', status: 'Đang đối soát' },
                ].map((row, idx) => (
                  <tr key={idx} className="hover:bg-slate-50 transition-colors">
                    <td className="px-4 py-3 font-bold text-slate-900">{row.name}</td>
                    <td className="px-4 py-3 text-slate-500 font-mono">{row.period}</td>
                    <td className="px-4 py-3 font-mono">{row.bookings}</td>
                    <td className="px-4 py-3 font-mono font-bold text-slate-900">{row.gross}</td>
                    <td className="px-4 py-3 font-mono text-purple-700">{row.fee}</td>
                    <td className="px-4 py-3 font-mono font-bold text-emerald-700">{row.net}</td>
                    <td className="px-4 py-3 text-right">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        row.status === 'Đã quyết toán' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                      }`}>
                        {row.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
