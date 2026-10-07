import React from 'react';
import { useAppDispatch, useAppSelector } from '@/store';
import { makeDefaultVehicle, deleteExistingVehicle } from '@/store/slices/vehicleSlice';
import { VehicleType, FuelType, type VehicleDto } from '@/types/vehicle';

interface VehicleCardProps {
  vehicle: VehicleDto;
  onEdit: (vehicle: VehicleDto) => void;
}

export const VehicleCard: React.FC<VehicleCardProps> = ({ vehicle, onEdit }) => {
  const dispatch = useAppDispatch();
  const { user } = useAppSelector((state) => state.auth);

  const isEv =
    vehicle.fuelType === 'Electric' ||
    vehicle.fuelType === 'PlugInHybrid' ||
    vehicle.fuelType === 2 ||
    vehicle.fuelType === 4;

  const isOversized =
    (vehicle.heightCm && vehicle.heightCm > 200) ||
    (vehicle.lengthCm && vehicle.lengthCm > 520) ||
    (vehicle.widthCm && vehicle.widthCm > 205) ||
    vehicle.vehicleType === 'Van' ||
    vehicle.vehicleType === 3 ||
    vehicle.vehicleType === 'Pickup' ||
    vehicle.vehicleType === 4;

  const getVehicleTypeName = (type: VehicleType) => {
    const t = String(type).toLowerCase();
    if (t === 'sedan' || t === '0') return 'Sedan (4 chỗ)';
    if (t === 'suv' || t === '1') return 'SUV (5-7 chỗ)';
    if (t === 'hatchback' || t === '2') return 'Hatchback';
    if (t === 'van' || t === '3') return 'Xe Van / 16 chỗ';
    if (t === 'pickup' || t === '4') return 'Bán tải (Pickup)';
    if (t === 'motorbike' || t === '5') return 'Xe máy';
    return 'Ô tô';
  };

  const getFuelTypeName = (fuel: FuelType) => {
    const f = String(fuel).toLowerCase();
    if (f === 'gasoline' || f === '0') return 'Xăng (RON 95)';
    if (f === 'diesel' || f === '1') return 'Dầu Diesel';
    if (f === 'electric' || f === '2') return 'Thuần điện (EV)';
    if (f === 'hybrid' || f === '3') return 'Hybrid (HEV)';
    if (f === 'pluginhybrid' || f === '4') return 'Plug-in Hybrid (PHEV)';
    return 'Xăng';
  };

  const handleSetDefault = () => {
    if (!vehicle.isDefault && user) {
      dispatch(makeDefaultVehicle({ vehicleId: vehicle.id, userId: user.id }));
    }
  };

  const handleDelete = () => {
    if (user && window.confirm(`Bạn có chắc chắn muốn xóa xe ${vehicle.plateDisplay} khỏi Garage không?`)) {
      dispatch(deleteExistingVehicle({ vehicleId: vehicle.id, userId: user.id }));
    }
  };

  // Image fallback based on car type / fuel
  const carImage = isEv
    ? 'https://images.unsplash.com/photo-1560958089-b8a1929cea89?auto=format&fit=crop&w=600&q=80'
    : vehicle.vehicleType === VehicleType.Suv
    ? 'https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?auto=format&fit=crop&w=600&q=80'
    : 'https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=600&q=80';

  return (
    <div className="flex flex-col bg-white rounded-2xl border border-slate-200 shadow-xs overflow-hidden transition-all duration-300 hover:shadow-xl hover:-translate-y-0.5">
      {/* Card Header Media */}
      <div className="relative w-full h-48 bg-slate-100 overflow-hidden">
        <img
          alt={`${vehicle.brand || ''} ${vehicle.model || ''}`}
          className="w-full h-full object-cover transition-transform duration-500 hover:scale-105"
          src={carImage}
        />
        <div className="absolute inset-0 bg-gradient-to-t from-slate-950/80 via-transparent to-black/30"></div>

        {/* Top Badges */}
        <div className="absolute top-3 left-3 right-3 flex items-center justify-between">
          <div className="flex items-center gap-1.5 flex-wrap">
            {vehicle.isDefault && (
              <span className="px-2.5 py-1 rounded-full bg-blue-600 text-white text-[11px] font-bold flex items-center gap-1 shadow-xs">
                <span className="material-symbols-outlined text-[13px]">star</span> Xe mặc định
              </span>
            )}
            {isEv && (
              <span className="px-2.5 py-1 rounded-full bg-emerald-600 text-white text-[11px] font-bold flex items-center gap-1 shadow-xs">
                <span className="material-symbols-outlined text-[13px]">bolt</span> EV Thông minh
              </span>
            )}
            {isOversized && (
              <span className="px-2 py-0.5 rounded-full bg-amber-500 text-white text-[10px] font-bold shadow-xs">
                Xe quá khổ (1.5x)
              </span>
            )}
          </div>

          <span className="px-2 py-0.5 rounded-full bg-white/20 backdrop-blur-md text-white text-[11px] font-medium">
            Ngoài bãi
          </span>
        </div>

        {/* Bottom Tag */}
        <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between text-white text-xs">
          <span className="font-semibold truncate">
            {vehicle.brand || 'Ô tô'} {vehicle.model || ''}
          </span>
          <span className="px-2 py-0.5 rounded bg-black/40 backdrop-blur-md text-[11px]">
            Cao: {vehicle.heightCm} cm
          </span>
        </div>
      </div>

      {/* Card Body */}
      <div className="p-5 flex flex-col flex-1 justify-between gap-4">
        <div className="flex flex-col gap-3">
          <div className="flex items-start justify-between">
            <div>
              <h3 className="font-bold text-slate-900 text-lg">
                {vehicle.brand ? `${vehicle.brand} ${vehicle.model || ''}` : 'Phương tiện cá nhân'}
              </h3>
              <p className="text-xs text-slate-500">{getVehicleTypeName(vehicle.vehicleType)}</p>
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={() => onEdit(vehicle)}
                title="Chỉnh sửa xe (US-011)"
                className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-slate-100 rounded-lg transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">edit</span>
              </button>
              <button
                onClick={handleDelete}
                title="Xóa xe khỏi Garage (US-011)"
                className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">delete</span>
              </button>
            </div>
          </div>

          {/* Embossed Vietnam License Plate */}
          <div className="py-1 flex justify-center">
            <div className="w-full px-4 py-2 rounded-xl bg-slate-50 border border-slate-200 shadow-inner flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <div className="w-4 h-3 bg-red-600 rounded-xs flex items-center justify-center shadow-xs">
                  <span className="w-1.5 h-1.5 bg-yellow-400 rounded-full"></span>
                </div>
                <span className="text-[11px] text-slate-500 font-extrabold tracking-wider">VN</span>
              </div>
              <span className="text-xl tracking-widest font-black font-mono text-slate-900">
                {vehicle.plateDisplay || vehicle.plateNumber}
              </span>
              <span className="material-symbols-outlined text-[18px] text-emerald-600" title="Đã đồng bộ ANPR">
                verified
              </span>
            </div>
          </div>

          {/* EV Battery Gauge (US-012) */}
          {isEv && (
            <div className="p-3 rounded-xl bg-emerald-50/60 border border-emerald-100 flex flex-col gap-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="text-emerald-800 font-semibold flex items-center gap-1">
                  <span className="material-symbols-outlined text-[16px] text-emerald-600">battery_charging_full</span>
                  Dung lượng Pin
                </span>
                <span className="font-bold text-emerald-700">85% • ~360 km</span>
              </div>
              <div className="w-full h-2 rounded-full bg-emerald-200/60 overflow-hidden">
                <div className="h-full bg-emerald-500 rounded-full" style={{ width: '85%' }}></div>
              </div>
              <span className="text-[10px] text-emerald-600">Hỗ trợ trạm sạc EV_CHARGING_SLOT</span>
            </div>
          )}

          {/* Specs Grid */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="p-2.5 rounded-xl bg-slate-50 border border-slate-100">
              <span className="text-[10px] text-slate-400 block uppercase font-medium">Nhiên liệu</span>
              <span className="font-semibold text-slate-800 truncate block mt-0.5">
                {getFuelTypeName(vehicle.fuelType)}
              </span>
            </div>
            <div className="p-2.5 rounded-xl bg-slate-50 border border-slate-100">
              <span className="text-[10px] text-slate-400 block uppercase font-medium">Màu ngoại thất</span>
              <span className="font-semibold text-slate-800 truncate block mt-0.5">
                {vehicle.color || 'Chưa cập nhật'}
              </span>
            </div>
          </div>
        </div>

        {/* Bottom Actions */}
        <div className="pt-2 flex items-center gap-2 border-t border-slate-100">
          {!vehicle.isDefault ? (
            <button
              onClick={handleSetDefault}
              type="button"
              className="flex-1 py-2 px-3 rounded-xl bg-slate-100 hover:bg-blue-50 hover:text-blue-700 text-slate-700 text-xs font-bold transition-colors flex items-center justify-center gap-1.5"
            >
              <span className="material-symbols-outlined text-[16px]">star_border</span>
              Đặt làm mặc định
            </button>
          ) : (
            <div className="flex-1 py-2 px-3 rounded-xl bg-blue-50 text-blue-700 text-xs font-bold flex items-center justify-center gap-1.5">
              <span className="material-symbols-outlined text-[16px]">check_circle</span>
              Xe mặc định ưu tiên
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
