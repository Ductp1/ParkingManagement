import React, { useState } from 'react';

interface DayStat {
  day: string;
  date: string;
  currentRevenue: number; // Triệu VNĐ
  prevRevenue: number;
  currentTraffic: number; // Số lượt xe
  prevTraffic: number;
  occupancy: number;
}

const SEVEN_DAYS_DATA: DayStat[] = [
  { day: 'T2', date: '12/10', currentRevenue: 11.2, prevRevenue: 9.8, currentTraffic: 260, prevTraffic: 230, occupancy: 72 },
  { day: 'T3', date: '13/10', currentRevenue: 12.8, prevRevenue: 10.5, currentTraffic: 295, prevTraffic: 245, occupancy: 78 },
  { day: 'T4', date: '14/10', currentRevenue: 13.4, prevRevenue: 11.0, currentTraffic: 310, prevTraffic: 255, occupancy: 82 },
  { day: 'T5', date: '15/10', currentRevenue: 12.6, prevRevenue: 10.8, currentTraffic: 288, prevTraffic: 250, occupancy: 76 },
  { day: 'T6', date: '16/10', currentRevenue: 15.2, prevRevenue: 12.4, currentTraffic: 360, prevTraffic: 290, occupancy: 88 },
  { day: 'T7', date: '17/10', currentRevenue: 16.8, prevRevenue: 14.1, currentTraffic: 395, prevTraffic: 330, occupancy: 94 },
  { day: 'CN', date: '18/10', currentRevenue: 14.85, prevRevenue: 12.5, currentTraffic: 342, prevTraffic: 290, occupancy: 84 },
];

const HOURLY_TRAFFIC = [
  { hour: '00', pct: 15 }, { hour: '02', pct: 10 }, { hour: '04', pct: 8 },
  { hour: '06', pct: 28 }, { hour: '07', pct: 65 }, { hour: '08', pct: 95 },
  { hour: '09', pct: 92 }, { hour: '10', pct: 84 }, { hour: '11', pct: 75 },
  { hour: '12', pct: 70 }, { hour: '13', pct: 72 }, { hour: '14', pct: 78 },
  { hour: '15', pct: 80 }, { hour: '16', pct: 85 }, { hour: '17', pct: 93 },
  { hour: '18', pct: 90 }, { hour: '19', pct: 76 }, { hour: '20', pct: 60 },
  { hour: '21', pct: 45 }, { hour: '22', pct: 30 }, { hour: '23', pct: 20 },
];

export const OwnerHomePage: React.FC = () => {
  const [metricMode, setMetricMode] = useState<'revenue' | 'traffic'>('revenue');
  const [periodMode, setPeriodMode] = useState<'week' | 'month'>('week');
  const [selectedLot, setSelectedLot] = useState<'all' | 'landmark' | 'bitexco'>('all');
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(6); // Mặc định chọn Chủ nhật (Hôm nay)

  const activeStat = hoveredIndex !== null ? SEVEN_DAYS_DATA[hoveredIndex] : SEVEN_DAYS_DATA[6];

  // Tọa độ tính toán an toàn cho SVG Chart (ViewBox: 0 0 660 235)
  // X: 60 -> 600 (Cách đều hai biên 60px để không bao giờ bị cắt nhãn hay tràn lề)
  const xCoords = [60, 150, 240, 330, 420, 510, 600];
  const baselineY = 195;
  const chartHeight = 140;
  const maxVal = metricMode === 'revenue' ? 20 : 450; // 20 Tr hoặc 450 xe

  const getY = (val: number) => {
    const clamped = Math.max(0, Math.min(maxVal, val));
    return baselineY - (clamped / maxVal) * chartHeight;
  };

  // Tọa độ kỳ hiện tại
  const currentPoints = SEVEN_DAYS_DATA.map((d, i) => ({
    x: xCoords[i],
    y: getY(metricMode === 'revenue' ? d.currentRevenue : d.currentTraffic),
    label: metricMode === 'revenue' ? `${d.currentRevenue.toFixed(1)} Tr` : `${d.currentTraffic} xe`,
    data: d,
  }));

  // Tọa độ kỳ trước (7 ngày trước đó)
  const prevPoints = SEVEN_DAYS_DATA.map((d, i) => ({
    x: xCoords[i],
    y: getY(metricMode === 'revenue' ? d.prevRevenue : d.prevTraffic),
    label: metricMode === 'revenue' ? `${d.prevRevenue.toFixed(1)} Tr` : `${d.prevTraffic} xe`,
  }));

  // Đường dẫn SVG Path cho đường nét và miền phủ
  const currentLineD = currentPoints.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x},${p.y}`).join(' ');
  const currentAreaD = `${currentLineD} L ${xCoords[6]},${baselineY} L ${xCoords[0]},${baselineY} Z`;

  const prevLineD = prevPoints.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x},${p.y}`).join(' ');
  const prevAreaD = `${prevLineD} L ${xCoords[6]},${baselineY} L ${xCoords[0]},${baselineY} Z`;

  // Tính tổng & so sánh
  const totalCurrentRev = SEVEN_DAYS_DATA.reduce((acc, d) => acc + d.currentRevenue, 0);
  const totalPrevRev = SEVEN_DAYS_DATA.reduce((acc, d) => acc + d.prevRevenue, 0);
  const revGrowth = (((totalCurrentRev - totalPrevRev) / totalPrevRev) * 100).toFixed(1);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* ===== HEADER ===== */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-100 text-indigo-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🏢 Cổng Chủ Bãi (Owner Portal)
          </div>
          <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">
            Tổng quan Bãi đỗ & Báo cáo Doanh thu
          </h1>
          <p className="text-slate-500 text-sm mt-1 max-w-3xl">
            Sơ đồ thống kê doanh thu đa chiều: Theo dõi biểu đồ miền xu hướng, phân tích cơ cấu nguồn thu và chỉ số vận hành.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button className="px-4 py-2 bg-slate-100 text-slate-700 rounded-xl text-sm font-semibold hover:bg-slate-200 border border-slate-200 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer">
            <span className="material-symbols-outlined text-[18px]">download</span>
            Xuất Excel / PDF
          </button>
          <button className="px-4 py-2 bg-indigo-600 text-white rounded-xl text-sm font-semibold hover:bg-indigo-700 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer">
            <span className="material-symbols-outlined text-[18px]">add</span>
            Đăng ký bãi mới
          </button>
        </div>
      </div>

      {/* ===== KHỐI 1: 4 THẺ CHỈ SỐ KPI CHÍNH ===== */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5 mt-8">
        {/* Doanh thu hôm nay */}
        <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs hover:border-indigo-400 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Doanh thu hôm nay</span>
            <span className="px-2 py-0.5 rounded-md bg-emerald-50 text-emerald-600 text-[11px] font-bold">
              +{revGrowth}%
            </span>
          </div>
          <div className="text-2xl font-extrabold text-slate-900 mt-2">14.850.000 đ</div>
          <p className="text-xs text-slate-500 mt-1">342 lượt giao dịch • 84% lấp đầy</p>
        </div>

        {/* Doanh thu tháng này */}
        <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs hover:border-indigo-400 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Doanh thu tháng này</span>
            <span className="px-2 py-0.5 rounded-md bg-blue-50 text-blue-600 text-[11px] font-bold">
              89.2% Chỉ tiêu
            </span>
          </div>
          <div className="text-2xl font-extrabold text-indigo-600 mt-2">312.400.000 đ</div>
          <p className="text-xs text-slate-500 mt-1">Kỳ đối soát: 01/10 - 31/10/2026</p>
        </div>

        {/* Số dư có thể rút */}
        <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs hover:border-indigo-400 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Số dư khả dụng</span>
            <span className="material-symbols-outlined text-[18px] text-emerald-600">account_balance_wallet</span>
          </div>
          <div className="text-2xl font-extrabold text-emerald-600 mt-2">281.160.000 đ</div>
          <p className="text-xs text-slate-500 mt-1">Vietcombank • 0011000000001</p>
        </div>

        {/* Công suất lấp đầy */}
        <div className="p-5 bg-white rounded-2xl border border-slate-200 shadow-xs hover:border-indigo-400 transition-colors">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Công suất phục vụ</span>
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
          </div>
          <div className="text-2xl font-extrabold text-blue-600 mt-2">168 / 200 Ô</div>
          <p className="text-xs text-slate-500 mt-1">Đang hoạt động: 02 Bãi đỗ xe</p>
        </div>
      </div>

      {/* ===== KHỐI 2: SƠ ĐỒ THỐNG KÊ DOANH THU THEO PHONG CÁCH SPLINE AREA CHART ===== */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mt-8">
        {/* CỘT TRÁI (2/3): BIỂU ĐỒ MIỀN XU HƯỚNG */}
        <div className="lg:col-span-2 p-6 bg-white rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between overflow-hidden">
          <div>
            {/* Header của biểu đồ */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-5 border-b border-slate-100">
              <div>
                <h2 className="text-lg font-extrabold text-slate-900 tracking-tight">
                  {metricMode === 'revenue' ? 'Doanh số 7 ngày gần đây' : 'Lượt xe 7 ngày gần đây'}
                </h2>
                <div className="flex items-center gap-2 mt-0.5">
                  <span className="text-xs text-slate-500">So với 7 ngày trước đó:</span>
                  <span className="text-xs font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-md inline-flex items-center gap-0.5">
                    <span className="material-symbols-outlined text-[14px]">trending_up</span>
                    +{revGrowth}% ({metricMode === 'revenue' ? `+${(totalCurrentRev - totalPrevRev).toFixed(1)} Tr` : '+182 lượt'})
                  </span>
                </div>
              </div>

              {/* Nhóm nút chọn kiểu hiển thị */}
              <div className="flex flex-wrap items-center gap-1.5">
                <button
                  type="button"
                  onClick={() => setMetricMode('revenue')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                    metricMode === 'revenue'
                      ? 'bg-indigo-600 text-white shadow-xs'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  Doanh số
                </button>
                <button
                  type="button"
                  onClick={() => setMetricMode('traffic')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                    metricMode === 'traffic'
                      ? 'bg-indigo-600 text-white shadow-xs'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  Lượt xe
                </button>
                <button
                  type="button"
                  onClick={() => setPeriodMode(periodMode === 'week' ? 'month' : 'week')}
                  className="px-3 py-1.5 rounded-lg text-xs font-bold bg-indigo-50 text-indigo-700 hover:bg-indigo-100 transition-colors flex items-center gap-1 cursor-pointer"
                >
                  <span>{periodMode === 'week' ? 'Tuần' : 'Tháng'}</span>
                  <span className="material-symbols-outlined text-[14px]">expand_more</span>
                </button>
              </div>
            </div>

            {/* Chú thích màu sắc & Chọn bãi xe */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-500 mt-4 px-1">
              <div className="flex items-center gap-4">
                <div className="flex items-center gap-1.5">
                  <span className="w-3 h-3 rounded-full bg-indigo-600"></span>
                  <span className="font-semibold text-slate-800">7 ngày gần đây</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="w-4 h-0.5 bg-slate-400 border-dashed"></span>
                  <span className="text-slate-500">7 ngày trước đó</span>
                </div>
              </div>

              <div className="flex items-center gap-1.5 text-xs text-slate-600 bg-slate-50 px-2.5 py-1 rounded-lg border border-slate-200">
                <span className="text-slate-400 text-[11px] font-medium">Bãi xe:</span>
                <select
                  value={selectedLot}
                  onChange={(e) => setSelectedLot(e.target.value as 'all' | 'landmark' | 'bitexco')}
                  className="text-xs text-slate-800 font-bold bg-transparent border-none focus:outline-hidden cursor-pointer"
                >
                  <option value="all">Tất cả các bãi (02 bãi)</option>
                  <option value="landmark">Landmark 81 Central</option>
                  <option value="bitexco">Bitexco Garage</option>
                </select>
              </div>
            </div>

            {/* CANVAS BIỂU ĐỒ MIỀN SVG */}
            <div className="relative w-full h-[250px] mt-4 select-none overflow-hidden rounded-xl">
              <svg className="w-full h-full" viewBox="0 0 660 235" preserveAspectRatio="none">
                <defs>
                  {/* Gradient miền phủ kỳ hiện tại */}
                  <linearGradient id="currentAreaGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#4f46e5" stopOpacity="0.50" />
                    <stop offset="50%" stopColor="#06b6d4" stopOpacity="0.25" />
                    <stop offset="100%" stopColor="#10b981" stopOpacity="0.04" />
                  </linearGradient>

                  {/* Gradient miền phủ kỳ trước */}
                  <linearGradient id="prevAreaGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#94a3b8" stopOpacity="0.20" />
                    <stop offset="100%" stopColor="#f1f5f9" stopOpacity="0.02" />
                  </linearGradient>

                  {/* Gradient nét vẽ */}
                  <linearGradient id="strokeGradient" x1="0" y1="0" x2="1" y2="0">
                    <stop offset="0%" stopColor="#4f46e5" />
                    <stop offset="50%" stopColor="#6366f1" />
                    <stop offset="100%" stopColor="#06b6d4" />
                  </linearGradient>
                </defs>

                {/* Các đường gióng ngang */}
                <line x1="30" y1="55" x2="630" y2="55" stroke="#f1f5f9" strokeWidth="1" />
                <line x1="30" y1="105" x2="630" y2="105" stroke="#f1f5f9" strokeWidth="1" />
                <line x1="30" y1="150" x2="630" y2="150" stroke="#f1f5f9" strokeWidth="1" />
                <line x1="30" y1={baselineY} x2="630" y2={baselineY} stroke="#e2e8f0" strokeWidth="1.5" />

                {/* 1. LỚP MIỀN PHỦ KỲ TRƯỚC */}
                <path d={prevAreaD} fill="url(#prevAreaGrad)" />
                <path
                  d={prevLineD}
                  fill="none"
                  stroke="#94a3b8"
                  strokeWidth="2"
                  strokeDasharray="4 4"
                />

                {/* 2. LỚP MIỀN PHỦ CHÍNH */}
                <path d={currentAreaD} fill="url(#currentAreaGrad)" />
                <path
                  d={currentLineD}
                  fill="none"
                  stroke="url(#strokeGradient)"
                  strokeWidth="3.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />

                {/* 3. CÁC ĐIỂM CHẤM VÀ NHÃN */}
                {currentPoints.map((p, idx) => {
                  const isHovered = hoveredIndex === idx;
                  const isToday = idx === 6;

                  return (
                    <g
                      key={idx}
                      className="cursor-pointer transition-all duration-200"
                      onMouseEnter={() => setHoveredIndex(idx)}
                      onClick={() => setHoveredIndex(idx)}
                    >
                      {/* Cột sáng dọc khi hover */}
                      {isHovered && (
                        <line
                          x1={p.x}
                          y1="55"
                          x2={p.x}
                          y2={baselineY}
                          stroke="#6366f1"
                          strokeWidth="1.5"
                          strokeDasharray="3 3"
                          opacity="0.8"
                        />
                      )}

                      {/* Quầng sáng nhẹ nhàng cố định (không dùng animate-ping để tránh tràn lề) */}
                      {isToday && (
                        <circle cx={p.x} cy={p.y} r="9" fill="#4f46e5" opacity="0.25" />
                      )}

                      {/* Điểm nút tròn */}
                      <circle
                        cx={p.x}
                        cy={p.y}
                        r={isHovered ? '6.5' : isToday ? '5.5' : '4.5'}
                        fill="#4f46e5"
                        stroke="#ffffff"
                        strokeWidth={isHovered ? '2.5' : '2'}
                        className="transition-all duration-150"
                      />

                      {/* Nhãn giá trị trực tiếp trên đỉnh điểm */}
                      <text
                        x={p.x}
                        y={p.y - 10}
                        textAnchor="middle"
                        className={`text-[11px] font-mono font-extrabold transition-all select-none ${
                          isHovered || isToday ? 'fill-indigo-900 font-black' : 'fill-slate-700'
                        }`}
                      >
                        {p.label}
                      </text>

                      {/* Nhãn Thứ và Ngày */}
                      <text
                        x={p.x}
                        y="212"
                        textAnchor="middle"
                        className={`text-xs font-bold transition-colors select-none ${
                          isHovered ? 'fill-indigo-700' : 'fill-slate-600'
                        }`}
                      >
                        {p.data.day}
                      </text>
                      <text
                        x={p.x}
                        y="226"
                        textAnchor="middle"
                        className="text-[10px] font-mono fill-slate-400 select-none"
                      >
                        {p.data.date}
                      </text>
                    </g>
                  );
                })}
              </svg>
            </div>
          </div>

          {/* Hộp chi tiết khi hover / click chọn ngày */}
          <div className="mt-4 p-4 rounded-xl bg-indigo-50/70 border border-indigo-100 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center font-bold text-sm shadow-xs shrink-0">
                {activeStat.day}
              </div>
              <div>
                <div className="text-xs font-semibold text-slate-500">
                  Chi tiết ngày <span className="font-bold text-slate-900">{activeStat.date}</span>:
                </div>
                <div className="text-lg font-extrabold text-indigo-900">
                  {activeStat.currentRevenue.toFixed(1)} Triệu VNĐ ({activeStat.currentTraffic} lượt xe)
                </div>
              </div>
            </div>

            <div className="flex items-center gap-6 text-xs">
              <div>
                <span className="text-slate-400 block font-medium">So với tuần trước:</span>
                <span className="font-extrabold text-emerald-600 text-sm">
                  +{(activeStat.currentRevenue - activeStat.prevRevenue).toFixed(1)} Tr (+
                  {(((activeStat.currentRevenue - activeStat.prevRevenue) / activeStat.prevRevenue) * 100).toFixed(0)}%)
                </span>
              </div>
              <div>
                <span className="text-slate-400 block font-medium">Công suất đỉnh:</span>
                <span className="font-extrabold text-blue-600 text-sm">{activeStat.occupancy}% Lấp đầy</span>
              </div>
            </div>
          </div>
        </div>

        {/* CỘT PHẢI (1/3): SƠ ĐỒ DONUT CƠ CẤU DOANH THU & NGUỒN THU */}
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between overflow-hidden">
          <div>
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
                <span className="material-symbols-outlined text-indigo-600">donut_large</span>
                Cơ cấu Nguồn thu
              </h3>
              <span className="text-[11px] font-bold text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded">Tháng 10</span>
            </div>

            {/* Sơ đồ hình tròn SVG Donut Chart */}
            <div className="relative flex items-center justify-center my-6">
              <svg className="w-48 h-48 -rotate-90" viewBox="0 0 100 100">
                <circle cx="50" cy="50" r="38" stroke="#f1f5f9" strokeWidth="12" fill="none" />

                {/* 1. Vé vãng lai theo giờ (58%) */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  stroke="#4f46e5"
                  strokeWidth="12"
                  strokeDasharray="138.48 238.76"
                  strokeDashoffset="0"
                  fill="none"
                />

                {/* 2. Vé tháng / Thuê bao (28%) */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  stroke="#10b981"
                  strokeWidth="12"
                  strokeDasharray="66.85 238.76"
                  strokeDashoffset="-138.48"
                  fill="none"
                />

                {/* 3. Trạm sạc xe điện EV (9%) */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  stroke="#06b6d4"
                  strokeWidth="12"
                  strokeDasharray="21.48 238.76"
                  strokeDashoffset="-205.33"
                  fill="none"
                />

                {/* 4. Phụ thu qua đêm (5%) */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  stroke="#f59e0b"
                  strokeWidth="12"
                  strokeDasharray="11.95 238.76"
                  strokeDashoffset="-226.81"
                  fill="none"
                />
              </svg>

              <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
                <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Tổng thu</span>
                <span className="text-xl font-extrabold text-slate-900 tracking-tight">312.4 Tr</span>
                <span className="text-[10px] text-emerald-600 font-bold">89% Chỉ tiêu</span>
              </div>
            </div>

            {/* Chú giải các phần cơ cấu */}
            <div className="space-y-2.5">
              <div className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-indigo-600 shrink-0"></span>
                  <span className="text-slate-700 font-medium">Vé vãng lai theo giờ</span>
                </div>
                <div className="text-right">
                  <span className="font-extrabold text-slate-900">58%</span>
                  <span className="text-slate-400 text-[11px] ml-1.5">(181.2 Tr)</span>
                </div>
              </div>

              <div className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-emerald-500 shrink-0"></span>
                  <span className="text-slate-700 font-medium">Vé tháng / Thuê bao</span>
                </div>
                <div className="text-right">
                  <span className="font-extrabold text-slate-900">28%</span>
                  <span className="text-slate-400 text-[11px] ml-1.5">(87.5 Tr)</span>
                </div>
              </div>

              <div className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-cyan-500 shrink-0"></span>
                  <span className="text-slate-700 font-medium">Trạm sạc xe điện EV</span>
                </div>
                <div className="text-right">
                  <span className="font-extrabold text-slate-900">9%</span>
                  <span className="text-slate-400 text-[11px] ml-1.5">(28.1 Tr)</span>
                </div>
              </div>

              <div className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-amber-500 shrink-0"></span>
                  <span className="text-slate-700 font-medium">Phụ thu đêm & Lễ tết</span>
                </div>
                <div className="text-right">
                  <span className="font-extrabold text-slate-900">5%</span>
                  <span className="text-slate-400 text-[11px] ml-1.5">(15.6 Tr)</span>
                </div>
              </div>
            </div>
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-400">
            <span>Hệ thống đối soát tự động ANPR & VietQR</span>
            <span className="font-semibold text-indigo-600">Chi tiết đối soát →</span>
          </div>
        </div>
      </div>

      {/* ===== KHỐI 3: LƯU LƯỢNG GIỜ CAO ĐIỂM TRONG NGÀY & DOANH THU THEO TỪNG BÃI ===== */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mt-8">
        {/* LƯU LƯỢNG GIỜ CAO ĐIỂM (PEAK HOURS TRAFFIC) */}
        <div className="lg:col-span-2 p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between mb-2">
            <div>
              <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600">schedule</span>
                Phân bổ Lưu lượng & Doanh thu theo Khung giờ (24h)
              </h3>
              <p className="text-xs text-slate-500 mt-0.5">
                Nhận diện khung giờ cao điểm để áp dụng chính sách giá linh hoạt (Dynamic Pricing)
              </p>
            </div>
            <div className="flex items-center gap-3 text-[11px]">
              <span className="flex items-center gap-1 text-slate-500">
                <span className="w-2 h-2 rounded-full bg-blue-500"></span> Bình thường
              </span>
              <span className="flex items-center gap-1 text-amber-600 font-bold">
                <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse"></span> Cao điểm (&gt;90%)
              </span>
            </div>
          </div>

          <div className="mt-6 pt-2">
            <div className="h-32 flex items-end justify-between gap-1 sm:gap-1.5">
              {HOURLY_TRAFFIC.map((h) => {
                const isPeak = h.pct >= 90;
                return (
                  <div key={h.hour} className="flex-1 flex flex-col items-center h-full justify-end group">
                    <div
                      style={{ height: `${h.pct}%` }}
                      className={`w-full rounded-t-sm transition-all group-hover:opacity-80 ${
                        isPeak
                          ? 'bg-gradient-to-t from-red-600 to-amber-500 shadow-xs'
                          : h.pct > 70
                            ? 'bg-gradient-to-t from-indigo-600 to-blue-400'
                            : 'bg-gradient-to-t from-slate-300 to-blue-200'
                      }`}
                    ></div>
                    <span className="text-[9px] font-mono text-slate-400 mt-1">{h.hour}h</span>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500 gap-2">
            <div>
              <strong className="text-slate-800">Khung giờ vàng:</strong> 08:00 - 09:30 (Đạt 95% lấp đầy) và 17:00 - 18:30 (Đạt 93% lấp đầy).
            </div>
            <span className="text-indigo-600 font-semibold cursor-pointer hover:underline">
              Cấu hình phụ thu giờ cao điểm →
            </span>
          </div>
        </div>

        {/* BẢNG SO SÁNH DOANH THU 2 BÃI XE */}
        <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <h3 className="font-bold text-slate-900 text-sm">Hiệu suất từng Bãi đỗ xe</h3>
            <span className="text-xs font-mono text-slate-400">Hôm nay</span>
          </div>

          <div className="space-y-4">
            {/* Bãi 1 */}
            <div className="p-3.5 rounded-xl border border-indigo-100 bg-indigo-50/40">
              <div className="flex items-center justify-between text-xs font-bold text-slate-900 mb-1">
                <span className="truncate">Landmark 81 Central</span>
                <span className="text-indigo-700 shrink-0">9.650.000 đ</span>
              </div>
              <div className="flex items-center justify-between text-[11px] text-slate-500 mb-2">
                <span>112 slots • 93% Lấp đầy</span>
                <span className="text-emerald-600 font-semibold">RevPAS: 86.100 đ/ô</span>
              </div>
              <div className="w-full bg-slate-200 rounded-full h-2 overflow-hidden">
                <div className="bg-indigo-600 h-2 rounded-full" style={{ width: '93%' }}></div>
              </div>
            </div>

            {/* Bãi 2 */}
            <div className="p-3.5 rounded-xl border border-slate-200 bg-slate-50/60">
              <div className="flex items-center justify-between text-xs font-bold text-slate-900 mb-1">
                <span className="truncate">Bitexco Financial Garage</span>
                <span className="text-indigo-700 shrink-0">5.200.000 đ</span>
              </div>
              <div className="flex items-center justify-between text-[11px] text-slate-500 mb-2">
                <span>56 slots • 70% Lấp đầy</span>
                <span className="text-blue-600 font-semibold">RevPAS: 65.000 đ/ô</span>
              </div>
              <div className="w-full bg-slate-200 rounded-full h-2 overflow-hidden">
                <div className="bg-indigo-500 h-2 rounded-full" style={{ width: '70%' }}></div>
              </div>
            </div>
          </div>

          <div className="mt-4 pt-3 border-t border-slate-100 text-[11px] text-slate-500 flex items-center justify-between">
            <span>Thanh toán không tiền mặt:</span>
            <span className="font-extrabold text-emerald-600">100% Cashless</span>
          </div>
        </div>
      </div>
    </div>
  );
};
