import { createContext, useContext } from 'react';

export type AdminToastType = 'success' | 'error';

export interface AdminToastInput {
  type: AdminToastType;
  title: string;
  message?: string;
}

export interface AdminToastContextValue {
  show: (toast: AdminToastInput) => void;
}

export const AdminToastContext = createContext<AdminToastContextValue | null>(null);

/** Bật toast góc phải sau một thao tác (Figma Shell/Toast Success / Error). */
export const useAdminToast = (): AdminToastContextValue => {
  const ctx = useContext(AdminToastContext);
  if (!ctx) throw new Error('useAdminToast phải dùng bên trong AdminToastProvider');
  return ctx;
};
