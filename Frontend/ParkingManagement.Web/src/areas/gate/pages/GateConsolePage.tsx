import React from 'react';

export const GateConsolePage: React.FC = () => {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-amber-100 text-amber-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🚧 Cổng Soát Vé (Gate Console)
          </div>
          <h1 className="text-3xl font-bold text-slate-900 tracking-tight">Điều khiển Cổng Barie & Soát vé</h1>
          <p className="text-slate-500 text-sm mt-1">
            Giao diện dành cho nhân viên trực cổng: Quét mã QR booking, nhận diện biển số ANPR, nhập biển số tay và mở barie Check-in / Check-out.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
          <span className="text-xs font-semibold text-emerald-700 bg-emerald-50 px-3 py-1.5 rounded-lg border border-emerald-200">
            BARIE GATE-01: ONLINE (0.3s LATENCY)
          </span>
        </div>
      </div>

      {/* 2-Column Console Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mt-8">
        {/* Left: Check-in */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">login</span>
              LÀN VÀO (INBOUND CHECK-IN)
            </h2>
            <span className="text-xs font-semibold text-slate-400">CAMERA ANPR #1</span>
          </div>

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              REC
            </div>
          </div>

          <div className="mt-4 flex gap-2">
            <input
              type="text"
              placeholder="Nhập biển số hoặc mã QR (VD: 51H-123.45)..."
              className="flex-1 px-4 py-2 text-sm border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono"
            />
            <button className="px-5 py-2 bg-emerald-600 text-white rounded-xl text-sm font-bold hover:bg-emerald-700 shadow-xs transition-colors flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[18px]">lock_open</span>
              CHECK-IN
            </button>
          </div>
        </div>

        {/* Right: Check-out */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-amber-600">logout</span>
              LÀN RA (OUTBOUND CHECK-OUT)
            </h2>
            <span className="text-xs font-semibold text-slate-400">CAMERA ANPR #2</span>
          </div>

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              REC
            </div>
          </div>

          <div className="mt-4 flex gap-2">
            <input
              type="text"
              placeholder="Nhập biển số hoặc quét mã vé xuất..."
              className="flex-1 px-4 py-2 text-sm border border-slate-300 rounded-xl focus:outline-hidden focus:border-amber-500 font-mono"
            />
            <button className="px-5 py-2 bg-amber-600 text-white rounded-xl text-sm font-bold hover:bg-amber-700 shadow-xs transition-colors flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[18px]">receipt_long</span>
              CHECK-OUT
            </button>
          </div>
        </div>
      </div>

      <div className="mt-8 p-6 bg-slate-100 rounded-2xl border border-slate-200 text-center">
        <h3 className="text-sm font-bold text-slate-800">Đã sẵn sàng cho TV7 (GateService - T-701, T-702, T-704)</h3>
        <p className="text-xs text-slate-500 mt-1">
          Giao diện phân hệ soát vé đã chuẩn bị sẵn để TV7 kết nối các endpoint Check-in / Check-out, quét QR HMAC và heartbeat thiết bị.
        </p>
      </div>
    </div>
  );
};
