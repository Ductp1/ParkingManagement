import React, { useEffect, useState, useMemo, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Calendar,
  Clock,
  Car,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Search,
  Filter,
  RefreshCw,
  Building2,
  Eye,
  History,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import {
  ownerBookingsApi,
  type GateBookingListItemDto,
  type ApprovalLogDto,
  type BookingDetailDto,
} from '@/api/ownerBookingsApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import { BookingDetailModal } from '../components/bookings/BookingDetailModal';

import { RejectBookingModal } from '../components/bookings/RejectBookingModal';
import { LotCancelModal } from '../components/bookings/LotCancelModal';

export const OwnerBookingsPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);
  const [loadingLots, setLoadingLots] = useState(true);

  // Tabs: 'pending' (Bãi Mức 0) | 'active' (Gate/Trong ca) | 'logs' (Lịch sử duyệt)
  const [activeTab, setActiveTab] = useState<'pending' | 'active' | 'logs'>('pending');

  // Tra cứu nhanh theo mã booking (BK-xxxx)
  const [codeSearchQuery, setCodeSearchQuery] = useState('');
  const [searchedBooking, setSearchedBooking] = useState<BookingDetailDto | null>(null);
  const [searchingCode, setSearchingCode] = useState(false);
  const [searchCodeError, setSearchCodeError] = useState<string | null>(null);

  // Dữ liệu Tab Gate (Đang hoạt động)
  const [gateBookings, setGateBookings] = useState<GateBookingListItemDto[]>([]);
  const [loadingGate, setLoadingGate] = useState(false);
  const [gateSearchText, setGateSearchText] = useState('');

  // Dữ liệu Tab Approval Logs (Lịch sử duyệt bãi mức 0)
  const [approvalLogs, setApprovalLogs] = useState<ApprovalLogDto[]>([]);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [logResultFilter, setLogResultFilter] = useState<string>('ALL');

  // Modals state
  const [selectedDetailCode, setSelectedDetailCode] = useState<string | null>(null);

  const [rejectBookingTarget, setRejectBookingTarget] = useState<{ code: string; plate: string } | null>(null);
  const [lotCancelTarget, setLotCancelTarget] = useState<{ code: string; plate: string } | null>(null);

  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // 1. Tải danh sách bãi của chủ bãi
  useEffect(() => {
    let isMounted = true;
    setLoadingLots(true);
    ownerLotsApi
      .getMyLots(ownerProfileId)
      .then((data) => {
        if (!isMounted) return;
        setLots(data || []);
        if (data && data.length > 0) {
          const lotIdParam = searchParams.get('lotId');
          const matched = data.find((l) => String(l.id) === lotIdParam);
          setSelectedLotId(matched ? matched.id : data[0].id);
        }
      })
      .catch(() => {
        if (isMounted) setLots([]);
      })
      .finally(() => {
        if (isMounted) setLoadingLots(false);
      });

    return () => {
      isMounted = false;
    };
  }, [ownerProfileId, searchParams]);

  // 2. Tải danh sách Gate bookings (Trong ca trực)
  const loadGateBookings = useCallback(async () => {
    if (!selectedLotId) return;
    setLoadingGate(true);
    try {
      const now = new Date();
      // Khung thời gian tra cứu: từ 24h trước đến 48h sau
      const fromUtc = new Date(now.getTime() - 24 * 3600 * 1000).toISOString();
      const toUtc = new Date(now.getTime() + 48 * 3600 * 1000).toISOString();

      const data = await ownerBookingsApi.getGateBookings({
        parkingLotId: selectedLotId,
        fromUtc,
        toUtc,
        search: gateSearchText.trim() || undefined,
        gracePeriodMinutes: 15,
      });
      setGateBookings(data || []);
    } catch {
      setGateBookings([]);
    } finally {
      setLoadingGate(false);
    }
  }, [selectedLotId, gateSearchText]);

  // 3. Tải Lịch sử phê duyệt (Approval Logs)
  const loadApprovalLogs = useCallback(async () => {
    if (!selectedLotId) return;
    setLoadingLogs(true);
    try {
      const data = await ownerBookingsApi.getApprovalLogs({
        parkingLotId: selectedLotId,
        result: logResultFilter === 'ALL' ? undefined : logResultFilter,
      });
      setApprovalLogs(data || []);
    } catch {
      setApprovalLogs([]);
    } finally {
      setLoadingLogs(false);
    }
  }, [selectedLotId, logResultFilter]);

  useEffect(() => {
    if (activeTab === 'active') {
      void loadGateBookings();
    } else if (activeTab === 'logs' || activeTab === 'pending') {
      void loadApprovalLogs();
    }
  }, [activeTab, selectedLotId, loadGateBookings, loadApprovalLogs]);

  // Tra cứu mã booking trực tiếp
  const handleSearchCode = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = codeSearchQuery.trim();
    if (!trimmed) return;

    setSearchingCode(true);
    setSearchCodeError(null);
    setSearchedBooking(null);

    try {
      const data = await ownerBookingsApi.getBookingByCode(trimmed);
      setSearchedBooking(data);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setSearchCodeError(
        apiErr?.response?.data?.detail ||
        apiErr?.message ||
        'Không tìm thấy mã đặt chỗ này hoặc bạn không có quyền truy cập.'
      );
    } finally {
      setSearchingCode(false);
    }
  };

  // Duyệt đặt chỗ bãi mức 0 ngay từ giao diện tìm kiếm
  const handleQuickApprove = async (code: string) => {
    try {
      await ownerBookingsApi.approveBooking(code, {
        ownerProfileId,
        reason: 'Chủ bãi duyệt yêu cầu giữ chỗ',
      });
      setToastMessage({ type: 'success', text: `Đã phê duyệt đặt chỗ ${code} thành công!` });
      // Refresh searched booking
      const updated = await ownerBookingsApi.getBookingByCode(code);
      setSearchedBooking(updated);
      void loadApprovalLogs();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string } }; message?: string };
      setToastMessage({
        type: 'error',
        text: apiErr?.response?.data?.detail || 'Không thể duyệt đặt chỗ.',
      });
    }
  };

  const selectedLot = useMemo(() => lots.find((l) => l.id === selectedLotId), [lots, selectedLotId]);

  const formatVnd = (amount?: number) => {
    if (!amount) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(amount);
  };

  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '--';
    try {
      return new Date(isoString).toLocaleString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return isoString;
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* =====================================================================
          1. HEADER TRANG & BỘ CHỌN BÃI ĐỖ XE (UI-23 / UI-24)
      ====================================================================== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            <Calendar size={13} />
            QUẢN LÝ ĐẶT CHỖ & PHÊ DUYỆT BÃI MỨC 0 (UI-23, UI-24)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Quản lý Đặt chỗ & Phê duyệt
          </h1>
          <p className="text-sm sm:text-base text-slate-500 leading-relaxed">
            Phê duyệt hoặc từ chối lượt đặt chỗ thủ công tại bãi Mức 0 (US-031), kiểm soát xe ra vào trong ca trực và xử lý bồi hoàn sự cố bãi xe (Lot Cancel / Hoàn tiền - US-041, US-042).
          </p>
        </div>

        {/* Dropdown chọn bãi xe */}
        <div className="flex items-center gap-3 self-start lg:self-end">
          <div className="flex items-center gap-2 bg-white px-3.5 py-2.5 rounded-2xl border border-slate-200 shadow-sm">
            <Building2 size={16} className="text-blue-600 shrink-0" />
            <select
              value={selectedLotId || ''}
              onChange={(e) => setSelectedLotId(Number(e.target.value))}
              disabled={loadingLots || lots.length === 0}
              className="text-xs font-bold text-slate-800 bg-transparent focus:outline-none cursor-pointer pr-2"
            >
              {lots.length === 0 ? (
                <option value="">Chưa có bãi xe</option>
              ) : (
                lots.map((lot) => (
                  <option key={lot.id} value={lot.id}>
                    {lot.name} (#{lot.id})
                  </option>
                ))
              )}
            </select>
          </div>

          <button
            type="button"
            onClick={() => {
              if (activeTab === 'active') void loadGateBookings();
              else void loadApprovalLogs();
            }}
            className="inline-flex items-center gap-1.5 px-3 py-2.5 text-xs font-semibold text-slate-700 bg-white border border-slate-200 rounded-2xl hover:bg-slate-50 shadow-sm transition-all"
            title="Làm mới dữ liệu"
          >
            <RefreshCw size={14} className={loadingGate || loadingLogs ? 'animate-spin text-blue-600' : ''} />
            <span className="hidden sm:inline">Làm mới</span>
          </button>
        </div>
      </div>

      {/* Thông báo Toast */}
      {toastMessage && (
        <div
          className={`p-4 rounded-2xl text-sm flex items-start justify-between gap-3 shadow-sm animate-fade-in ${
            toastMessage.type === 'success'
              ? 'bg-emerald-50 border border-emerald-200 text-emerald-800'
              : 'bg-rose-50 border border-rose-200 text-rose-800'
          }`}
        >
          <div className="flex items-start gap-2.5">
            {toastMessage.type === 'success' ? (
              <CheckCircle2 size={18} className="text-emerald-600 shrink-0 mt-0.5" />
            ) : (
              <AlertTriangle size={18} className="text-rose-600 shrink-0 mt-0.5" />
            )}
            <span>{toastMessage.text}</span>
          </div>
          <button
            onClick={() => setToastMessage(null)}
            className="text-xs font-bold opacity-70 hover:opacity-100"
          >
            Đóng
          </button>
        </div>
      )}

      {/* =====================================================================
          2. THANH ĐIỀU HƯỚNG TABS
      ====================================================================== */}
      <div className="flex border-b border-slate-200 gap-2">
        <button
          type="button"
          onClick={() => setActiveTab('pending')}
          className={`pb-3 px-4 text-xs font-bold transition-colors border-b-2 flex items-center gap-2 ${
            activeTab === 'pending'
              ? 'border-blue-600 text-blue-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <Clock size={15} />
          Duyệt đặt chỗ bãi Mức 0 (US-031)
          {selectedLot && (
            <span className="text-[10px] px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200 font-bold">
              Thủ công
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('active')}
          className={`pb-3 px-4 text-xs font-bold transition-colors border-b-2 flex items-center gap-2 ${
            activeTab === 'active'
              ? 'border-blue-600 text-blue-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <Car size={15} />
          Trong ca trực / Hoạt động ({gateBookings.length})
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('logs')}
          className={`pb-3 px-4 text-xs font-bold transition-colors border-b-2 flex items-center gap-2 ${
            activeTab === 'logs'
              ? 'border-blue-600 text-blue-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <History size={15} />
          Lịch sử phê duyệt (T-303)
        </button>
      </div>

      {/* =====================================================================
          3. TAB CONTENT
      ====================================================================== */}

      {/* TAB 1: DUYỆT ĐẶT CHỖ BÃI MỨC 0 (US-031) */}
      {activeTab === 'pending' && (
        <div className="space-y-6">
          {/* Card tra cứu nhanh đơn đặt chỗ theo mã BK-xxxx */}
          <div className="bg-white rounded-3xl p-6 border border-slate-200 shadow-sm space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 className="text-base font-bold text-slate-900">Tra cứu đơn đặt chỗ chờ duyệt</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Nhập mã đặt chỗ (BK-xxxx) mà khách hàng cung cấp hoặc tra cứu từ thông báo để duyệt/từ chối ngay.
                </p>
              </div>
            </div>

            <form onSubmit={handleSearchCode} className="flex gap-2 max-w-lg">
              <div className="relative flex-1">
                <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
                <input
                  type="text"
                  required
                  value={codeSearchQuery}
                  onChange={(e) => setCodeSearchQuery(e.target.value)}
                  placeholder="Ví dụ: BK-0001, BK-0002..."
                  className="w-full pl-10 pr-3 py-2.5 text-xs font-mono font-semibold rounded-2xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900 transition-all uppercase placeholder:normal-case"
                />
              </div>
              <button
                type="submit"
                disabled={searchingCode}
                className="inline-flex items-center gap-1.5 px-4 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-2xl shadow-sm transition-all disabled:opacity-50"
              >
                {searchingCode ? (
                  <RefreshCw size={14} className="animate-spin" />
                ) : (
                  <Search size={14} />
                )}
                Tra cứu
              </button>
            </form>

            {searchCodeError && (
              <div className="p-3.5 rounded-2xl bg-rose-50 border border-rose-200 text-rose-700 text-xs">
                {searchCodeError}
              </div>
            )}

            {/* Hiển thị kết quả tra cứu */}
            {searchedBooking && (
              <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200 space-y-4 animate-fade-in">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-3">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-2xl bg-blue-100 text-blue-700 flex items-center justify-center font-bold">
                      <Car size={20} />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-bold text-slate-900 text-sm">
                          {searchedBooking.code}
                        </span>
                        <span className="text-xs px-2 py-0.5 rounded-full font-bold bg-blue-100 text-blue-800">
                          {searchedBooking.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">
                        Biển số: <strong className="text-slate-800 font-mono">{searchedBooking.plateNumber}</strong> ({searchedBooking.vehicleType})
                      </p>
                    </div>
                  </div>

                  {/* Actions trên đơn */}
                  <div className="flex items-center gap-2">
                    {searchedBooking.status === 'PendingOwnerApproval' ? (
                      <>
                        <button
                          type="button"
                          onClick={() => handleQuickApprove(searchedBooking.code)}
                          className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 transition-colors shadow-xs"
                        >
                          <CheckCircle2 size={14} />
                          Duyệt đặt chỗ
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            setRejectBookingTarget({
                              code: searchedBooking.code,
                              plate: searchedBooking.plateNumber,
                            })
                          }
                          className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold text-rose-700 bg-rose-100 hover:bg-rose-200 transition-colors"
                        >
                          <XCircle size={14} />
                          Từ chối
                        </button>
                      </>
                    ) : (
                      <span className="text-xs text-slate-500 font-medium">
                        Đơn đã ở trạng thái {searchedBooking.status}
                      </span>
                    )}

                    <button
                      type="button"
                      onClick={() => setSelectedDetailCode(searchedBooking.code)}
                      className="inline-flex items-center gap-1 px-3 py-1.5 rounded-xl text-xs font-semibold text-slate-700 bg-white border border-slate-200 hover:bg-slate-100 transition-colors"
                    >
                      <Eye size={13} />
                      Chi tiết
                    </button>
                  </div>
                </div>

                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
                  <div>
                    <span className="text-slate-400 block text-[11px]">Bãi đỗ:</span>
                    <strong className="text-slate-800">{searchedBooking.parkingLotName}</strong>
                  </div>
                  <div>
                    <span className="text-slate-400 block text-[11px]">Bắt đầu:</span>
                    <strong className="text-slate-800">{formatDate(searchedBooking.startAtUtc)}</strong>
                  </div>
                  <div>
                    <span className="text-slate-400 block text-[11px]">Kết thúc:</span>
                    <strong className="text-slate-800">{formatDate(searchedBooking.endAtUtc)}</strong>
                  </div>
                  <div>
                    <span className="text-slate-400 block text-[11px]">Số tiền:</span>
                    <strong className="text-blue-600 font-bold">{formatVnd(searchedBooking.totalAmount)}</strong>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Lịch sử phê duyệt gần đây của bãi */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Lịch sử phê duyệt gần đây tại bãi</h3>
                <p className="text-xs text-slate-500">Các lượt duyệt và từ chối đã được ghi nhận vào nhật ký hệ thống</p>
              </div>
            </div>

            {loadingLogs ? (
              <div className="p-10 flex flex-col items-center justify-center gap-2 text-slate-500">
                <RefreshCw size={20} className="animate-spin text-blue-600" />
                <p className="text-xs">Đang tải lịch sử phê duyệt...</p>
              </div>
            ) : approvalLogs.length === 0 ? (
              <div className="p-10 text-center text-xs text-slate-400">
                Chưa có yêu cầu duyệt nào được ghi nhận tại bãi đỗ xe này.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                      <th className="py-3 px-6">Mã booking</th>
                      <th className="py-3 px-6">Biển số</th>
                      <th className="py-3 px-6">Kết quả</th>
                      <th className="py-3 px-6">Lý do</th>
                      <th className="py-3 px-6">Thời gian</th>
                      <th className="py-3 px-6 text-right">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {approvalLogs.slice(0, 10).map((log) => (
                      <tr key={log.id} className="hover:bg-slate-50/60 transition-colors">
                        <td className="py-3 px-6 font-mono font-bold text-slate-900">
                          {log.bookingCode}
                        </td>
                        <td className="py-3 px-6 font-mono text-slate-700">
                          {log.plateNumber}
                        </td>
                        <td className="py-3 px-6">
                          <span
                            className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                              log.result === 'Approved'
                                ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                : 'bg-rose-50 text-rose-700 border-rose-200'
                            }`}
                          >
                            {log.result === 'Approved' ? (
                              <CheckCircle2 size={12} className="text-emerald-600" />
                            ) : (
                              <XCircle size={12} className="text-rose-600" />
                            )}
                            {log.result === 'Approved' ? 'Đã duyệt' : 'Đã từ chối'}
                          </span>
                        </td>
                        <td className="py-3 px-6 text-slate-600 italic">
                          {log.reason || '--'}
                        </td>
                        <td className="py-3 px-6 text-slate-500">
                          {formatDate(log.createdAtUtc)}
                        </td>
                        <td className="py-3 px-6 text-right">
                          <button
                            type="button"
                            onClick={() => setSelectedDetailCode(log.bookingCode)}
                            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold text-blue-600 hover:text-blue-800 hover:bg-blue-50 transition-colors"
                          >
                            <Eye size={13} />
                            Xem
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 2: ĐANG HOẠT ĐỘNG / TRONG CA TRỰC (UC-20) */}
      {activeTab === 'active' && (
        <div className="space-y-4">
          {/* Bộ lọc tìm kiếm trong ca */}
          <div className="bg-white rounded-2xl p-4 border border-slate-200 shadow-sm flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
            <div className="relative flex-1 max-w-md">
              <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                value={gateSearchText}
                onChange={(e) => setGateSearchText(e.target.value)}
                placeholder="Tìm theo biển số hoặc mã booking..."
                className="w-full pl-10 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900 placeholder:text-slate-400 transition-all"
              />
            </div>
          </div>

          {/* Bảng danh sách xe trong ca */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            {loadingGate ? (
              <div className="p-16 flex flex-col items-center justify-center gap-3 text-slate-500">
                <RefreshCw size={24} className="animate-spin text-blue-600" />
                <p className="text-xs font-medium">Đang tải danh sách đặt chỗ trong ca trực...</p>
              </div>
            ) : gateBookings.length === 0 ? (
              <div className="p-16 text-center text-xs text-slate-400">
                Không có lượt đặt chỗ nào trong khung giờ hiện tại của bãi đỗ xe này.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                      <th className="py-3.5 px-6">Mã đặt chỗ</th>
                      <th className="py-3.5 px-6">Biển số & Phương tiện</th>
                      <th className="py-3.5 px-6">Vị trí ô</th>
                      <th className="py-3.5 px-6">Khung giờ đỗ</th>
                      <th className="py-3.5 px-6">Trạng thái</th>
                      <th className="py-3.5 px-6">Tổng cước</th>
                      <th className="py-3.5 px-6 text-right">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {gateBookings.map((b) => (
                      <tr key={b.code} className="hover:bg-slate-50/60 transition-colors">
                        <td className="py-4 px-6 font-mono font-bold text-slate-900">
                          {b.code}
                        </td>
                        <td className="py-4 px-6">
                          <div className="flex items-center gap-2">
                            <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded-md border border-slate-200">
                              {b.plateNumber}
                            </span>
                            <span className="text-[11px] text-slate-400">{b.vehicleType}</span>
                          </div>
                        </td>
                        <td className="py-4 px-6 text-slate-700 font-medium">
                          {b.slotCode || 'Linh hoạt'}
                        </td>
                        <td className="py-4 px-6 text-slate-600">
                          <div>
                            <span>{formatDate(b.startAtUtc)}</span>
                            <span className="text-slate-400 mx-1">→</span>
                            <span>{formatDate(b.endAtUtc)}</span>
                          </div>
                          {b.isOverdue && (
                            <span className="inline-flex items-center gap-1 text-[10px] font-bold text-rose-600 mt-0.5">
                              <Clock size={11} /> Quá giờ đến (&gt;15 phút)
                            </span>
                          )}
                        </td>
                        <td className="py-4 px-6">
                          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-blue-50 text-blue-700 border border-blue-200">
                            {b.status}
                          </span>
                        </td>
                        <td className="py-4 px-6 font-bold text-blue-600">
                          {formatVnd(b.totalAmount)}
                        </td>
                        <td className="py-4 px-6 text-right">
                          <div className="inline-flex items-center gap-1.5 justify-end">
                            <button
                              type="button"
                              onClick={() => setSelectedDetailCode(b.code)}
                              className="p-1.5 text-blue-600 hover:text-blue-800 hover:bg-blue-50 rounded-lg transition-colors"
                              title="Xem chi tiết"
                            >
                              <Eye size={15} />
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                setLotCancelTarget({ code: b.code, plate: b.plateNumber })
                              }
                              className="p-1.5 text-amber-600 hover:text-amber-800 hover:bg-amber-50 rounded-lg transition-colors"
                              title="Hủy sự cố bãi (Lot Cancel)"
                            >
                              <AlertTriangle size={15} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 3: TOÀN BỘ LỊCH SỬ PHÊ DUYỆT (T-303) */}
      {activeTab === 'logs' && (
        <div className="space-y-4">
          <div className="bg-white rounded-2xl p-4 border border-slate-200 shadow-sm flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Filter size={14} className="text-slate-400" />
              <select
                value={logResultFilter}
                onChange={(e) => setLogResultFilter(e.target.value)}
                className="text-xs py-2 px-2.5 rounded-xl border border-slate-200 bg-white text-slate-700 focus:outline-none"
              >
                <option value="ALL">Tất cả kết quả</option>
                <option value="Approved">Đã duyệt (Approved)</option>
                <option value="Rejected">Đã từ chối (Rejected)</option>
              </select>
            </div>
            <span className="text-xs text-slate-500">
              Tổng số bản ghi: <strong className="text-slate-900 font-bold">{approvalLogs.length}</strong>
            </span>
          </div>

          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            {loadingLogs ? (
              <div className="p-16 flex flex-col items-center justify-center gap-3 text-slate-500">
                <RefreshCw size={24} className="animate-spin text-blue-600" />
                <p className="text-xs font-medium">Đang tải nhật ký duyệt đơn...</p>
              </div>
            ) : approvalLogs.length === 0 ? (
              <div className="p-16 text-center text-xs text-slate-400">
                Không tìm thấy bản ghi phê duyệt nào khớp với bộ lọc.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                      <th className="py-3.5 px-6">Mã booking</th>
                      <th className="py-3.5 px-6">Biển số</th>
                      <th className="py-3.5 px-6">Kết quả</th>
                      <th className="py-3.5 px-6">Người duyệt</th>
                      <th className="py-3.5 px-6">Lý do</th>
                      <th className="py-3.5 px-6">Thời gian xử lý</th>
                      <th className="py-3.5 px-6 text-right">Chi tiết</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {approvalLogs.map((log) => (
                      <tr key={log.id} className="hover:bg-slate-50/60 transition-colors">
                        <td className="py-4 px-6 font-mono font-bold text-slate-900">
                          {log.bookingCode}
                        </td>
                        <td className="py-4 px-6 font-mono text-slate-700">
                          {log.plateNumber}
                        </td>
                        <td className="py-4 px-6">
                          <span
                            className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                              log.result === 'Approved'
                                ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                : 'bg-rose-50 text-rose-700 border-rose-200'
                            }`}
                          >
                            {log.result === 'Approved' ? (
                              <CheckCircle2 size={12} className="text-emerald-600" />
                            ) : (
                              <XCircle size={12} className="text-rose-600" />
                            )}
                            {log.result === 'Approved' ? 'Đã duyệt' : 'Đã từ chối'}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-slate-600">
                          {log.reviewedByUserId ? `Tài khoản #${log.reviewedByUserId}` : 'Hệ thống / Chủ bãi'}
                        </td>
                        <td className="py-4 px-6 text-slate-600 italic">
                          {log.reason || '--'}
                        </td>
                        <td className="py-4 px-6 text-slate-500">
                          {formatDate(log.createdAtUtc)}
                        </td>
                        <td className="py-4 px-6 text-right">
                          <button
                            type="button"
                            onClick={() => setSelectedDetailCode(log.bookingCode)}
                            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold text-blue-600 hover:text-blue-800 hover:bg-blue-50 transition-colors"
                          >
                            <Eye size={13} />
                            Xem
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* =====================================================================
          4. MODALS (DETAIL, REFUND, REJECT, LOT CANCEL)
      ====================================================================== */}
      <BookingDetailModal
        isOpen={selectedDetailCode !== null}
        onClose={() => setSelectedDetailCode(null)}
        bookingCode={selectedDetailCode}
        ownerProfileId={ownerProfileId}
        onStatusChanged={() => {
          if (activeTab === 'active') void loadGateBookings();
          else void loadApprovalLogs();
        }}
      />

      {rejectBookingTarget && (
        <RejectBookingModal
          isOpen={true}
          onClose={() => setRejectBookingTarget(null)}
          bookingCode={rejectBookingTarget.code}
          plateNumber={rejectBookingTarget.plate}
          ownerProfileId={ownerProfileId}
          onSuccess={() => {
            setToastMessage({
              type: 'success',
              text: `Đã từ chối đơn đặt chỗ ${rejectBookingTarget.code}.`,
            });
            void loadApprovalLogs();
          }}
        />
      )}

      {lotCancelTarget && (
        <LotCancelModal
          isOpen={true}
          onClose={() => setLotCancelTarget(null)}
          bookingCode={lotCancelTarget.code}
          plateNumber={lotCancelTarget.plate}
          ownerProfileId={ownerProfileId}
          onSuccess={() => {
            setToastMessage({
              type: 'success',
              text: `Đã hủy đặt chỗ ${lotCancelTarget.code} do sự cố bãi và kích hoạt hoàn 100% cho khách!`,
            });
            if (activeTab === 'active') void loadGateBookings();
          }}
        />
      )}
    </div>
  );
};
