import React, { useEffect, useState, useMemo, useCallback } from 'react';
import {
  TrendingUp,
  DollarSign,
  Download,
  Calendar,
  Building2,
  RefreshCw,
  Scale,
  CheckCircle2,
  AlertCircle,
  Eye,
  CreditCard,
  QrCode,
  Banknote,
  ArrowDownRight,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import {
  ownerRevenueApi,
  type OwnerRevenueSummaryDto,
  type OwnerRevenueSeriesDto,
  type OwnerRevenueTransactionDto,
  type OwnerSettlementDto,
} from '@/api/ownerRevenueApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import { SettlementDetailModal } from '../components/revenue/SettlementDetailModal';

export const OwnerRevenuePage: React.FC = () => {
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | 'ALL'>('ALL');
  const [loadingLots, setLoadingLots] = useState(true);

  // Bộ lọc ngày
  const [dateRangePreset, setDateRangePreset] = useState<'30days' | '7days' | 'thisMonth' | 'lastMonth'>('30days');

  // Tabs: 'overview' | 'settlements'
  const [activeTab, setActiveTab] = useState<'overview' | 'settlements'>('overview');

  // Dữ liệu Báo cáo Doanh thu (US-078)
  const [summary, setSummary] = useState<OwnerRevenueSummaryDto | null>(null);
  const [series, setSeries] = useState<OwnerRevenueSeriesDto | null>(null);
  const [transactions, setTransactions] = useState<OwnerRevenueTransactionDto[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transPage, setTransPage] = useState(1);
  const [loadingRevenue, setLoadingRevenue] = useState(false);
  const [exportingCsv, setExportingCsv] = useState(false);

  // Dữ liệu Kỳ quyết toán (US-080)
  const [settlements, setSettlements] = useState<OwnerSettlementDto[]>([]);
  const [loadingSettlements, setLoadingSettlements] = useState(false);
  const [selectedSettlementId, setSelectedSettlementId] = useState<number | null>(null);

  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Tính ngày theo preset
  const dateParams = useMemo(() => {
    const now = new Date();
    const to = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1); // tomorrow 00:00 ICT
    let from: Date;

    if (dateRangePreset === '7days') {
      from = new Date(now.getTime() - 7 * 24 * 3600 * 1000);
    } else if (dateRangePreset === 'thisMonth') {
      from = new Date(now.getFullYear(), now.getMonth(), 1);
    } else if (dateRangePreset === 'lastMonth') {
      from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    } else {
      // 30 days
      from = new Date(now.getTime() - 30 * 24 * 3600 * 1000);
    }

    const toStr = to.toISOString().split('T')[0];
    const fromStr = from.toISOString().split('T')[0];
    return { from: fromStr, to: toStr };
  }, [dateRangePreset]);

  // 1. Tải danh sách bãi
  useEffect(() => {
    let isMounted = true;
    setLoadingLots(true);
    ownerLotsApi
      .getMyLots(ownerProfileId)
      .then((data) => {
        if (!isMounted) return;
        setLots(data || []);
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
  }, [ownerProfileId]);

  // 2. Tải Báo cáo Doanh thu (Summary, Series, Transactions)
  const loadRevenueData = useCallback(async () => {
    setLoadingRevenue(true);
    const filter = {
      from: dateParams.from,
      to: dateParams.to,
      parkingLotId: selectedLotId === 'ALL' ? undefined : selectedLotId,
    };

    try {
      const [sumData, seriesData, transData] = await Promise.all([
        ownerRevenueApi.getRevenueSummary(filter),
        ownerRevenueApi.getRevenueSeries({ ...filter, granularity: 'Day' }),
        ownerRevenueApi.getRevenueTransactions({ ...filter, page: transPage, pageSize: 15 }),
      ]);

      setSummary(sumData);
      setSeries(seriesData);
      setTransactions(transData.items || []);
      setTransactionTotal(transData.totalCount || 0);
    } catch {
      // Graceful fallback
    } finally {
      setLoadingRevenue(false);
    }
  }, [dateParams, selectedLotId, transPage]);

  // 3. Tải Danh sách Kỳ quyết toán (US-080)
  const loadSettlementsData = useCallback(async () => {
    setLoadingSettlements(true);
    try {
      const res = await ownerRevenueApi.getSettlements({ page: 1, pageSize: 20 });
      setSettlements(res.items || []);
    } catch {
      setSettlements([]);
    } finally {
      setLoadingSettlements(false);
    }
  }, []);

  useEffect(() => {
    if (activeTab === 'overview') {
      void loadRevenueData();
    } else {
      void loadSettlementsData();
    }
  }, [activeTab, loadRevenueData, loadSettlementsData]);

  // Xuất file CSV doanh thu
  const handleExportCsv = async () => {
    setExportingCsv(true);
    try {
      const filter = {
        from: dateParams.from,
        to: dateParams.to,
        parkingLotId: selectedLotId === 'ALL' ? undefined : selectedLotId,
      };
      const blob = await ownerRevenueApi.exportRevenueCsv(filter);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `Owner_Revenue_${dateParams.from}_${dateParams.to}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      setToastMessage({ type: 'success', text: 'Đã xuất file báo cáo doanh thu CSV thành công!' });
    } catch {
      setToastMessage({ type: 'error', text: 'Không thể xuất file CSV doanh thu.' });
    } finally {
      setExportingCsv(false);
    }
  };

  const formatVnd = (val?: number) => {
    if (val === undefined || val === null) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(val);
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

  // Icon mapping cho Payment Method
  const getMethodIcon = (method: string) => {
    switch ((method || '').toLowerCase()) {
      case 'vnpay':
        return <CreditCard size={14} className="text-blue-600" />;
      case 'vietqr':
        return <QrCode size={14} className="text-emerald-600" />;
      case 'cash':
        return <Banknote size={14} className="text-amber-600" />;
      default:
        return <DollarSign size={14} className="text-slate-600" />;
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* =====================================================================
          1. HEADER TRANG & BỘ LỌC (UI-25 / US-078)
      ====================================================================== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            <TrendingUp size={13} />
            BÁO CÁO DOANH THU & KỲ QUYẾT TOÁN (UI-25)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Báo cáo Doanh thu & Quyết toán
          </h1>
          <p className="text-sm sm:text-base text-slate-500 leading-relaxed">
            Theo dõi dòng tiền theo thời gian thực (US-078), phân rã doanh thu theo phương thức thanh toán, hoa hồng nền tảng (US-079) và lịch sử các kỳ quyết toán đã chốt (US-080).
          </p>
        </div>

        {/* Bộ lọc bãi và nút xuất CSV */}
        <div className="flex flex-wrap items-center gap-3 self-start lg:self-end">
          <div className="flex items-center gap-2 bg-white px-3.5 py-2.5 rounded-2xl border border-slate-200 shadow-sm">
            <Building2 size={16} className="text-blue-600 shrink-0" />
            <select
              value={selectedLotId}
              onChange={(e) => setSelectedLotId(e.target.value === 'ALL' ? 'ALL' : Number(e.target.value))}
              disabled={loadingLots}
              className="text-xs font-bold text-slate-800 bg-transparent focus:outline-none cursor-pointer pr-2"
            >
              <option value="ALL">Tất cả bãi đỗ xe</option>
              {lots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name} (#{lot.id})
                </option>
              ))}
            </select>
          </div>

          <button
            type="button"
            onClick={handleExportCsv}
            disabled={exportingCsv || loadingRevenue}
            className="inline-flex items-center gap-1.5 px-4 py-2.5 text-xs font-bold text-slate-700 bg-white border border-slate-200 rounded-2xl hover:bg-slate-50 shadow-sm transition-all disabled:opacity-50"
          >
            <Download size={14} className={exportingCsv ? 'animate-bounce text-blue-600' : ''} />
            <span>Xuất CSV</span>
          </button>

          <button
            type="button"
            onClick={() => {
              if (activeTab === 'overview') void loadRevenueData();
              else void loadSettlementsData();
            }}
            className="inline-flex items-center gap-1.5 px-3 py-2.5 text-xs font-semibold text-slate-700 bg-white border border-slate-200 rounded-2xl hover:bg-slate-50 shadow-sm transition-all"
            title="Làm mới dữ liệu"
          >
            <RefreshCw size={14} className={loadingRevenue || loadingSettlements ? 'animate-spin text-blue-600' : ''} />
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
              <AlertCircle size={18} className="text-rose-600 shrink-0 mt-0.5" />
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
          2. THANH ĐIỀU HƯỚNG TABS & BỘ LỌC THỜI GIAN
      ====================================================================== */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-3">
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => setActiveTab('overview')}
            className={`pb-2 px-4 text-xs font-bold transition-colors border-b-2 flex items-center gap-2 ${
              activeTab === 'overview'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <TrendingUp size={15} />
            Doanh thu & Giao dịch (US-078)
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('settlements')}
            className={`pb-2 px-4 text-xs font-bold transition-colors border-b-2 flex items-center gap-2 ${
              activeTab === 'settlements'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <Scale size={15} />
            Kỳ quyết toán & Đối soát ({settlements.length})
          </button>
        </div>

        {/* Khung thời gian preset */}
        {activeTab === 'overview' && (
          <div className="flex items-center gap-1.5 bg-slate-100 p-1 rounded-xl self-start sm:self-auto text-xs">
            <button
              type="button"
              onClick={() => { setDateRangePreset('7days'); setTransPage(1); }}
              className={`px-3 py-1 rounded-lg font-bold transition-all ${
                dateRangePreset === '7days' ? 'bg-white text-blue-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              7 ngày qua
            </button>
            <button
              type="button"
              onClick={() => { setDateRangePreset('30days'); setTransPage(1); }}
              className={`px-3 py-1 rounded-lg font-bold transition-all ${
                dateRangePreset === '30days' ? 'bg-white text-blue-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              30 ngày qua
            </button>
            <button
              type="button"
              onClick={() => { setDateRangePreset('thisMonth'); setTransPage(1); }}
              className={`px-3 py-1 rounded-lg font-bold transition-all ${
                dateRangePreset === 'thisMonth' ? 'bg-white text-blue-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tháng này
            </button>
          </div>
        )}
      </div>

      {/* =====================================================================
          3. TAB 1: DOANH THU & GIAO DỊCH (US-078)
      ====================================================================== */}
      {activeTab === 'overview' && (
        <div className="space-y-8">
          {/* 4 KPI METRIC CARDS */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Card 1: Doanh thu gộp */}
            <div className="bg-white rounded-3xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Doanh thu gộp (Gross)</p>
                <p className="text-2xl font-black text-slate-900 mt-1">
                  {formatVnd(summary?.totals?.grossRevenue)}
                </p>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  Tổng {summary?.totals?.transactionCount || 0} giao dịch
                </p>
              </div>
              <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 border border-blue-100 flex items-center justify-center">
                <DollarSign size={22} />
              </div>
            </div>

            {/* Card 2: Tiền hoàn trả khách */}
            <div className="bg-white rounded-3xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Tiền đã hoàn lại</p>
                <p className="text-2xl font-black text-rose-600 mt-1">
                  {formatVnd(summary?.totals?.refundAmount)}
                </p>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  Khách hủy hoặc lỗi bãi xe
                </p>
              </div>
              <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-600 border border-rose-100 flex items-center justify-center">
                <ArrowDownRight size={22} />
              </div>
            </div>

            {/* Card 3: Doanh thu thuần */}
            <div className="bg-white rounded-3xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Doanh thu thuần (Net)</p>
                <p className="text-2xl font-black text-emerald-600 mt-1">
                  {formatVnd(summary?.totals?.netRevenue)}
                </p>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  Gross − RefundAmount
                </p>
              </div>
              <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 border border-emerald-100 flex items-center justify-center">
                <TrendingUp size={22} />
              </div>
            </div>

            {/* Card 4: Hoa hồng nền tảng (US-079) */}
            <div className="bg-white rounded-3xl p-5 border border-slate-200 shadow-sm flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Hoa hồng nền tảng (US-079)</p>
                <p className="text-2xl font-black text-amber-600 mt-1">
                  {formatVnd(summary?.commission?.commissionAmount)}
                </p>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  {summary?.commission?.pendingCount ? `${summary.commission.pendingCount} đơn chờ chốt` : 'Đã hạch toán đầy đủ'}
                </p>
              </div>
              <div className="w-12 h-12 rounded-2xl bg-amber-50 text-amber-600 border border-amber-100 flex items-center justify-center">
                <Scale size={22} />
              </div>
            </div>
          </div>

          {/* BIỂU ĐỒ DIỄN BIẾN DOANH THU & PHÂN RÃ NGUỒN THU */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Visual Bar Time Series */}
            <div className="lg:col-span-2 bg-white rounded-3xl p-6 border border-slate-200 shadow-sm space-y-4">
              <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                <div>
                  <h3 className="text-sm font-bold text-slate-900">Diễn biến doanh thu theo ngày</h3>
                  <p className="text-xs text-slate-500">Chuỗi dữ liệu phát sinh trong kỳ</p>
                </div>
                <span className="text-xs font-bold text-blue-600 bg-blue-50 px-2.5 py-1 rounded-lg">
                  Theo ngày (Day)
                </span>
              </div>

              {loadingRevenue ? (
                <div className="h-48 flex items-center justify-center gap-2 text-slate-400 text-xs">
                  <RefreshCw size={20} className="animate-spin text-blue-600" />
                  Đang tải biểu đồ doanh thu...
                </div>
              ) : series?.points && series.points.length > 0 ? (
                <div className="space-y-3 pt-2">
                  {/* Top highest points visual */}
                  <div className="flex items-end gap-1.5 h-44 pt-6 pb-2 overflow-x-auto">
                    {series.points.map((pt, idx) => {
                      const maxNet = Math.max(...series.points.map((p) => p.netRevenue || 0), 1);
                      const netVal = pt.netRevenue || 0;
                      const heightPercent = Math.max(8, Math.round((netVal / maxNet) * 100));

                      // An toàn: Hỗ trợ cả periodStart trả về từ backend ASP.NET và bucket string
                      let pointLabel = `--`;
                      if (pt.periodStart) {
                        try {
                          const d = new Date(pt.periodStart);
                          pointLabel = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
                        } catch {
                          pointLabel = String(pt.periodStart).slice(5, 10);
                        }
                      } else if (typeof pt.bucket === 'string') {
                        pointLabel = pt.bucket.split('-').slice(1).join('/');
                      } else {
                        pointLabel = `#${idx + 1}`;
                      }

                      return (
                        <div
                          key={idx}
                          className="flex-1 min-w-[20px] flex flex-col items-center gap-1.5 group relative"
                        >
                          {/* Tooltip on hover */}
                          <div className="absolute -top-10 hidden group-hover:flex flex-col items-center z-20 pointer-events-none bg-slate-900 text-white text-[10px] px-2 py-1 rounded-md shadow-lg whitespace-nowrap">
                            <span>{pointLabel}: {formatVnd(netVal)}</span>
                            <span className="text-slate-300">({pt.transactionCount || 0} đơn)</span>
                          </div>

                          <div
                            style={{ height: `${heightPercent}%` }}
                            className="w-full rounded-t-lg bg-blue-500 group-hover:bg-blue-600 transition-all cursor-pointer relative"
                          />
                          <span className="text-[10px] text-slate-400 font-mono truncate max-w-[36px]">
                            {pointLabel}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ) : (
                <div className="h-44 flex items-center justify-center text-xs text-slate-400">
                  Không có dữ liệu chuỗi doanh thu trong khoảng thời gian này.
                </div>
              )}
            </div>

            {/* Breakdown theo Phương thức thanh toán (Method) */}
            <div className="bg-white rounded-3xl p-6 border border-slate-200 shadow-sm space-y-4">
              <div className="border-b border-slate-100 pb-3">
                <h3 className="text-sm font-bold text-slate-900">Phương thức thanh toán</h3>
                <p className="text-xs text-slate-500">Phân loại theo kênh thu tiền</p>
              </div>

              {summary?.byMethod && summary.byMethod.length > 0 ? (
                <div className="space-y-3">
                  {summary.byMethod.map((item, idx) => {
                    const totalGross = summary?.totals?.grossRevenue || 1;
                    const percent = Math.round(((item?.grossRevenue || 0) / totalGross) * 100);

                    return (
                      <div key={idx} className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <div className="flex items-center gap-2">
                            {getMethodIcon(item.key)}
                            <span className="font-semibold text-slate-800">{item.key}</span>
                          </div>
                          <span className="font-mono font-bold text-slate-900">{formatVnd(item.netRevenue)}</span>
                        </div>
                        <div className="w-full h-1.5 rounded-full bg-slate-100 overflow-hidden">
                          <div
                            style={{ width: `${percent}%` }}
                            className="h-full bg-blue-600 rounded-full"
                          />
                        </div>
                        <div className="flex justify-between text-[10px] text-slate-400">
                          <span>{item.transactionCount} giao dịch</span>
                          <span>{percent}%</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <p className="text-xs text-slate-400 text-center py-8">Chưa có giao dịch phát sinh.</p>
              )}
            </div>
          </div>

          {/* BẢNG DANH SÁCH GIAO DỊCH (US-078) */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Chi tiết giao dịch phát sinh</h3>
                <p className="text-xs text-slate-500">Mới nhất trước ({transactionTotal} giao dịch)</p>
              </div>
            </div>

            {loadingRevenue ? (
              <div className="p-16 flex flex-col items-center justify-center gap-3 text-slate-500">
                <RefreshCw size={24} className="animate-spin text-blue-600" />
                <p className="text-xs font-medium">Đang tải lịch sử giao dịch...</p>
              </div>
            ) : transactions.length === 0 ? (
              <div className="p-16 text-center text-xs text-slate-400">
                Không có giao dịch nào được ghi nhận trong khoảng thời gian đã chọn.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                      <th className="py-3.5 px-6">Mã giao dịch</th>
                      <th className="py-3.5 px-6">Mã đơn liên kết</th>
                      <th className="py-3.5 px-6">Phương thức</th>
                      <th className="py-3.5 px-6">Mục đích</th>
                      <th className="py-3.5 px-6">Số tiền</th>
                      <th className="py-3.5 px-6">Đã hoàn</th>
                      <th className="py-3.5 px-6">Thực nhận</th>
                      <th className="py-3.5 px-6">Thời gian</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {transactions.map((tx) => (
                      <tr key={tx.id} className="hover:bg-slate-50/60 transition-colors">
                        <td className="py-4 px-6 font-mono font-bold text-slate-900">
                          {tx.code}
                        </td>
                        <td className="py-4 px-6 font-mono text-slate-700">
                          {tx.bookingCode || (tx.parkingSessionId ? `Phiên #${tx.parkingSessionId}` : '--')}
                        </td>
                        <td className="py-4 px-6">
                          <div className="inline-flex items-center gap-1.5 font-medium text-slate-700">
                            {getMethodIcon(tx.method)}
                            <span>{tx.method}</span>
                          </div>
                        </td>
                        <td className="py-4 px-6 text-slate-600">
                          {tx.purpose}
                        </td>
                        <td className="py-4 px-6 font-bold text-slate-900">
                          {formatVnd(tx.amount)}
                        </td>
                        <td className="py-4 px-6 text-rose-600 font-semibold">
                          {tx.refundedAmount > 0 ? `-${formatVnd(tx.refundedAmount)}` : '--'}
                        </td>
                        <td className="py-4 px-6 font-bold text-emerald-600">
                          {formatVnd(tx.netAmount)}
                        </td>
                        <td className="py-4 px-6 text-slate-500">
                          {formatDate(tx.paidAtUtc)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Phân trang */}
            {transactionTotal > 15 && (
              <div className="flex items-center justify-between px-6 py-4 border-t border-slate-100 bg-slate-50/50">
                <span className="text-xs text-slate-500">
                  Trang <strong className="text-slate-900 font-bold">{transPage}</strong>
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={transPage <= 1 || loadingRevenue}
                    onClick={() => setTransPage((p) => Math.max(1, p - 1))}
                    className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 transition-colors"
                  >
                    <ChevronLeft size={14} />
                    Trước
                  </button>
                  <button
                    type="button"
                    disabled={transPage * 15 >= transactionTotal || loadingRevenue}
                    onClick={() => setTransPage((p) => p + 1)}
                    className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 transition-colors"
                  >
                    Sau
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* =====================================================================
          4. TAB 2: CÁC KỲ QUYẾT TOÁN ĐÃ CHỐT (US-080)
      ====================================================================== */}
      {activeTab === 'settlements' && (
        <div className="space-y-4">
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Danh sách các kỳ quyết toán đã duyệt</h3>
                <p className="text-xs text-slate-500">Các đợt chi trả đã được chốt và thanh toán cho chủ bãi xe</p>
              </div>
            </div>

            {loadingSettlements ? (
              <div className="p-16 flex flex-col items-center justify-center gap-3 text-slate-500">
                <RefreshCw size={24} className="animate-spin text-blue-600" />
                <p className="text-xs font-medium">Đang tải danh sách kỳ quyết toán...</p>
              </div>
            ) : settlements.length === 0 ? (
              <div className="p-16 text-center text-xs text-slate-400">
                Chưa có kỳ quyết toán nào được chốt cho tài khoản của bạn.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                      <th className="py-3.5 px-6">Mã kỳ quyết toán</th>
                      <th className="py-3.5 px-6">Chu kỳ quyết toán</th>
                      <th className="py-3.5 px-6">Doanh thu gộp</th>
                      <th className="py-3.5 px-6">Hoa hồng</th>
                      <th className="py-3.5 px-6">Thực nhận (Net Payout)</th>
                      <th className="py-3.5 px-6">Trạng thái</th>
                      <th className="py-3.5 px-6 text-right">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {settlements.map((stl) => (
                      <tr key={stl.id} className="hover:bg-slate-50/60 transition-colors">
                        <td className="py-4 px-6 font-mono font-bold text-slate-900">
                          {stl.code}
                        </td>
                        <td className="py-4 px-6 text-slate-700">
                          <div className="flex items-center gap-1.5 font-medium">
                            <Calendar size={13} className="text-blue-600 shrink-0" />
                            <span>{stl.periodStart}</span>
                            <span className="text-slate-400">→</span>
                            <span>{stl.periodEnd}</span>
                          </div>
                        </td>
                        <td className="py-4 px-6 font-semibold text-slate-900">
                          {formatVnd(stl.grossAmount)}
                        </td>
                        <td className="py-4 px-6 font-semibold text-amber-700">
                          -{formatVnd(stl.commissionAmount)}
                        </td>
                        <td className="py-4 px-6 font-black text-blue-600 text-sm">
                          {formatVnd(stl.netPayout)}
                        </td>
                        <td className="py-4 px-6">
                          <span
                            className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                              stl.status === 'Paid'
                                ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                : 'bg-blue-50 text-blue-700 border-blue-200'
                            }`}
                          >
                            {stl.status === 'Paid' ? 'Đã chi trả' : 'Đã duyệt'}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-right">
                          <button
                            type="button"
                            onClick={() => setSelectedSettlementId(stl.id)}
                            className="inline-flex items-center gap-1 px-3 py-1.5 rounded-xl text-xs font-semibold text-blue-600 hover:text-blue-800 hover:bg-blue-50 transition-colors"
                          >
                            <Eye size={13} />
                            Đối soát
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
          5. MODAL ĐỐI SOÁT QUYẾT TOÁN (US-080)
      ====================================================================== */}
      <SettlementDetailModal
        isOpen={selectedSettlementId !== null}
        onClose={() => setSelectedSettlementId(null)}
        settlementId={selectedSettlementId}
      />
    </div>
  );
};
