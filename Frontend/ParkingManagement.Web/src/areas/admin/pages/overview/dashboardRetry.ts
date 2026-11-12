import { createContext } from 'react';

/** Làm mới toàn dashboard (nút "Thử lại" của widget lỗi nguồn gọi lại cả overview – mọi nguồn chạy song song). */
export const DashboardRetryContext = createContext<() => void>(() => undefined);
