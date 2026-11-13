import React, { useState, useMemo } from 'react';
import {
  Search,
  Filter,
  RefreshCw,
  Eye,
  MessageSquare,
  FileQuestion,
} from 'lucide-react';
import type { ComplaintItem } from '@/types/feedback';
import { COMPLAINT_CATEGORY_LABELS } from '@/types/feedback';
import { DisputeCountdownBadge } from './DisputeCountdownBadge';

type FilterTab = 'ALL' | 'AWAITING' | 'RESPONDED' | 'PROCESSED';

interface DisputeListTableProps {
  complaints: ComplaintItem[];
  loading: boolean;
  onSelectComplaint: (complaint: ComplaintItem) => void;
  onRefresh: () => void;
}

export const DisputeListTable: React.FC<DisputeListTableProps> = ({
  complaints,
  loading,
  onSelectComplaint,
  onRefresh,
}) => {
  const [activeTab, setActiveTab] = useState<FilterTab>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('ALL');

  // Đếm số lượng theo trạng thái để hiển thị trên badge tabs
  const tabCounts = useMemo(() => {
    let awaiting = 0;
    let responded = 0;
    let processed = 0;

    complaints.forEach((c) => {
      if (c.status === 'AwaitingOwner') awaiting++;
      else if (c.status === 'OwnerResponded') responded++;
      else processed++;
    });

    return {
      all: complaints.length,
      awaiting,
      responded,
      processed,
    };
  }, [complaints]);

  // Lọc dữ liệu client-side chuẩn xác
  const filteredComplaints = useMemo(() => {
    return complaints.filter((c) => {
      // 1. Lọc theo tab trạng thái
      if (activeTab === 'AWAITING' && c.status !== 'AwaitingOwner') return false;
      if (activeTab === 'RESPONDED' && c.status !== 'OwnerResponded') return false;
      if (
        activeTab === 'PROCESSED' &&
        c.status === 'AwaitingOwner'
      ) {
        return false;
      }
      if (
        activeTab === 'PROCESSED' &&
        c.status === 'OwnerResponded'
      ) {
        return false;
      }

      // 2. Lọc theo phân loại
      if (selectedCategory !== 'ALL' && c.category !== selectedCategory) return false;

      // 3. Tìm kiếm theo mã hoặc mô tả
      if (searchQuery.trim()) {
        const q = searchQuery.trim().toLowerCase();
        const codeMatch = c.code.toLowerCase().includes(q);
        const bookingMatch = c.bookingCode.toLowerCase().includes(q);
        const descMatch = c.description.toLowerCase().includes(q);
        if (!codeMatch && !bookingMatch && !descMatch) return false;
      }

      return true;
    });
  }, [complaints, activeTab, selectedCategory, searchQuery]);

  const formatVnd = (amount?: number) => {
    if (!amount) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(amount);
  };

  const formatDate = (isoString: string) => {
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
    <div className="bg-white rounded-2xl border border-slate-200 shadow-xs overflow-hidden">
      {/* Top Header & Tabs */}
      <div className="border-b border-slate-200">
        <div className="p-4 sm:p-5 flex flex-wrap items-center justify-between gap-3 bg-slate-50/50">
          <div>
            <h3 className="font-extrabold text-slate-900 text-base tracking-tight">Danh sách tranh chấp & khiếu nại</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Chủ bãi có trách nhiệm giải trình tranh chấp trong vòng 48h để bảo vệ quyền lợi quyết toán trên hệ thống
            </p>
          </div>
          <button
            onClick={onRefresh}
            disabled={loading}
            className="px-3 py-1.5 text-slate-600 hover:text-blue-600 hover:bg-white rounded-xl border border-slate-200 shadow-xs transition flex items-center gap-1.5 text-xs font-semibold cursor-pointer"
            title="Làm mới dữ liệu"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin text-blue-600' : ''}`} />
            <span>Làm mới</span>
          </button>
        </div>

        {/* Tab Buttons */}
        <div className="flex border-t border-slate-100 overflow-x-auto px-4 bg-white gap-2 py-2">
          <button
            onClick={() => setActiveTab('ALL')}
            className={`px-3.5 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 whitespace-nowrap cursor-pointer ${
              activeTab === 'ALL'
                ? 'bg-blue-50 text-blue-700 border border-blue-200/80 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50'
            }`}
          >
            <span>Tất cả</span>
            <span
              className={`text-[11px] font-bold px-2 py-0.5 rounded-full ${
                activeTab === 'ALL' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'
              }`}
            >
              {tabCounts.all}
            </span>
          </button>

          <button
            onClick={() => setActiveTab('AWAITING')}
            className={`px-3.5 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 whitespace-nowrap cursor-pointer ${
              activeTab === 'AWAITING'
                ? 'bg-amber-50 text-amber-800 border border-amber-200/80 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50'
            }`}
          >
            <span>Chờ phản hồi (48h)</span>
            <span
              className={`text-[11px] font-bold px-2 py-0.5 rounded-full ${
                tabCounts.awaiting > 0
                  ? 'bg-amber-500 text-white'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {tabCounts.awaiting}
            </span>
          </button>

          <button
            onClick={() => setActiveTab('RESPONDED')}
            className={`px-3.5 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 whitespace-nowrap cursor-pointer ${
              activeTab === 'RESPONDED'
                ? 'bg-blue-50 text-blue-700 border border-blue-200/80 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50'
            }`}
          >
            <span>Đã phản hồi</span>
            <span
              className={`text-[11px] font-bold px-2 py-0.5 rounded-full ${
                activeTab === 'RESPONDED'
                  ? 'bg-blue-600 text-white'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {tabCounts.responded}
            </span>
          </button>

          <button
            onClick={() => setActiveTab('PROCESSED')}
            className={`px-3.5 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 whitespace-nowrap cursor-pointer ${
              activeTab === 'PROCESSED'
                ? 'bg-purple-50 text-purple-700 border border-purple-200/80 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50'
            }`}
          >
            <span>Chuyển nền tảng / Kết thúc</span>
            <span
              className={`text-[11px] font-bold px-2 py-0.5 rounded-full ${
                activeTab === 'PROCESSED'
                  ? 'bg-purple-600 text-white'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {tabCounts.processed}
            </span>
          </button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="p-4 bg-white border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
        <div className="relative flex-1 min-w-[240px]">
          <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Tìm theo mã khiếu nại (CP-...), mã đặt chỗ (BK-...) hoặc nội dung..."
            className="w-full text-xs pl-10 pr-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition font-medium placeholder-slate-400"
          />
        </div>

        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-slate-500" />
          <select
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
            className="text-xs bg-slate-50 border border-slate-200 text-slate-700 font-semibold rounded-xl px-3.5 py-2.5 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition cursor-pointer"
          >
            <option value="ALL">Tất cả phân loại</option>
            {Object.entries(COMPLAINT_CATEGORY_LABELS).map(([catKey, catLabel]) => (
              <option key={catKey} value={catKey}>
                {catLabel}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Table Content */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse text-xs">
          <thead>
            <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-bold uppercase text-[10px] tracking-wider">
              <th className="py-3.5 px-4 sm:px-6">Mã khiếu nại / Đặt chỗ</th>
              <th className="py-3.5 px-4">Phân loại</th>
              <th className="py-3.5 px-4">Yêu cầu bồi hoàn</th>
              <th className="py-3.5 px-4">Thời hạn / Trạng thái</th>
              <th className="py-3.5 px-4">Ngày gửi</th>
              <th className="py-3.5 px-4 sm:px-6 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading && complaints.length === 0 ? (
              <tr>
                <td colSpan={6} className="py-12 text-center text-slate-400">
                  <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-blue-500" />
                  <span className="font-semibold text-sm">Đang tải danh sách khiếu nại...</span>
                </td>
              </tr>
            ) : filteredComplaints.length === 0 ? (
              <tr>
                <td colSpan={6} className="py-12 text-center text-slate-400">
                  <div className="w-14 h-14 rounded-2xl bg-slate-100 flex items-center justify-center text-slate-400 mx-auto mb-3">
                    <FileQuestion className="w-7 h-7" />
                  </div>
                  <p className="font-bold text-slate-700 text-sm">Không có tranh chấp nào</p>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Chưa có khiếu nại nào phù hợp với bộ lọc hiện tại
                  </p>
                </td>
              </tr>
            ) : (
              filteredComplaints.map((c) => {
                const isAwaiting = c.status === 'AwaitingOwner';
                return (
                  <tr
                    key={c.code}
                    className="hover:bg-slate-50/70 transition-colors group cursor-pointer"
                    onClick={() => onSelectComplaint(c)}
                  >
                    <td className="py-3.5 px-4 sm:px-6">
                      <div className="font-bold text-slate-900 group-hover:text-blue-600 transition flex items-center gap-1.5">
                        <span>{c.code}</span>
                      </div>
                      <div className="text-[11px] text-slate-500 mt-0.5">
                        Booking: <span className="font-semibold text-slate-700">{c.bookingCode}</span>
                      </div>
                    </td>

                    <td className="py-3.5 px-4">
                      <span className="inline-block px-2.5 py-0.5 rounded-lg text-[11px] font-semibold bg-slate-100 text-slate-700 border border-slate-200">
                        {COMPLAINT_CATEGORY_LABELS[c.category] || c.category}
                      </span>
                    </td>

                    <td className="py-3.5 px-4">
                      <span className="font-bold text-rose-600 text-sm">
                        {formatVnd(c.requestedAmount)}
                      </span>
                    </td>

                    <td className="py-3.5 px-4">
                      <DisputeCountdownBadge
                        status={c.status}
                        dueAtUtc={c.ownerResponseDueAtUtc}
                      />
                    </td>

                    <td className="py-3.5 px-4 text-slate-500 text-[11px]">
                      {formatDate(c.createdAtUtc)}
                    </td>

                    <td className="py-3.5 px-4 sm:px-6 text-right" onClick={(e) => e.stopPropagation()}>
                      {isAwaiting ? (
                        <button
                          onClick={() => onSelectComplaint(c)}
                          className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 shadow-xs transition-all cursor-pointer"
                        >
                          <MessageSquare className="w-3.5 h-3.5" />
                          <span>Giải trình (48h)</span>
                        </button>
                      ) : (
                        <button
                          onClick={() => onSelectComplaint(c)}
                          className="inline-flex items-center gap-1 px-3 py-1.5 rounded-xl text-xs font-semibold text-slate-700 hover:text-slate-900 hover:bg-slate-100 border border-slate-200 transition-all cursor-pointer"
                        >
                          <Eye className="w-3.5 h-3.5" />
                          <span>Chi tiết</span>
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
