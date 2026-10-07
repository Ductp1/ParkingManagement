import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthState, AuthUser, UserRole } from '@/types/auth';

const initialUser: AuthUser = {
  id: 5, // Nguyễn Văn An (Driver trong DB seed có id = 5)
  fullName: 'Nguyễn Văn An',
  phone: '0900000005',
  email: 'driver1@smartparking.vn',
  role: 'Driver',
};

const initialState: AuthState = {
  user: initialUser,
  token: localStorage.getItem('parkmaster_token'),
  isAuthenticated: true,
  isLoading: false,
  error: null,
};

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUser: (state, action: PayloadAction<AuthUser | null>) => {
      state.user = action.payload;
      state.isAuthenticated = !!action.payload;
    },
    setToken: (state, action: PayloadAction<string | null>) => {
      state.token = action.payload;
      if (action.payload) {
        localStorage.setItem('parkmaster_token', action.payload);
      } else {
        localStorage.removeItem('parkmaster_token');
      }
    },
    switchRole: (state, action: PayloadAction<UserRole>) => {
      if (state.user) {
        state.user.role = action.payload;
      }
    },
    logout: (state) => {
      state.user = null;
      state.token = null;
      state.isAuthenticated = false;
      localStorage.removeItem('parkmaster_token');
    },
  },
});

export const { setUser, setToken, switchRole, logout } = authSlice.actions;
export default authSlice.reducer;
