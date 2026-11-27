export const VehicleType = {
  Sedan: 'Sedan',
  Suv: 'Suv',
  Hatchback: 'Hatchback',
  Van: 'Van',
  Pickup: 'Pickup',
  Oversized: 'Oversized',
  Motorbike: 'Motorbike',
} as const;
export type VehicleType = (typeof VehicleType)[keyof typeof VehicleType] | string | number;

export const FuelType = {
  Gasoline: 'Gasoline',
  Diesel: 'Diesel',
  Electric: 'Electric',
  Hybrid: 'Hybrid',
  PlugInHybrid: 'PlugInHybrid',
} as const;
export type FuelType = (typeof FuelType)[keyof typeof FuelType] | string | number;

export interface VehicleDto {
  id: number;
  userId: number;
  plateNumber: string;
  plateDisplay: string;
  vehicleType: VehicleType;
  fuelType: FuelType;
  brand?: string | null;
  model?: string | null;
  color?: string | null;
  heightCm: number;
  lengthCm?: number | null;
  widthCm?: number | null;
  isEmergencyVehicle?: boolean;
  isDefault: boolean;
}

export interface CreateVehicleRequestDto {
  userId: number;
  plateNumber: string;
  vehicleType?: VehicleType;
  fuelType?: FuelType;
  brand?: string | null;
  model?: string | null;
  color?: string | null;
  heightCm?: number | null;
  lengthCm?: number | null;
  widthCm?: number | null;
  isDefault?: boolean;
}

export interface UpdateVehicleRequestDto {
  userId: number;
  vehicleType: VehicleType;
  fuelType: FuelType;
  brand?: string | null;
  model?: string | null;
  color?: string | null;
  heightCm?: number | null;
  lengthCm?: number | null;
  widthCm?: number | null;
}
