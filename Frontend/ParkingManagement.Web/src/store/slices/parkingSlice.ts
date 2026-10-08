import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import { parkingApi } from '@/api/parkingApi';
import type { ParkingLotDto, ParkingLotSearchItemDto } from '@/types/parking';

interface ParkingState {
  currentLot: ParkingLotDto | null;
  searchResults: ParkingLotSearchItemDto[];
  isLoading: boolean;
  error: string | null;
}

const initialState: ParkingState = {
  currentLot: null,
  searchResults: [],
  isLoading: false,
  error: null,
};

export const fetchParkingLotById = createAsyncThunk(
  'parking/fetchById',
  async (id: number, { rejectWithValue }) => {
    try {
      return await parkingApi.getById(id);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể tải thông tin bãi đỗ.');
    }
  }
);

export const searchParkingLots = createAsyncThunk(
  'parking/searchLots',
  async (
    params: { lat: number; lng: number; radiusKm?: number; vehicleHeightCm?: number },
    { rejectWithValue }
  ) => {
    try {
      return await parkingApi.search(params);
    } catch (err: unknown) {
      const error = err as Error;
      return rejectWithValue(error.message || 'Không thể tìm kiếm bãi đỗ.');
    }
  }
);

export const parkingSlice = createSlice({
  name: 'parking',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchParkingLotById.pending, (state) => {
        state.isLoading = true;
        state.error = null;
      })
      .addCase(fetchParkingLotById.fulfilled, (state, action) => {
        state.isLoading = false;
        state.currentLot = action.payload;
      })
      .addCase(fetchParkingLotById.rejected, (state, action) => {
        state.isLoading = false;
        state.error = action.payload as string;
      })
      .addCase(searchParkingLots.fulfilled, (state, action) => {
        state.searchResults = action.payload;
      });
  },
});

export default parkingSlice.reducer;
