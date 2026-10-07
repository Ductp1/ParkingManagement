import React from 'react';

export const OwnerHomePage: React.FC = () => {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🏢 Cổng Chủ Bãi (Owner Portal)
          </div>
          <h1 className="text-3xl font-bold text-slate-900 tracking-tight">Quản lý Bãi đỗ xe</h1>
          <p className="text-slate-500 text-sm mt-1">
            Phân hệ dành cho Chủ bãi đối tác: Khai báo bãi đỗ, phân tầng sơ đồ (Zone/Floor/Slot), nộp hồ sơ KYB và cấu hình bảng giá.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button className="px-4 py-2 bg-blue-600 text-white rounded-xl text-sm font-semibold hover:bg-blue-700 shadow-xs transition-colors">
            + Đăng ký bãi mới
          </button>
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-8">
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Trạng thái KYB</span>
          <div className="text-2xl font-bold text-emerald-600 mt-2">Đã phê duyệt (Active)</div>
          <p className="text-xs text-slate-500 mt-1">Đầy đủ hồ sơ pháp lý & PCCC</p>
        </div>
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Tổng số bãi đang hoạt động</span>
          <div className="text-2xl font-bold text-slate-900 mt-2">02 Bãi</div>
          <p className="text-xs text-slate-500 mt-1">Landmark 81 Central & Bitexco Garage</p>
        </div>
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Công suất phục vụ hôm nay</span>
          <div className="text-2xl font-bold text-blue-600 mt-2">84% Lấp đầy</div>
          <p className="text-xs text-slate-500 mt-1">168 / 200 slots đang có xe</p>
        </div>
      </div>

      <div className="mt-8 p-8 bg-blue-50/50 rounded-2xl border border-blue-100 text-center">
        <span className="material-symbols-outlined text-[48px] text-blue-500 mb-2">dashboard_customize</span>
        <h3 className="text-lg font-bold text-slate-800">Sẵn sàng tích hợp cho TV5 (ParkingService)</h3>
        <p className="text-sm text-slate-600 max-w-xl mx-auto mt-1">
          Khung router và layout đã cấu hình sẵn sàng. TV5 có thể trực tiếp triển khai các form KYB ([US-054]), sơ đồ phân tầng ([US-058]) và bảng giá ([US-070]) tại thư mục này.
        </p>
      </div>
    </div>
  );
};
