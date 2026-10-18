import React, { useState } from 'react';

interface GateEvent {
  id: string;
  plate: string;
  lane: 'Làn vào #1' | 'Làn ra #2';
  type: 'Check-in' | 'Check-out';
  time: string;
  status: 'Đã mở Barie' | 'Khớp vé QR' | 'Cần kiểm tra';
  statusColor: string;
}

const INITIAL_EVENTS: GateEvent[] = [
  { id: 'EV-1092', plate: '51H-123.45', lane: 'Làn vào #1', type: 'Check-in', time: '11:42:15', status: 'Đã mở Barie', statusColor: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  { id: 'EV-1091', plate: '30F-987.65', lane: 'Làn ra #2', type: 'Check-out', time: '11:39:04', status: 'Khớp vé QR', statusColor: 'bg-blue-50 text-blue-700 border-blue-200' },
  { id: 'EV-1090', plate: '60A-456.78', lane: 'Làn vào #1', type: 'Check-in', time: '11:35:50', status: 'Đã mở Barie', statusColor: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  { id: 'EV-1089', plate: '43C-333.22', lane: 'Làn ra #2', type: 'Check-out', time: '11:28:11', status: 'Khớp vé QR', statusColor: 'bg-blue-50 text-blue-700 border-blue-200' },
];

export const GateConsolePage: React.FC = () => {
  const [inboundPlate, setInboundPlate] = useState('');
  const [outboundPlate, setOutboundPlate] = useState('');
  const [events, setEvents] = useState<GateEvent[]>(INITIAL_EVENTS);
  const [alertNotice, setAlertNotice] = useState<string | null>(null);

  const showAlert = (msg: string) => {
    setAlertNotice(msg);
    setTimeout(() => setAlertNotice(null), 3500);
  };

  const handleInboundCheckIn = () => {
    const plate = inboundPlate.trim() || '51K-888.99';
    const now = new Date();
    const timeStr = now.toTimeString().split(' ')[0];
    const newEv: GateEvent = {
      id: `EV-${Math.floor(1000 + Math.random() * 9000)}`,
      plate: plate.toUpperCase(),
      lane: 'Làn vào #1',
      type: 'Check-in',
      time: timeStr,
      status: 'Đã mở Barie',
      statusColor: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    };
    setEvents([newEv, ...events]);
    setInboundPlate('');
    showAlert(`Làn vào #1: Đã xác thực xe ${plate.toUpperCase()} và kích hoạt nâng cần Barie!`);
  };

  const handleOutboundCheckOut = () => {
    const plate = outboundPlate.trim() || '30G-666.88';
    const now = new Date();
    const timeStr = now.toTimeString().split(' ')[0];
    const newEv: GateEvent = {
      id: `EV-${Math.floor(1000 + Math.random() * 9000)}`,
      plate: plate.toUpperCase(),
      lane: 'Làn ra #2',
      type: 'Check-out',
      time: timeStr,
      status: 'Khớp vé QR',
      statusColor: 'bg-blue-50 text-blue-700 border-blue-200',
    };
    setEvents([newEv, ...events]);
    setOutboundPlate('');
    showAlert(`Làn ra #2: Đã đối soát thanh toán xe ${plate.toUpperCase()} và mở cần Barie xuất bãi!`);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {/* Toast Notice */}
      {alertNotice && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 bg-slate-900 text-white rounded-xl shadow-xl text-sm font-semibold animate-bounce">
          <span className="material-symbols-outlined text-emerald-400 text-[20px]">check_circle</span>
          <span>{alertNotice}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-amber-100 text-amber-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🚧 Bàn Điều Khiển Cổng & Barie Tự Động (Gate Console)
          </div>
          <h1 className="text-2xl md:text-3xl font-extrabold text-slate-900 tracking-tight">
            Điều khiển Cổng Barie & Giám sát ANPR
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Giao diện trực ban soát vé: Quét mã QR đặt chỗ, camera quang học nhận diện biển số ANPR và điều khiển relay nâng hạ Barie thời gian thực.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
          <span className="text-xs font-bold text-emerald-700 bg-emerald-50 px-3 py-1.5 rounded-lg border border-emerald-200">
            BARIE CONTROLLER: ONLINE (0.3s LATENCY)
          </span>
        </div>
      </div>

      {/* 2-Column Console Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Check-in */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">login</span>
              LÀN VÀO (INBOUND CHECK-IN #01)
            </h2>
            <span className="text-xs font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
              CAMERA ANPR SẴN SÀNG
            </span>
          </div>

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Video Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              LIVE REC
            </div>
            <div className="absolute bottom-2 right-2 text-[10px] text-slate-400 font-mono">
              FPS: 30 • 1080p
            </div>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              value={inboundPlate}
              onChange={(e) => setInboundPlate(e.target.value)}
              placeholder="Nhập biển số hoặc mã QR (VD: 51H-123.45)..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono uppercase"
            />
            <button
              onClick={handleInboundCheckIn}
              className="px-5 py-2.5 bg-emerald-600 text-white rounded-xl text-xs font-bold hover:bg-emerald-700 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer shrink-0"
            >
              <span className="material-symbols-outlined text-[18px]">lock_open</span>
              MỞ BARIE VÀO
            </button>
          </div>
        </div>

        {/* Right: Check-out */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-amber-600">logout</span>
              LÀN RA (OUTBOUND CHECK-OUT #02)
            </h2>
            <span className="text-xs font-bold text-amber-600 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
              CAMERA ANPR SẴN SÀNG
            </span>
          </div>

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Video Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              LIVE REC
            </div>
            <div className="absolute bottom-2 right-2 text-[10px] text-slate-400 font-mono">
              FPS: 30 • 1080p
            </div>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              value={outboundPlate}
              onChange={(e) => setOutboundPlate(e.target.value)}
              placeholder="Nhập biển số hoặc quét mã vé xuất..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-amber-500 font-mono uppercase"
            />
            <button
              onClick={handleOutboundCheckOut}
              className="px-5 py-2.5 bg-amber-600 text-white rounded-xl text-xs font-bold hover:bg-amber-700 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer shrink-0"
            >
              <span className="material-symbols-outlined text-[18px]">receipt_long</span>
              MỞ BARIE RA
            </button>
          </div>
        </div>
      </div>

      {/* Live Event Passage Feed */}
      <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">history</span>
              Nhật ký Lượt xe Qua Cổng Thời Gian Thực (Live Gate Events)
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">Tự động ghi nhận nhật ký vào/ra, trạng thái đối soát vé và lệnh relay Barie</p>
          </div>
          <span className="text-xs font-semibold text-slate-400 font-mono">Cổng trạm Vincom Mega Mall</span>
        </div>

        <div className="overflow-x-auto border border-slate-200 rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
              <tr>
                <th className="px-4 py-3">Mã sự kiện</th>
                <th className="px-4 py-3">Biển số nhận diện</th>
                <th className="px-4 py-3">Làn điều khiển</th>
                <th className="px-4 py-3">Phân loại</th>
                <th className="px-4 py-3">Thời gian ghi nhận</th>
                <th className="px-4 py-3">Trạng thái Barie</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {events.map((ev) => (
                <tr key={ev.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-4 py-3 font-mono text-slate-500">{ev.id}</td>
                  <td className="px-4 py-3">
                    <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                      {ev.plate}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-700 font-medium">{ev.lane}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                      ev.type === 'Check-in' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                    }`}>
                      {ev.type}
                    </span>
                  </td>
                  <td className="px-4 py-3 font-mono text-slate-500">{ev.time}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-md text-[10px] font-bold border ${ev.statusColor}`}>
                      {ev.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
