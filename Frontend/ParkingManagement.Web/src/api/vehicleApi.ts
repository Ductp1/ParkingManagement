import { apiClient } from './client';
import type { CreateVehicleRequestDto, UpdateVehicleRequestDto, VehicleDto } from '@/types/vehicle';

export const vehicleApi = {
  /**
   * Lấy danh sách xe trong Garage của tài xế (UC-07)
   */
  getVehiclesByUser: async (userId: number): Promise<VehicleDto[]> => {
    const response = await apiClient.get<VehicleDto[]>('/api/v1/vehicles', {
      params: { userId },
    });
    return response.data;
  },

  /**
   * Tra cứu thông tin xe theo biển số
   */
  findVehicleByPlate: async (plate: string): Promise<VehicleDto> => {
    const response = await apiClient.get<VehicleDto>(`/api/v1/vehicles/lookup/${encodeURIComponent(plate)}`);
    return response.data;
  },

  /**
   * Thêm xe mới vào Garage (US-009, US-010)
   */
  createVehicle: async (data: CreateVehicleRequestDto): Promise<VehicleDto> => {
    const response = await apiClient.post<VehicleDto>('/api/v1/vehicles', data);
    return response.data;
  },

  /**
   * Đặt xe làm mặc định (US-010)
   */
  setDefaultVehicle: async (vehicleId: number, userId: number): Promise<VehicleDto> => {
    const response = await apiClient.patch<VehicleDto>(`/api/v1/vehicles/${vehicleId}/default`, null, {
      params: { userId },
    });
    return response.data;
  },

  /**
   * Cập nhật thông tin xe (US-011)
   */
  updateVehicle: async (vehicleId: number, data: UpdateVehicleRequestDto): Promise<VehicleDto> => {
    const response = await apiClient.put<VehicleDto>(`/api/v1/vehicles/${vehicleId}`, data);
    return response.data;
  },

  /**
   * Xóa mềm xe có kiểm tra ràng buộc booking (US-011)
   */
  deleteVehicle: async (vehicleId: number, userId: number): Promise<void> => {
    await apiClient.delete(`/api/v1/vehicles/${vehicleId}`, {
      params: { userId },
    });
  },
};
