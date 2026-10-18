import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthState, AuthUser, UserRole } from '@/types/auth';

const initialState: AuthState = {
  user: null,
  token: sessionStorage.getItem('parkmaster_token'),
  isAuthenticated: false,
  isLoading: !!sessionStorage.getItem('parkmaster_token'),
  error: null,
};

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setLoading: (state, action: PayloadAction<boolean>) => { state.isLoading = action.payload; },
    setUser: (state, action: PayloadAction<AuthUser | null>) => {
      state.user = action.payload;
      state.isAuthenticated = !!action.payload;
    },
    setToken: (state, action: PayloadAction<string | null>) => {
      state.token = action.payload;
      if (action.payload) {
        sessionStorage.setItem('parkmaster_token', action.payload);
      } else {
        sessionStorage.removeItem('parkmaster_token');
      }
    },
    switchRole: (state, action: PayloadAction<UserRole>) => {
      const targetRole = action.payload;
      if (state.user) {
        // Chỉ cho phép chuyển vai trò nếu tài khoản thực sự sở hữu vai trò đó trong user.roles (hoặc là Admin)
        const canSwitch =
          state.user.roles?.includes(targetRole) || state.user.roles?.includes('Admin');
        if (canSwitch) {
          state.user.role = targetRole;
        }
      }
    },
    logout: (state) => {
      state.user = null;
      state.token = null;
      state.isAuthenticated = false;
      sessionStorage.removeItem('parkmaster_token');
    },
  },
});

export const { setLoading, setUser, setToken, switchRole, logout } = authSlice.actions;
export default authSlice.reducer;
