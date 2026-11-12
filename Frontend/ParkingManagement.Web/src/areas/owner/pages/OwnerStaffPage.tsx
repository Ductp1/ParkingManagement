import React, { useEffect, useState, useMemo, useCallback } from 'react';
import {
  Users,
  UserPlus,
  Building2,
  CheckCircle2,
  Clock,
  Search,
  Filter,
  RefreshCw,
  Trash2,
  UserCheck,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Info,
} from 'lucide-react';
import {
  ownerStaffApi,
  type StaffItemDto,
  type OwnerLotOptionDto,
  type CreateStaffRequest,
} from '@/api/ownerStaffApi';
import { AddStaffModal } from '../components/staff/AddStaffModal';
import { RevokeStaffModal } from '../components/staff/RevokeStaffModal';

const STATUS_CONFIG: Record<
  string,
  { label: string; badgeClass: string; dotClass: string }
> = {
  Active: {
    label: 'Đang hoạt động',
    badgeClass: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    dotClass: 'bg-emerald-500',
  },
  PendingVerification: {
    label: 'Chờ kích hoạt',
    badgeClass: 'bg-amber-50 text-amber-700 border-amber-200',
    dotClass: 'bg-amber-500 animate-pulse',
  },
  Locked: {
    label: 'Đã khóa',
    badgeClass: 'bg-rose-50 text-rose-700 border-rose-200',
    dotClass: 'bg-rose-500',
  },
  Banned: {
    label: 'Đã cấm',
    badgeClass: 'bg-slate-100 text-slate-700 border-slate-300',
    dotClass: 'bg-slate-500',
  },
};

export const OwnerStaffPage: React.FC = () => {
  const [items, setItems] = useState<StaffItemDto[]>([]);
  const [lots, setLots] = useState<OwnerLotOptionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedLotFilter, setSelectedLotFilter] = useState<string>('ALL');
  const [selectedStatusFilter, setSelectedStatusFilter] = useState<string>('ALL');

  // Modals
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [revokingStaff, setRevokingStaff] = useState<StaffItemDto | null>(null);

  // Load lots list
  const loadLots = useCallback(async () => {
    try {
      const data = await ownerStaffApi.getOwnerLots();
      setLots(data || []);
    } catch {
      setLots([]);
    }
  }, []);

  // Load staff list
  const loadStaff = useCallback(async (targetPage = page, isManual = false) => {
    if (isManual) setRefreshing(true);
    else setLoading(true);
    setError(null);

    try {
      const data = await ownerStaffApi.getStaffList(targetPage);
      setItems(data || []);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.message ||
        'Không thể tải danh sách nhân viên. Vui lòng kiểm tra lại kết nối.'
      );
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [page]);

  useEffect(() => {
    void loadLots();
  }, [loadLots]);

  useEffect(() => {
    void loadStaff(page);
  }, [page, loadStaff]);

  const lotName = useCallback(
    (id: number) => {
      const lot = lots.find((item) => item.id === id);
      return lot?.name ? `${lot.name} (#${id})` : `Bãi #${id}`;
    },
    [lots]
  );

  // KPI Metrics
  const stats = useMemo(() => {
    const total = items.length;
    const active = items.filter((i) => i.status === 'Active').length;
    const pending = items.filter((i) => i.status === 'PendingVerification').length;
    const uniqueLots = new Set(items.map((i) => i.parkingLotId)).size;
    return { total, active, pending, uniqueLots };
  }, [items]);

  // Filtered staff list
  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      const matchesSearch =
        searchQuery === '' ||
        item.fullName.toLowerCase().includes(searchQuery.toLowerCase()) ||
        String(item.userId).includes(searchQuery);

      const matchesLot =
        selectedLotFilter === 'ALL' || String(item.parkingLotId) === selectedLotFilter;

      const matchesStatus =
        selectedStatusFilter === 'ALL' || item.status === selectedStatusFilter;

      return matchesSearch && matchesLot && matchesStatus;
    });
  }, [items, searchQuery, selectedLotFilter, selectedStatusFilter]);

  // Handle Create Staff
  const handleCreateStaff = async (data: CreateStaffRequest) => {
    const created = await ownerStaffApi.createStaff(data);
    setSuccessMessage(
      `Đã phân công nhân viên ${created.fullName} tại ${lotName(created.parkingLotId)} thành công! Tài khoản và mật khẩu tạm thời đã được gửi tới email.`
    );
    setPage(1);
    await loadStaff(1);
  };

  // Handle Revoke Staff
  const handleRevokeStaff = async (staffId: number) => {
    await ownerStaffApi.revokeStaff(staffId);
    setSuccessMessage('Đã gỡ quyền nhân viên tại bãi và thu hồi phiên làm việc tức thì.');
    await loadStaff(page);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* =====================================================================
          1. HEADER TRANG & NÚT THÊM NHÂN VIÊN (UI-28 / US-075)
      ====================================================================== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            <Users size={13} />
            QUẢN LÝ NHÂN VIÊN TRỰC CỔNG (UI-28)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Nhân viên & Phân công trạm barie
          </h1>
          <p className="text-sm sm:text-base text-slate-500 leading-relaxed">
            Thêm người trực cổng barie cho từng bãi đỗ xe. Nhân viên mới nhận tài khoản và mật khẩu tạm thời qua email; gán quyền và thu hồi phiên làm việc tức thì.
          </p>
        </div>

        <div className="flex items-center gap-3 self-start lg:self-end">
          <button
            type="button"
            onClick={() => void loadStaff(page, true)}
            disabled={refreshing || loading}
            className="inline-flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold text-slate-700 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 active:bg-slate-100 shadow-sm transition-all"
            title="Tải lại danh sách"
          >
            <RefreshCw size={15} className={refreshing ? 'animate-spin text-blue-600' : ''} />
            <span className="hidden sm:inline">Làm mới</span>
          </button>
          <button
            type="button"
            onClick={() => setIsAddModalOpen(true)}
            className="inline-flex items-center gap-2 px-4 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-xl shadow-md shadow-blue-500/10 transition-all hover:-translate-y-0.5"
          >
            <UserPlus size={16} />
            Thêm nhân viên
          </button>
        </div>
      </div>

      {/* Thông báo thành công / Lỗi */}
      {successMessage && (
        <div className="p-4 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-start justify-between gap-3 shadow-sm animate-fade-in">
          <div className="flex items-start gap-2.5">
            <CheckCircle2 size={18} className="text-emerald-600 shrink-0 mt-0.5" />
            <span>{successMessage}</span>
          </div>
          <button
            onClick={() => setSuccessMessage(null)}
            className="text-emerald-500 hover:text-emerald-700 text-xs font-bold"
          >
            Đóng
          </button>
        </div>
      )}

      {error && (
        <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex items-start justify-between gap-3 shadow-sm animate-fade-in">
          <div className="flex items-start gap-2.5">
            <AlertCircle size={18} className="text-rose-600 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
          <button
            onClick={() => setError(null)}
            className="text-rose-500 hover:text-rose-700 text-xs font-bold"
          >
            Đóng
          </button>
        </div>
      )}

      {/* Cảnh báo chưa có bãi xe */}
      {lots.length === 0 && !loading && (
        <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-800 text-sm flex items-start gap-3">
          <Info size={18} className="text-amber-600 shrink-0 mt-0.5" />
          <div>
            <strong>Lưu ý:</strong> Bạn chưa có bãi đỗ xe nào trên hệ thống hoặc hồ sơ chưa hoàn tất. Vui lòng tạo bãi đỗ tại phần <span className="underline font-semibold">Bãi đỗ & Sơ đồ ô</span> trước khi phân công nhân viên.
          </div>
        </div>
      )}

      {/* =====================================================================
          2. KPI THỐNG KÊ NHÂN SỰ (4 CARDS)
      ====================================================================== */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Card 1: Tổng nhân viên */}
        <div className="bg-white rounded-2xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Tổng nhân sự</p>
            <p className="text-2xl font-black text-slate-900 mt-1">{stats.total}</p>
            <p className="text-[11px] text-slate-400 mt-0.5">Phân công tại các trạm</p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 border border-blue-100 flex items-center justify-center">
            <Users size={22} />
          </div>
        </div>

        {/* Card 2: Đang hoạt động */}
        <div className="bg-white rounded-2xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Đang hoạt động</p>
            <p className="text-2xl font-black text-emerald-600 mt-1">{stats.active}</p>
            <p className="text-[11px] text-slate-400 mt-0.5">Đã xác minh & cấp quyền</p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 border border-emerald-100 flex items-center justify-center">
            <UserCheck size={22} />
          </div>
        </div>

        {/* Card 3: Chờ kích hoạt */}
        <div className="bg-white rounded-2xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Chờ kích hoạt</p>
            <p className="text-2xl font-black text-amber-600 mt-1">{stats.pending}</p>
            <p className="text-[11px] text-slate-400 mt-0.5">Chưa đổi mật khẩu tạm</p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-amber-50 text-amber-600 border border-amber-100 flex items-center justify-center">
            <Clock size={22} />
          </div>
        </div>

        {/* Card 4: Số bãi có nhân viên */}
        <div className="bg-white rounded-2xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Bãi đã phân công</p>
            <p className="text-2xl font-black text-indigo-600 mt-1">{stats.uniqueLots}</p>
            <p className="text-[11px] text-slate-400 mt-0.5">Trên tổng {lots.length} bãi xe</p>
          </div>
          <div className="w-12 h-12 rounded-2xl bg-indigo-50 text-indigo-600 border border-indigo-100 flex items-center justify-center">
            <Building2 size={22} />
          </div>
        </div>
      </div>

      {/* =====================================================================
          3. BỘ LỌC & TÌM KIẾM
      ====================================================================== */}
      <div className="bg-white rounded-2xl p-4 border border-slate-200 shadow-sm flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
        <div className="relative flex-1 max-w-md">
          <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Tìm theo tên nhân viên hoặc mã ID..."
            className="w-full pl-10 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900 placeholder:text-slate-400 transition-all"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          {/* Lọc theo bãi xe */}
          <div className="flex items-center gap-1.5">
            <Building2 size={14} className="text-slate-400 shrink-0" />
            <select
              value={selectedLotFilter}
              onChange={(e) => setSelectedLotFilter(e.target.value)}
              className="text-xs py-2 px-2.5 rounded-xl border border-slate-200 bg-white text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600"
            >
              <option value="ALL">Tất cả bãi xe</option>
              {lots.map((lot) => (
                <option key={lot.id} value={String(lot.id)}>
                  {lot.name || `Bãi #${lot.id}`}
                </option>
              ))}
            </select>
          </div>

          {/* Lọc theo trạng thái */}
          <div className="flex items-center gap-1.5">
            <Filter size={14} className="text-slate-400 shrink-0" />
            <select
              value={selectedStatusFilter}
              onChange={(e) => setSelectedStatusFilter(e.target.value)}
              className="text-xs py-2 px-2.5 rounded-xl border border-slate-200 bg-white text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600"
            >
              <option value="ALL">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động</option>
              <option value="PendingVerification">Chờ kích hoạt</option>
              <option value="Locked">Đã khóa</option>
              <option value="Banned">Đã cấm</option>
            </select>
          </div>
        </div>
      </div>

      {/* =====================================================================
          4. DANH SÁCH NHÂN VIÊN (TABLE / CARDS)
      ====================================================================== */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-16 flex flex-col items-center justify-center gap-3 text-slate-500">
            <RefreshCw size={24} className="animate-spin text-blue-600" />
            <p className="text-xs font-medium">Đang tải danh sách nhân viên trực cổng...</p>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="p-16 flex flex-col items-center justify-center gap-3 text-center">
            <div className="w-14 h-14 rounded-2xl bg-slate-50 border border-slate-200 flex items-center justify-center text-slate-400">
              <Users size={28} />
            </div>
            <h3 className="text-base font-bold text-slate-900 mt-1">Chưa có nhân viên nào</h3>
            <p className="text-xs text-slate-500 max-w-sm">
              {searchQuery || selectedLotFilter !== 'ALL' || selectedStatusFilter !== 'ALL'
                ? 'Không tìm thấy nhân viên nào khớp với bộ lọc hiện tại. Thử xóa bớt tiêu chí lọc.'
                : 'Bắt đầu phân công người trực cổng barie bằng cách nhấn nút "Thêm nhân viên" bên dưới.'}
            </p>
            <button
              type="button"
              onClick={() => setIsAddModalOpen(true)}
              className="mt-2 inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-xl shadow-sm transition-all"
            >
              <UserPlus size={15} />
              Thêm nhân viên mới
            </button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                  <th className="py-3.5 px-6">Nhân viên</th>
                  <th className="py-3.5 px-6">Bãi đỗ phân công</th>
                  <th className="py-3.5 px-6">Trạng thái tài khoản</th>
                  <th className="py-3.5 px-6">Quyền hạn trạm</th>
                  <th className="py-3.5 px-6 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredItems.map((item) => {
                  const statusInfo = STATUS_CONFIG[item.status] || {
                    label: item.status,
                    badgeClass: 'bg-slate-100 text-slate-700 border-slate-200',
                    dotClass: 'bg-slate-400',
                  };
                  const initialLetter = item.fullName.trim().charAt(0).toUpperCase() || 'U';

                  return (
                    <tr
                      key={item.id}
                      className="hover:bg-slate-50/60 transition-colors group"
                    >
                      {/* Cột Nhân viên */}
                      <td className="py-4 px-6">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-full bg-blue-50 text-blue-700 border border-blue-200 flex items-center justify-center font-bold text-sm shrink-0 shadow-xs">
                            {initialLetter}
                          </div>
                          <div>
                            <p className="font-bold text-slate-900 text-sm group-hover:text-blue-600 transition-colors">
                              {item.fullName}
                            </p>
                            <p className="text-[11px] text-slate-400 mt-0.5 font-mono">
                              Mã tài khoản: #{item.userId}
                            </p>
                          </div>
                        </div>
                      </td>

                      {/* Cột Bãi đỗ phân công */}
                      <td className="py-4 px-6">
                        <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-slate-100 border border-slate-200 text-slate-700 font-medium text-xs">
                          <Building2 size={13} className="text-slate-500 shrink-0" />
                          <span>{lotName(item.parkingLotId)}</span>
                        </div>
                      </td>

                      {/* Cột Trạng thái */}
                      <td className="py-4 px-6">
                        <span
                          className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border ${statusInfo.badgeClass}`}
                        >
                          <span className={`w-1.5 h-1.5 rounded-full ${statusInfo.dotClass}`} />
                          {statusInfo.label}
                        </span>
                      </td>

                      {/* Cột Quyền hạn */}
                      <td className="py-4 px-6">
                        <span className="text-slate-600 font-medium text-xs">
                          Trực cổng & Kiểm soát Barie
                        </span>
                      </td>

                      {/* Cột Thao tác */}
                      <td className="py-4 px-6 text-right">
                        <button
                          type="button"
                          onClick={() => setRevokingStaff(item)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-600 hover:text-rose-700 hover:bg-rose-50 border border-transparent hover:border-rose-200 transition-all"
                        >
                          <Trash2 size={14} />
                          Gỡ quyền
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Phân trang */}
        {(page > 1 || items.length >= 20) && (
          <div className="flex items-center justify-between px-6 py-4 border-t border-slate-100 bg-slate-50/50">
            <span className="text-xs text-slate-500">
              Trang <strong className="text-slate-900 font-bold">{page}</strong>
            </span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page <= 1 || loading}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 transition-colors"
              >
                <ChevronLeft size={14} />
                Trước
              </button>
              <button
                type="button"
                disabled={items.length < 20 || loading}
                onClick={() => setPage((p) => p + 1)}
                className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 transition-colors"
              >
                Sau
                <ChevronRight size={14} />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* =====================================================================
          5. MODALS
      ====================================================================== */}
      <AddStaffModal
        isOpen={isAddModalOpen}
        onClose={() => setIsAddModalOpen(false)}
        lots={lots}
        onSubmit={handleCreateStaff}
      />

      <RevokeStaffModal
        isOpen={revokingStaff !== null}
        onClose={() => setRevokingStaff(null)}
        staff={revokingStaff}
        lotName={revokingStaff ? lotName(revokingStaff.parkingLotId) : ''}
        onConfirm={handleRevokeStaff}
      />
    </div>
  );
};
