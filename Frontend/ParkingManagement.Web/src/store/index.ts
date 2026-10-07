import { configureStore } from '@reduxjs/toolkit';
import { useDispatch, useSelector, type TypedUseSelectorHook } from 'react-redux';
import authReducer from './slices/authSlice';
import vehicleReducer from './slices/vehicleSlice';
import parkingReducer from './slices/parkingSlice';

export const store = configureStore({
  reducer: {
    auth: authReducer,
    vehicle: vehicleReducer,
    parking: parkingReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export const useAppDispatch = () => useDispatch<AppDispatch>();
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;
