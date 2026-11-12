import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAppSelector } from '@/store';
import { ownerExitApi, type OwnerExitStatusDto } from '@/api/ownerExitApi';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import {
  ownerRevenueApi,
  type OwnerRevenueSummaryDto,
  type OwnerRevenueSeriesDto,
} from '@/api/ownerRevenueApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import { OwnerExitStatusBanner } from '../components/exit/OwnerExitStatusBanner';
import { ExitPlatformModal } from '../components/exit/ExitPlatformModal';
import { StaleDataBanner } from '../components/capacity/StaleDataBanner';

export const OwnerHomePage: React.FC = () => {
  const navigate = useNavigate();
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Trạng thái Rời nền tảng (UI-21) & Stale Data (UI-27)
  const [exitStatus, setExitStatus] = useState<OwnerExitStatusDto | null>(null);
  const [isExitModalOpen, setIsExitModalOpen] = useState(false);
  const [isStaleData, setIsStaleData] = useState(false);

  // Danh sách bãi đỗ xe thực tế
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | 'all'>('all');
  const [loadingLots, setLoadingLots] = useState(true);

  // Dữ liệu doanh thu & chuỗi thời gian thực tế (US-078)
  const [summary, setSummary] = useState<OwnerRevenueSummaryDto | null>(null);
  const [series, setSeries] = useState<OwnerRevenueSeriesDto | null>(null);
  const [loadingRevenue, setLoadingRevenue] = useState(true);

  // Chế độ xem biểu đồ
  const [metricMode, setMetricMode] = useState<'revenue' | 'traffic'>('revenue');
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

  // 1. Tải trạng thái exit
  useEffect(() => {
    ownerExitApi
      .getExitStatus(ownerProfileId)
      .then(setExitStatus)
      .catch(() => {});
  }, [ownerProfileId]);

  // 2. Tải danh sách bãi xe thực tế
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

  // 3. Tải báo cáo doanh thu & chuỗi dữ liệu thực tế
  const loadDashboardData = useCallback(async () => {
    setLoadingRevenue(true);

    const filter = {
      parkingLotId: selectedLotId === 'all' ? undefined : selectedLotId,
    };

    try {
      const [sumData, seriesData] = await Promise.all([
        ownerRevenueApi.getRevenueSummary(filter),
        ownerRevenueApi.getRevenueSeries({
          ...filter,
          granularity: 'Day',
        }),
      ]);

      setSummary(sumData);
      setSeries(seriesData);
    } catch {
      setSummary(null);
      setSeries(null);
    } finally {
      setLoadingRevenue(false);
    }
  }, [selectedLotId]);

  useEffect(() => {
    void loadDashboardData();
  }, [loadDashboardData]);

  // Format tiền tệ VNĐ
  const formatVnd = (val?: number | null) => {
    if (val === undefined || val === null) return '0 ₫';

    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(val);
  };

  // Thống kê sức chứa thực tế từ bãi xe
  const totalSlots = useMemo(
    () => lots.reduce((acc, lot) => acc + (lot.totalSlots || 0), 0),
    [lots],
  );

  const availableSlots = useMemo(
    () => lots.reduce((acc, lot) => acc + (lot.availableSlots || 0), 0),
    [lots],
  );

  const activeLotsCount = useMemo(
    () => lots.filter((lot) => lot.status === 'Active').length,
    [lots],
  );

  // 7 điểm dữ liệu biểu đồ gần nhất
  const chartPoints = useMemo(() => {
    if (!series?.points || series.points.length === 0) return [];
    return series.points.slice(-7);
  }, [series]);

  // Tính tọa độ SVG Chart thực tế
  const xCoords = [60, 150, 240, 330, 420, 510, 600];
  const baselineY = 195;
  const chartHeight = 140;

  const maxVal = useMemo(() => {
    if (chartPoints.length === 0) return 100_000;

    if (metricMode === 'revenue') {
      const maxRevenue = Math.max(
        ...chartPoints.map((point) => point.netRevenue || 0),
      );

      return Math.max(maxRevenue, 100_000);
    }

    const maxTraffic = Math.max(
      ...chartPoints.map((point) => point.transactionCount || 0),
    );

    return Math.max(maxTraffic, 10);
  }, [chartPoints, metricMode]);

  const getY = (val: number) => {
    const clamped = Math.max(0, Math.min(maxVal, val));
    return baselineY - (clamped / maxVal) * chartHeight;
  };

  const svgDataPoints = useMemo(() => {
    return chartPoints.map((point, index) => {
      const val =
        metricMode === 'revenue'
          ? point.netRevenue || 0
          : point.transactionCount || 0;

      let dayName = `D${index + 1}`;
      let dateLabel = '--';

      if (point.periodStart) {
        try {
          const date = new Date(point.periodStart);
          const daysOfWeek = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

          dayName = daysOfWeek[date.getDay()] || `T${date.getDay() + 1}`;
          dateLabel = `${String(date.getDate()).padStart(2, '0')}/${String(
            date.getMonth() + 1,
          ).padStart(2, '0')}`;
        } catch {
          dateLabel = String(point.periodStart).slice(5, 10);
        }
      }

      return {
        x: xCoords[index] || 60 + index * 90,
        y: getY(val),
        val,
        dayName,
        dateLabel,
        raw: point,
      };
    });
  }, [chartPoints, metricMode, maxVal]);

  const lineD =
    svgDataPoints.length > 0
      ? svgDataPoints
          .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x},${point.y}`)
          .join(' ')
      : '';

  const areaD =
    svgDataPoints.length > 0
      ? `${lineD} L ${
          svgDataPoints[svgDataPoints.length - 1].x
        },${baselineY} L ${svgDataPoints[0].x},${baselineY} Z`
      : '';

  // Khung giờ 24h thực tế từ API (byHourOfDay)
  const hourlyData = useMemo(() => {
    if (!summary?.byHourOfDay || summary.byHourOfDay.length === 0) {
      return [];
    }

    const maxHourGross = Math.max(
      ...summary.byHourOfDay.map((hour) => hour.grossRevenue || 0),
      1,
    );

    return summary.byHourOfDay.map((hour) => ({
      hour: String(hour.hour).padStart(2, '0'),
      pct: Math.max(
        6,
        Math.round(((hour.grossRevenue || 0) / maxHourGross) * 100),
      ),
      grossRevenue: hour.grossRevenue || 0,
      transactionCount: hour.transactionCount || 0,
    }));
  }, [summary]);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* ===== HEADER ===== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            PORTAL CHỦ BÃI • BÁO CÁO DOANH THU & CHỈ SỐ VẬN HÀNH
          </div>

          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Tổng quan Bãi đỗ & Doanh thu
          </h1>

          <p className="text-slate-500 text-sm leading-relaxed">
            Dữ liệu vận hành thực tế: Theo dõi biến động doanh thu, sức chứa bãi
            xe và cơ cấu nguồn thu từ các hệ thống thanh toán tự động.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/owner/revenue')}
            className="px-4 py-2.5 rounded-xl border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 transition-colors text-sm font-semibold flex items-center gap-2 shadow-xs cursor-pointer"
          >
            <span className="material-symbols-outlined text-[18px] text-blue-600">
              analytics
            </span>
            <span>Báo cáo doanh thu & Quyết toán</span>
          </button>

          <button
            type="button"
            onClick={() => setIsExitModalOpen(true)}
            className="px-3.5 py-2.5 rounded-xl border border-rose-200 bg-rose-50 text-rose-700 hover:bg-rose-100 transition-colors text-sm font-semibold flex items-center gap-1.5 shadow-xs cursor-pointer"
          >
            <span className="material-symbols-outlined text-[18px] text-rose-600">
              logout
            </span>
            <span>Xin rời nền tảng</span>
          </button>

          <button
            type="button"
            onClick={() => navigate('/owner/kyb')}
            className="px-5 py-2.5 rounded-xl bg-blue-600 text-white hover:bg-blue-700 transition-all shadow-sm font-semibold text-sm flex items-center gap-2 cursor-pointer"
          >
            <span className="material-symbols-outlined text-[20px]">add</span>
            <span>Đăng ký bãi mới</span>
          </button>
        </div>
      </div>

      {/* Cảnh báo Rời nền tảng (UI-21) */}
      <OwnerExitStatusBanner
        exitStatus={exitStatus}
        onOpenModal={() => setIsExitModalOpen(true)}
      />

      {/* Cảnh báo Dữ liệu cũ Stale Data (UI-27) */}
      <StaleDataBanner
        isStale={isStaleData}
        onRefresh={() => {
          setIsStaleData(false);
          void loadDashboardData();
        }}
      />

      {/* ===== KHỐI 1: 4 THẺ CHỈ SỐ TELEMETRY RIBBON DỰA TRÊN DỮ LIỆU THỰC ===== */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
        {/* Doanh thu gộp */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
              Doanh thu gộp (Gross)
            </span>

            <div className="w-9 h-9 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">
                payments
              </span>
            </div>
          </div>

          <div>
            <div className="text-3xl font-extrabold text-slate-900 tracking-tight">
              {loadingRevenue
                ? 'Đang tải...'
                : formatVnd(summary?.totals?.grossRevenue)}
            </div>

            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="w-2 h-2 rounded-full bg-blue-600"></span>
              <span>
                Tổng {summary?.totals?.transactionCount || 0} lượt giao dịch
              </span>
            </div>
          </div>
        </div>

        {/* Doanh thu thuần */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
              Doanh thu thuần (Net)
            </span>

            <div className="w-9 h-9 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">
                analytics
              </span>
            </div>
          </div>

          <div>
            <div className="text-3xl font-extrabold text-emerald-600 tracking-tight">
              {loadingRevenue
                ? 'Đang tải...'
                : formatVnd(summary?.totals?.netRevenue)}
            </div>

            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>
                Đã trừ hoàn trả: {formatVnd(summary?.totals?.refundAmount)}
              </span>
            </div>
          </div>
        </div>

        {/* Hoa hồng nền tảng */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
              Hoa hồng nền tảng
            </span>

            <div className="w-9 h-9 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">
                account_balance_wallet
              </span>
            </div>
          </div>

          <div>
            <div className="text-3xl font-extrabold text-amber-600 tracking-tight">
              {loadingRevenue
                ? 'Đang tải...'
                : formatVnd(summary?.commission?.commissionAmount)}
            </div>

            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="w-2 h-2 rounded-full bg-amber-500"></span>
              <span>
                {summary?.commission?.pendingCount || 0} đơn đang xử lý kỳ
              </span>
            </div>
          </div>
        </div>

        {/* Công suất lấp đầy */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3 hover:border-slate-300 transition-all">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">
              Công suất phục vụ
            </span>

            <div className="w-9 h-9 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">
                directions_car
              </span>
            </div>
          </div>

          <div>
            <div className="text-3xl font-extrabold text-slate-900 tracking-tight">
              {availableSlots} / {totalSlots} Ô
            </div>

            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              <span className="font-semibold text-slate-700">
                Đang hoạt động: {activeLotsCount} / {lots.length} Bãi đỗ xe
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* ===== KHỐI 2: SƠ ĐỒ THỐNG KÊ DOANH THU THỰC TẾ ===== */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* CỘT TRÁI (2/3): BIỂU ĐỒ MIỀN XU HƯỚNG */}
        <div className="lg:col-span-2 p-6 bg-white rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between overflow-hidden">
          <div>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-5 border-b border-slate-100">
              <div>
                <h2 className="text-lg font-extrabold text-slate-900 tracking-tight">
                  {metricMode === 'revenue'
                    ? 'Diễn biến doanh thu gần đây'
                    : 'Lượt giao dịch gần đây'}
                </h2>

                <p className="text-xs text-slate-500 mt-0.5">
                  Theo dõi dữ liệu thực tế phát sinh trên các bãi đỗ
                </p>
              </div>

              {/* Nhóm nút chọn kiểu hiển thị */}
              <div className="flex flex-wrap items-center gap-2">
                <div className="p-1 bg-slate-100 rounded-xl flex items-center gap-1 border border-slate-200/70">
                  <button
                    type="button"
                    onClick={() => setMetricMode('revenue')}
                    className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                      metricMode === 'revenue'
                        ? 'bg-white text-blue-700 shadow-xs'
                        : 'text-slate-600 hover:text-slate-900'
                    }`}
                  >
                    Doanh thu
                  </button>

                  <button
                    type="button"
                    onClick={() => setMetricMode('traffic')}
                    className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                      metricMode === 'traffic'
                        ? 'bg-white text-blue-700 shadow-xs'
                        : 'text-slate-600 hover:text-slate-900'
                    }`}
                  >
                    Lượt xe
                  </button>
                </div>
              </div>
            </div>

            {/* Chọn bãi xe thực tế */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-500 mt-4 px-1">
              <span className="text-xs font-semibold text-slate-600">
                Hiển thị dữ liệu 7 ngày gần nhất:
              </span>

              <div className="flex items-center gap-1.5 text-xs text-slate-600 bg-slate-50 px-3 py-1.5 rounded-xl border border-slate-200">
                <span className="text-slate-400 text-[11px] font-semibold uppercase tracking-wider">
                  Bãi xe:
                </span>

                <select
                  value={selectedLotId}
                  onChange={(e) =>
                    setSelectedLotId(
                      e.target.value === 'all'
                        ? 'all'
                        : Number(e.target.value),
                    )
                  }
                  disabled={loadingLots}
                  className="text-xs text-slate-800 font-bold bg-transparent border-none focus:outline-hidden cursor-pointer"
                >
                  <option value="all">
                    Tất cả các bãi ({lots.length} bãi)
                  </option>

                  {lots.map((lot) => (
                    <option key={lot.id} value={lot.id}>
                      {lot.name} (#{lot.id})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* CANVAS BIỂU ĐỒ MIỀN SVG */}
            <div className="relative w-full h-[250px] mt-4 select-none overflow-hidden rounded-xl">
              {loadingRevenue ? (
                <div className="h-full flex items-center justify-center text-xs text-slate-400">
                  Đang tải biểu đồ...
                </div>
              ) : svgDataPoints.length === 0 ? (
                <div className="h-full flex flex-col items-center justify-center text-slate-400 text-xs">
                  <span className="material-symbols-outlined text-[36px] text-slate-300 mb-1">
                    query_stats
                  </span>
                  <span>Chưa có dữ liệu giao dịch trong chu kỳ này.</span>
                  <span className="text-[11px] text-slate-400 mt-0.5">
                    Biểu đồ sẽ tự động hiển thị khi phát sinh lượt đỗ.
                  </span>
                </div>
              ) : (
                <svg
                  className="w-full h-full"
                  viewBox="0 0 660 235"
                  preserveAspectRatio="none"
                >
                  <defs>
                    <linearGradient
                      id="realCurrentAreaGrad"
                      x1="0"
                      y1="0"
                      x2="0"
                      y2="1"
                    >
                      <stop offset="0%" stopColor="#2563eb" stopOpacity="0.45" />
                      <stop offset="100%" stopColor="#3b82f6" stopOpacity="0.02" />
                    </linearGradient>
                  </defs>

                  {/* Grid lines */}
                  <line
                    x1="30"
                    y1="55"
                    x2="630"
                    y2="55"
                    stroke="#f1f5f9"
                    strokeWidth="1"
                  />
                  <line
                    x1="30"
                    y1="105"
                    x2="630"
                    y2="105"
                    stroke="#f1f5f9"
                    strokeWidth="1"
                  />
                  <line
                    x1="30"
                    y1="150"
                    x2="630"
                    y2="150"
                    stroke="#f1f5f9"
                    strokeWidth="1"
                  />
                  <line
                    x1="30"
                    y1={baselineY}
                    x2="630"
                    y2={baselineY}
                    stroke="#e2e8f0"
                    strokeWidth="1.5"
                  />

                  {/* Area fill */}
                  <path d={areaD} fill="url(#realCurrentAreaGrad)" />

                  {/* Line stroke */}
                  <path
                    d={lineD}
                    fill="none"
                    stroke="#2563eb"
                    strokeWidth="3"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />

                  {/* Data points */}
                  {svgDataPoints.map((point, index) => {
                    const isHovered = hoveredIndex === index;

                    return (
                      <g
                        key={index}
                        className="cursor-pointer"
                        onMouseEnter={() => setHoveredIndex(index)}
                        onMouseLeave={() => setHoveredIndex(null)}
                      >
                        {isHovered && (
                          <line
                            x1={point.x}
                            y1="55"
                            x2={point.x}
                            y2={baselineY}
                            stroke="#3b82f6"
                            strokeWidth="1.5"
                            strokeDasharray="3 3"
                          />
                        )}

                        <circle
                          cx={point.x}
                          cy={point.y}
                          r={isHovered ? '6.5' : '4.5'}
                          fill="#2563eb"
                          stroke="#ffffff"
                          strokeWidth="2"
                        />

                        {/* Top label */}
                        <text
                          x={point.x}
                          y={point.y - 10}
                          textAnchor="middle"
                          className="text-[10px] font-mono font-bold fill-slate-700 select-none"
                        >
                          {metricMode === 'revenue'
                            ? formatVnd(point.val)
                            : `${point.val} xe`}
                        </text>

                        {/* Bottom day & date */}
                        <text
                          x={point.x}
                          y="212"
                          textAnchor="middle"
                          className={`text-xs font-bold select-none ${
                            isHovered ? 'fill-blue-700' : 'fill-slate-600'
                          }`}
                        >
                          {point.dayName}
                        </text>

                        <text
                          x={point.x}
                          y="226"
                          textAnchor="middle"
                          className="text-[10px] font-mono fill-slate-400 select-none"
                        >
                          {point.dateLabel}
                        </text>
                      </g>
                    );
                  })}
                </svg>
              )}
            </div>
          </div>
        </div>

        {/* CỘT PHẢI (1/3): CƠ CẤU DOANH THU THEO PHƯƠNG THỨC THỰC TẾ */}
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between overflow-hidden">
          <div>
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600 text-[20px]">
                  donut_large
                </span>
                Cơ cấu Nguồn thu
              </h3>

              <span className="text-[11px] font-bold text-blue-700 bg-blue-50 px-2.5 py-0.5 rounded-lg border border-blue-200/50">
                Thực tế
              </span>
            </div>

            {summary?.byMethod && summary.byMethod.length > 0 ? (
              <div className="space-y-4 my-6">
                {summary.byMethod.map((item, index) => {
                  const totalGross = summary.totals?.grossRevenue || 1;
                  const pct = Math.round(
                    ((item.grossRevenue || 0) / totalGross) * 100,
                  );

                  return (
                    <div key={index} className="space-y-1.5">
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-slate-800">
                          {item.key}
                        </span>

                        <div className="text-right">
                          <span className="font-bold text-slate-900">
                            {formatVnd(item.netRevenue)}
                          </span>
                          <span className="text-slate-400 ml-1">
                            ({pct}%)
                          </span>
                        </div>
                      </div>

                      <div className="w-full bg-slate-100 rounded-full h-2 overflow-hidden">
                        <div
                          className="bg-blue-600 h-2 rounded-full"
                          style={{
                            width: `${Math.min(100, Math.max(5, pct))}%`,
                          }}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="py-16 text-center text-xs text-slate-400">
                Chưa có dữ liệu phân loại nguồn thu trong kỳ này.
              </div>
            )}
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-400">
            <span>Hệ thống đối soát tự động ParkMaster</span>

            <button
              type="button"
              onClick={() => navigate('/owner/revenue')}
              className="font-semibold text-blue-600 hover:text-blue-700 cursor-pointer"
            >
              Chi tiết đối soát →
            </button>
          </div>
        </div>
      </div>

      {/* ===== KHỐI 3: PHÂN BỔ KHUNG GIỜ THỰC TẾ & DANH SÁCH BÃI XE THẬT ===== */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* LƯU LƯỢNG 24H TỪ DỮ LIỆU THỰC */}
        <div className="lg:col-span-2 p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between mb-2">
            <div>
              <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600 text-[20px]">
                  schedule
                </span>
                Phân bổ Doanh thu theo Khung giờ (24h)
              </h3>

              <p className="text-xs text-slate-500 mt-0.5">
                Thống kê thực tế lượng giao dịch phát sinh qua 24 giờ trong ngày
              </p>
            </div>
          </div>

          <div className="mt-6 pt-2">
            {hourlyData.length === 0 ? (
              <div className="h-32 flex items-center justify-center text-xs text-slate-400">
                Chưa có dữ liệu theo khung giờ.
              </div>
            ) : (
              <div className="h-32 flex items-end justify-between gap-1 sm:gap-1.5">
                {hourlyData.map((hour) => (
                  <div
                    key={hour.hour}
                    className="flex-1 flex flex-col items-center h-full justify-end group"
                  >
                    <div
                      style={{ height: `${hour.pct}%` }}
                      className="w-full rounded-t-sm bg-blue-500 group-hover:bg-blue-600 transition-all cursor-pointer"
                      title={`${hour.hour}h: ${formatVnd(
                        hour.grossRevenue,
                      )} (${hour.transactionCount} đơn)`}
                    />

                    <span className="text-[9px] font-mono text-slate-400 mt-1">
                      {hour.hour}h
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* DANH SÁCH BÃI XE THỰC TẾ CỦA CHỦ BÃI */}
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-bold text-slate-900 text-sm">
                Bãi đỗ xe của bạn ({lots.length})
              </h3>

              <button
                type="button"
                onClick={() => navigate('/owner/lots')}
                className="text-xs font-semibold text-blue-600 hover:text-blue-700 cursor-pointer"
              >
                Quản lý →
              </button>
            </div>

            {loadingLots ? (
              <div className="py-8 text-center text-xs text-slate-400">
                Đang tải danh sách bãi đỗ xe...
              </div>
            ) : lots.length === 0 ? (
              <div className="py-8 text-center text-xs text-slate-400">
                Chưa có bãi đỗ xe nào được khai báo.
              </div>
            ) : (
              <div className="space-y-3 max-h-64 overflow-y-auto">
                {lots.map((lot) => {
                  const occupancy =
                    lot.totalSlots > 0
                      ? Math.round(
                          ((lot.totalSlots - lot.availableSlots) /
                            lot.totalSlots) *
                            100,
                        )
                      : 0;

                  return (
                    <div
                      key={lot.id}
                      className="p-3.5 rounded-xl border border-slate-200 bg-slate-50/60"
                    >
                      <div className="flex items-center justify-between text-xs font-bold text-slate-900 mb-1">
                        <span className="truncate">{lot.name}</span>
                        <span className="text-blue-700 shrink-0">
                          #{lot.id}
                        </span>
                      </div>

                      <div className="flex items-center justify-between text-[11px] text-slate-500 mb-2">
                        <span>
                          {lot.totalSlots} slots • {lot.availableSlots} ô trống
                        </span>

                        <span
                          className={`font-semibold ${
                            lot.status === 'Active'
                              ? 'text-emerald-600'
                              : 'text-amber-600'
                          }`}
                        >
                          {lot.status}
                        </span>
                      </div>

                      <div className="w-full bg-slate-200 rounded-full h-1.5 overflow-hidden">
                        <div
                          className="bg-blue-600 h-1.5 rounded-full"
                          style={{
                            width: `${Math.min(
                              100,
                              Math.max(0, occupancy),
                            )}%`,
                          }}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 text-[11px] text-slate-500 flex items-center justify-between">
            <span>Tổng cộng:</span>
            <strong className="text-slate-900">
              {totalSlots} ô đỗ trên toàn hệ thống
            </strong>
          </div>
        </div>
      </div>

      <ExitPlatformModal
        isOpen={isExitModalOpen}
        onClose={() => setIsExitModalOpen(false)}
        ownerProfileId={ownerProfileId}
        onStatusChanged={(updated) => setExitStatus(updated)}
      />
    </div>
  );
};