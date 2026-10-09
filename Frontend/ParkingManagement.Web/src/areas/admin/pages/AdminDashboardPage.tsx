import React from 'react';

export const AdminDashboardPage: React.FC = () => {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-purple-100 text-purple-800 text-xs font-semibold uppercase tracking-wider mb-2">
            ⚙️ Cổng Quản Trị Hệ Thống (Admin Portal)
          </div>
          <h1 className="text-3xl font-bold text-slate-900 tracking-tight">Hệ thống Quản trị & Điều phối</h1>
          <p className="text-slate-500 text-sm mt-1">
            Phân hệ dành cho Ban Quản trị: Quản lý người dùng, duyệt hồ sơ KYB chủ bãi, cấu hình tham số hệ thống và Feature Toggle.
          </p>
        </div>
      </div>

      {/* Admin Modules Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-8">
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="w-10 h-10 rounded-xl bg-purple-100 text-purple-700 flex items-center justify-center mb-4">
            <span className="material-symbols-outlined text-[24px]">manage_accounts</span>
          </div>
          <h3 className="font-bold text-slate-900 text-lg">Quản lý Tài khoản ([US-096])</h3>
          <p className="text-sm text-slate-500 mt-1">
            Khóa/mở tài khoản người dùng, phân quyền RBAC và phê duyệt hồ sơ đối tác chủ bãi.
          </p>
        </div>

        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="w-10 h-10 rounded-xl bg-blue-100 text-blue-700 flex items-center justify-center mb-4">
            <span className="material-symbols-outlined text-[24px]">tune</span>
          </div>
          <h3 className="font-bold text-slate-900 text-lg">Cấu hình Tham số ([US-098])</h3>
          <p className="text-sm text-slate-500 mt-1">
            Cấu hình thời gian giữ chỗ (15 phút), Grace Period miễn phí, phí nền tảng và tỷ lệ hoa hồng (10%).
          </p>
        </div>

        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="w-10 h-10 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center mb-4">
            <span className="material-symbols-outlined text-[24px]">toggle_on</span>
          </div>
          <h3 className="font-bold text-slate-900 text-lg">Feature Toggles ([US-099])</h3>
          <p className="text-sm text-slate-500 mt-1">
            Bật/tắt các tính năng thử nghiệm Phase 2: Nhận diện OCR, AI Advisor và sơ đồ 3D Map.
          </p>
        </div>
      </div>

      <div className="mt-8 p-6 bg-purple-50 rounded-2xl border border-purple-100 text-center">
        <h3 className="text-sm font-bold text-slate-800">Đã sẵn sàng cho TV8 & TV9 (AdminService & Testing)</h3>
        <p className="text-xs text-slate-500 mt-1">
          Khung router và layout đã chia rõ ràng, sẵn sàng để TV8 và TV9 tích hợp các bảng dữ liệu có phân trang và bộ lọc.
        </p>
      </div>
    </div>
  );
};
