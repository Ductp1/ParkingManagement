export type UserRole = 'Driver' | 'Owner' | 'Staff' | 'Admin';

export interface AuthUser {
  id: number;
  fullName: string;
  phone?: string;
  email?: string;
  role: UserRole;
  roles?: UserRole[];
  ownerProfileId?: number;
  token?: string;
}

export interface AuthState {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}
