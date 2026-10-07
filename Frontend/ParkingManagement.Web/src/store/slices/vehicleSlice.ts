import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { vehicleApi } from '@/api/vehicleApi';
import type { CreateVehicleRequestDto, UpdateVehicleRequestDto, VehicleDto } from '@/types/vehicle';

interface VehicleState {
  vehicles: VehicleDto[];
  isLoading: boolean;
  isSubmitting: boolean;
  error: string | null;
  successMessage: string | null;
}

const initialState: VehicleState = {
  vehicles: [],
  isLoading: false,
  isSubmitting: false,
  error: null,
  successMessage: null,
};

export const fetchVehicles = createAsyncThunk(
  'vehicle/fetchVehicles',
  async (userId: number, { rejectWithValue }) => {
    try {
      return await vehicleApi.getVehiclesByUser(userId);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể tải danh sách phương tiện.');
    }
  }
);

export const addNewVehicle = createAsyncThunk(
  'vehicle/addNewVehicle',
  async (data: CreateVehicleRequestDto, { rejectWithValue }) => {
    try {
      return await vehicleApi.createVehicle(data);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể thêm phương tiện.');
    }
  }
);

export const makeDefaultVehicle = createAsyncThunk(
  'vehicle/makeDefaultVehicle',
  async ({ vehicleId, userId }: { vehicleId: number; userId: number }, { rejectWithValue }) => {
    try {
      return await vehicleApi.setDefaultVehicle(vehicleId, userId);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể đặt xe làm mặc định.');
    }
  }
);

export const updateExistingVehicle = createAsyncThunk(
  'vehicle/updateExistingVehicle',
  async ({ vehicleId, data }: { vehicleId: number; data: UpdateVehicleRequestDto }, { rejectWithValue }) => {
    try {
      return await vehicleApi.updateVehicle(vehicleId, data);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể cập nhật thông tin xe.');
    }
  }
);

export const deleteExistingVehicle = createAsyncThunk(
  'vehicle/deleteExistingVehicle',
  async ({ vehicleId, userId }: { vehicleId: number; userId: number }, { rejectWithValue }) => {
    try {
      await vehicleApi.deleteVehicle(vehicleId, userId);
      return vehicleId;
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể xóa phương tiện.');
    }
  }
);

export const vehicleSlice = createSlice({
  name: 'vehicle',
  initialState,
  reducers: {
    clearMessages: (state) => {
      state.error = null;
      state.successMessage = null;
    },
  },
  extraReducers: (builder) => {
    // Fetch
    builder
      .addCase(fetchVehicles.pending, (state) => {
        state.isLoading = true;
        state.error = null;
      })
      .addCase(fetchVehicles.fulfilled, (state, action) => {
        state.isLoading = false;
        state.vehicles = action.payload;
      })
      .addCase(fetchVehicles.rejected, (state, action) => {
        state.isLoading = false;
        state.error = action.payload as string;
      });

    // Add
    builder
      .addCase(addNewVehicle.pending, (state) => {
        state.isSubmitting = true;
        state.error = null;
      })
      .addCase(addNewVehicle.fulfilled, (state, action) => {
        state.isSubmitting = false;
        state.vehicles.push(action.payload);
        state.successMessage = `Thêm xe ${action.payload.plateDisplay} thành công!`;
      })
      .addCase(addNewVehicle.rejected, (state, action) => {
        state.isSubmitting = false;
        state.error = action.payload as string;
      });

    // Make Default
    builder
      .addCase(makeDefaultVehicle.fulfilled, (state, action) => {
        const updated = action.payload;
        state.vehicles = state.vehicles.map((v) => ({
          ...v,
          isDefault: v.id === updated.id,
        }));
        state.successMessage = `Đã đặt xe ${updated.plateDisplay} làm mặc định.`;
      })
      .addCase(makeDefaultVehicle.rejected, (state, action) => {
        state.error = action.payload as string;
      });

    // Update
    builder
      .addCase(updateExistingVehicle.fulfilled, (state, action) => {
        const updated = action.payload;
        const index = state.vehicles.findIndex((v) => v.id === updated.id);
        if (index !== -1) {
          state.vehicles[index] = updated;
        }
        state.successMessage = `Cập nhật thông tin xe ${updated.plateDisplay} thành công!`;
      })
      .addCase(updateExistingVehicle.rejected, (state, action) => {
        state.error = action.payload as string;
      });

    // Delete
    builder
      .addCase(deleteExistingVehicle.fulfilled, (state, action) => {
        const deletedId = action.payload;
        state.vehicles = state.vehicles.filter((v) => v.id !== deletedId);
        state.successMessage = 'Đã xóa phương tiện khỏi Garage.';
      })
      .addCase(deleteExistingVehicle.rejected, (state, action) => {
        state.error = action.payload as string;
      });
  },
});

export const { clearMessages } = vehicleSlice.actions;
export default vehicleSlice.reducer;
