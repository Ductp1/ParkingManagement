import React, { useState, useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '@/store';
import { updateExistingVehicle } from '@/store/slices/vehicleSlice';
import { VehicleType, FuelType, type VehicleDto, type UpdateVehicleRequestDto } from '@/types/vehicle';

interface EditVehicleModalProps {
  vehicle: VehicleDto | null;
  isOpen: boolean;
  onClose: () => void;
}

export const EditVehicleModal: React.FC<EditVehicleModalProps> = ({ vehicle, isOpen, onClose }) => {
  const dispatch = useAppDispatch();
  const { user } = useAppSelector((state) => state.auth);
  const { isSubmitting } = useAppSelector((state) => state.vehicle);

  const [vehicleType, setVehicleType] = useState<VehicleType>(VehicleType.Sedan);
  const [fuelType, setFuelType] = useState<FuelType>(FuelType.Gasoline);
  const [brand, setBrand] = useState('');
  const [model, setModel] = useState('');
  const [color, setColor] = useState('');
  const [heightCm, setHeightCm] = useState<number | ''>('');
  const [lengthCm, setLengthCm] = useState<number | ''>('');
  const [widthCm, setWidthCm] = useState<number | ''>('');
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (vehicle) {
      setVehicleType(vehicle.vehicleType);
      setFuelType(vehicle.fuelType);
      setBrand(vehicle.brand || '');
      setModel(vehicle.model || '');
      setColor(vehicle.color || '');
      setHeightCm(vehicle.heightCm || '');
      setLengthCm(vehicle.lengthCm || '');
      setWidthCm(vehicle.widthCm || '');
    }
  }, [vehicle]);

  if (!isOpen || !vehicle) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError(null);

    const payload: UpdateVehicleRequestDto = {
      userId: user?.id || 1,
      vehicleType,
      fuelType,
      brand: brand.trim() || undefined,
      model: model.trim() || undefined,
      color: color.trim() || undefined,
      heightCm: heightCm ? Number(heightCm) : undefined,
      lengthCm: lengthCm ? Number(lengthCm) : undefined,
      widthCm: widthCm ? Number(widthCm) : undefined,
    };

    const result = await dispatch(updateExistingVehicle({ vehicleId: vehicle.id, data: payload }));
    if (updateExistingVehicle.fulfilled.match(result)) {
      onClose();
    } else if (updateExistingVehicle.rejected.match(result)) {
      setValidationError(result.payload as string);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-100 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between pb-4 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <div className="w-9 h-9 rounded-xl bg-amber-100 text-amber-700 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">edit</span>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">Chỉnh sửa xe ({vehicle.plateDisplay})</h2>
              <p className="text-xs text-slate-500">Biển số xe là định danh bất biến (US-011)</p>
            </div>
          </div>
          <button
            onClick={onClose}
            type="button"
            className="text-slate-400 hover:text-slate-600 p-1.5 rounded-lg hover:bg-slate-100"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        {validationError && (
          <div className="mt-4 p-3 rounded-xl bg-red-50 border border-red-200 text-xs text-red-700 flex items-start gap-2">
            <span className="material-symbols-outlined text-[18px] text-red-500 shrink-0">error</span>
            <span>{validationError}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          {/* Biển số (Read-only) */}
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">
              Biển số xe (Bất biến)
            </label>
            <input
              type="text"
              value={vehicle.plateDisplay}
              disabled
              className="w-full px-4 py-2 rounded-xl bg-slate-100 border border-slate-200 font-mono text-base font-bold text-slate-600 cursor-not-allowed"
            />
          </div>

          {/* Loại xe & Nhiên liệu */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Loại xe</label>
              <select
                value={vehicleType}
                onChange={(e) => setVehicleType(e.target.value as VehicleType)}
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              >
                <option value={VehicleType.Sedan}>Sedan (4 chỗ)</option>
                <option value={VehicleType.Suv}>SUV (5-7 chỗ)</option>
                <option value={VehicleType.Hatchback}>Hatchback</option>
                <option value={VehicleType.Van}>Van</option>
                <option value={VehicleType.Pickup}>Bán tải (Pickup)</option>
                <option value={VehicleType.Oversized}>Xe quá khổ (Oversized / Limousine)</option>
                <option value={VehicleType.Motorbike}>Xe máy</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Nhiên liệu (US-012)</label>
              <select
                value={fuelType}
                onChange={(e) => setFuelType(e.target.value as FuelType)}
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              >
                <option value={FuelType.Gasoline}>Xăng</option>
                <option value={FuelType.Diesel}>Dầu Diesel</option>
                <option value={FuelType.Electric}>Điện (EV - Thuần điện)</option>
                <option value={FuelType.Hybrid}>Hybrid (HEV)</option>
                <option value={FuelType.PlugInHybrid}>Plug-in Hybrid (PHEV)</option>
              </select>
            </div>
          </div>

          {/* Hãng & Model */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Hãng xe</label>
              <input
                type="text"
                value={brand}
                onChange={(e) => setBrand(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Dòng xe (Model)</label>
              <input
                type="text"
                value={model}
                onChange={(e) => setModel(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Màu & Chiều cao */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Màu ngoại thất</label>
              <input
                type="text"
                value={color}
                onChange={(e) => setColor(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Chiều cao (cm)</label>
              <input
                type="number"
                value={heightCm}
                onChange={(e) => setHeightCm(e.target.value ? Number(e.target.value) : '')}
                min="50"
                max="300"
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Chiều dài & Chiều rộng (US-012 Xe quá khổ) */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Chiều dài (cm)</label>
              <input
                type="number"
                value={lengthCm}
                onChange={(e) => setLengthCm(e.target.value ? Number(e.target.value) : '')}
                placeholder="VD: 475"
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Chiều rộng (cm)</label>
              <input
                type="number"
                value={widthCm}
                onChange={(e) => setWidthCm(e.target.value ? Number(e.target.value) : '')}
                placeholder="VD: 193"
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Actions */}
          <div className="pt-4 flex items-center justify-end gap-3 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="px-5 py-2 text-sm font-bold bg-blue-600 text-white rounded-xl hover:bg-blue-700 transition-colors shadow-sm disabled:opacity-50"
            >
              {isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
