import React, { useEffect, useState } from 'react';
import { useAppDispatch, useAppSelector } from '@/store';
import { fetchVehicles, clearMessages } from '@/store/slices/vehicleSlice';
import { VehicleCard } from '../components/VehicleCard';
import { AddVehicleModal } from '../components/AddVehicleModal';
import { EditVehicleModal } from '../components/EditVehicleModal';
import type { VehicleDto } from '@/types/vehicle';

export const VehiclesPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const { user } = useAppSelector((state) => state.auth);
  const { vehicles, isLoading, error, successMessage } = useAppSelector((state) => state.vehicle);

  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [editingVehicle, setEditingVehicle] = useState<VehicleDto | null>(null);
  const [isRfidModalOpen, setIsRfidModalOpen] = useState(false);

  const userId = user?.id || 5;

  useEffect(() => {
    dispatch(fetchVehicles(userId));
  }, [dispatch, userId]);

  const handleEditVehicle = (v: VehicleDto) => {
    setEditingVehicle(v);
  };

  const evCount = vehicles.filter(
    (v) =>
      v.fuelType === 'Electric' ||
      v.fuelType === 'PlugInHybrid' ||
      v.fuelType === 2 ||
      v.fuelType === 4
  ).length;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* Top Section: Header & Control Row */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            SMART GARAGE V2.4 • HỆ THỐNG ANPR TỰ ĐỘNG NHẬN DIỆN
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Phương tiện của tôi
          </h1>
          <p className="text-slate-500 text-sm leading-relaxed">
            Quản lý danh sách phương tiện cá nhân, tự động mở barie qua nhận diện biển số ANPR tốc độ cao và giám sát sạc điện thông minh trong mạng lưới ParkMaster.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <button
            onClick={() => setIsRfidModalOpen(true)}
            className="px-4 py-2.5 rounded-xl border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 transition-colors text-sm font-semibold flex items-center gap-2 shadow-xs cursor-pointer"
            type="button"
          >
            <span className="material-symbols-outlined text-[18px] text-slate-500">contactless</span>
            <span>Quản lý thẻ RFID & Barie</span>
          </button>
          <button
            onClick={() => dispatch(fetchVehicles(userId))}
            title="Tải lại danh sách"
            className="p-2.5 rounded-xl bg-white border border-slate-200 text-slate-600 hover:bg-slate-50 transition-all shadow-xs cursor-pointer"
          >
            <span className="material-symbols-outlined text-[20px]">refresh</span>
          </button>
          <button
            onClick={() => setIsAddModalOpen(true)}
            disabled={vehicles.length >= 10}
            className="px-5 py-2.5 rounded-xl bg-blue-600 text-white hover:bg-blue-700 transition-all shadow-sm font-semibold text-sm flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
          >
            <span className="material-symbols-outlined text-[20px]">add</span>
            <span>Thêm xe mới</span>
          </button>
        </div>
      </div>

      {/* Alert Banners */}
      {successMessage && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-sm text-emerald-800 flex items-center justify-between animate-in fade-in">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[20px] text-emerald-600">check_circle</span>
            <span>{successMessage}</span>
          </div>
          <button
            onClick={() => dispatch(clearMessages())}
            className="text-emerald-600 hover:text-emerald-800"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>
      )}

      {error && (
        <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-sm text-red-800 flex items-center justify-between animate-in fade-in">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[20px] text-red-600">error</span>
            <span>{error}</span>
          </div>
          <button
            onClick={() => dispatch(clearMessages())}
            className="text-red-600 hover:text-red-800"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>
      )}

      {/* Quick Stats Telemetry Ribbon */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
        {/* Metric 1 */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">Tổng phương tiện</span>
            <div className="w-9 h-9 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">directions_car</span>
            </div>
          </div>
          <div>
            <div className="text-3xl font-extrabold text-slate-900">
              {String(vehicles.length).padStart(2, '0')}{' '}
              <span className="text-sm font-normal text-slate-500">/ 10 xe</span>
            </div>
            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>Giới hạn tối đa 10 xe (US-010)</span>
            </div>
          </div>
        </div>

        {/* Metric 2 */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">Xe điện & Hybrid (EV)</span>
            <div className="w-9 h-9 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">bolt</span>
            </div>
          </div>
          <div>
            <div className="text-3xl font-extrabold text-emerald-600">
              {String(evCount).padStart(2, '0')}{' '}
              <span className="text-sm font-normal text-slate-500">xe EV</span>
            </div>
            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="material-symbols-outlined text-emerald-600 text-[14px]">ev_station</span>
              <span>Đủ điều kiện chọn trụ sạc EV</span>
            </div>
          </div>
        </div>

        {/* Metric 3 */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">Trạng thái ANPR</span>
            <div className="w-9 h-9 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">verified_user</span>
            </div>
          </div>
          <div>
            <div className="text-3xl font-extrabold text-slate-900">100%</div>
            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="material-symbols-outlined text-emerald-600 text-[14px]">check_circle</span>
              <span>Mở barie tự động &lt; 0.3s</span>
            </div>
          </div>
        </div>

        {/* Metric 4 */}
        <div className="p-5 rounded-2xl bg-white border border-slate-200 shadow-xs flex flex-col justify-between gap-3">
          <div className="flex items-center justify-between">
            <span className="text-xs uppercase text-slate-500 font-bold tracking-wider">Ví ParkMaster Pay</span>
            <div className="w-9 h-9 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">account_balance_wallet</span>
            </div>
          </div>
          <div>
            <div className="text-3xl font-extrabold text-slate-900">
              2.450.000 <span className="text-sm font-semibold">đ</span>
            </div>
            <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
              <span className="material-symbols-outlined text-blue-600 text-[14px]">bolt</span>
              <span>Tự động trừ tiền vé khi xuất cổng</span>
            </div>
          </div>
        </div>
      </div>

      {/* Loading Indicator */}
      {isLoading && (
        <div className="py-12 flex flex-col items-center justify-center text-slate-400 gap-3">
          <div className="w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
          <span className="text-sm font-medium">Đang tải danh sách xe từ Garage...</span>
        </div>
      )}

      {/* Vehicles Grid */}
      {!isLoading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {vehicles.map((v) => (
            <VehicleCard key={v.id} vehicle={v} onEdit={handleEditVehicle} />
          ))}

          {/* Add Vehicle Button Card (if not reaching 10 limit) */}
          {vehicles.length < 10 && (
            <div
              onClick={() => setIsAddModalOpen(true)}
              className="border-2 border-dashed border-slate-300 bg-white/50 hover:bg-blue-50/50 hover:border-blue-500 rounded-2xl flex flex-col items-center justify-center min-h-[380px] p-6 text-center cursor-pointer transition-all duration-300 group shadow-xs hover:-translate-y-1"
            >
              <div className="w-14 h-14 text-blue-600 mb-4 bg-blue-100 p-3.5 rounded-2xl flex items-center justify-center group-hover:scale-110 group-hover:bg-blue-600 group-hover:text-white transition-all duration-300 shadow-xs">
                <span className="material-symbols-outlined text-[28px]">add</span>
              </div>
              <h3 className="text-lg font-bold text-slate-700 group-hover:text-blue-600 transition-colors">
                Thêm phương tiện mới
              </h3>
              <p className="text-xs text-slate-500 mt-1 max-w-[200px]">
                Đăng ký xe mới vào hệ thống ({vehicles.length}/10 xe)
              </p>
              <span className="mt-4 inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-semibold bg-white text-slate-600 border border-slate-200 group-hover:border-blue-200 group-hover:text-blue-600 shadow-2xs">
                Đồng bộ ANPR & Quét biển số
              </span>
            </div>
          )}
        </div>
      )}

      {/* Modals */}
      <AddVehicleModal isOpen={isAddModalOpen} onClose={() => setIsAddModalOpen(false)} />
      <EditVehicleModal
        vehicle={editingVehicle}
        isOpen={!!editingVehicle}
        onClose={() => setEditingVehicle(null)}
      />

      {/* RFID & Barie Card Modal */}
      {isRfidModalOpen && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-lg border border-slate-200">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[24px]">contactless</span>
                </div>
                <div>
                  <h3 className="font-bold text-lg text-slate-900">Quản lý thẻ RFID & Barie</h3>
                  <p className="text-xs text-slate-500">Đồng bộ thẻ từ vật lý và mã NFC cho bãi xe không có camera ANPR</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsRfidModalOpen(false)}
                className="text-slate-400 hover:text-slate-700 p-1"
              >
                <span className="material-symbols-outlined text-[22px]">close</span>
              </button>
            </div>

            <div className="my-5 flex flex-col gap-3 text-xs">
              <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
                    NFC
                  </div>
                  <div>
                    <span className="font-bold text-slate-900">Thẻ ParkMaster Virtual Tag</span>
                    <p className="text-slate-500 text-[11px]">Mã ID: PKM-RFID-88932 (Hoạt động tốt)</p>
                  </div>
                </div>
                <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-700 font-bold text-[10px]">
                  Đã liên kết
                </span>
              </div>

              <div className="p-4 rounded-xl bg-blue-50/50 border border-blue-200 text-slate-700 leading-relaxed text-xs">
                <span className="font-bold text-blue-700 block mb-1">Cơ chế hoạt động kép:</span>
                Khi xe đến bãi, hệ thống ANPR tự động đọc biển số xe trước. Trong trường hợp thời tiết xấu hoặc biển số mờ, bạn có thể chạm điện thoại hoặc thẻ từ RFID vào đầu đọc tại barie để mở cổng trong 0.2 giây.
              </div>
            </div>

            <div className="flex justify-end pt-2 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setIsRfidModalOpen(false)}
                className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-xs"
              >
                Đã hiểu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
