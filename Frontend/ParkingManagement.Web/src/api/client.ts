import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { ProblemDetails } from '@/types/common';

// Base URL: In development, using relative URL '/api' leverages Vite reverse proxy,
// preventing any CORS issues. Can also fall back to VITE_API_GATEWAY_URL.
const gatewayBaseUrl = import.meta.env.VITE_API_GATEWAY_URL || 'http://localhost:5000';

export const apiClient = axios.create({
  baseURL: import.meta.env.DEV ? '' : gatewayBaseUrl,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
  timeout: 15000,
});

// Request Interceptor: Attach JWT Bearer Token if present in localStorage
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('parkmaster_token');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response Interceptor: Format error with RFC7807 ProblemDetails
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    let message = 'Đã có lỗi xảy ra trong quá trình xử lý.';

    if (error.response?.data) {
      const data = error.response.data;
      if (data.detail) {
        message = data.detail;
      } else if (data.title) {
        message = data.title;
      } else if (data.errors) {
        const errorList = Object.values(data.errors).flat();
        if (errorList.length > 0) {
          message = errorList.join(', ');
        }
      }
    } else if (error.message) {
      if (error.code === 'ERR_NETWORK') {
        message = 'Không thể kết nối đến API Gateway (Port 5000). Vui lòng kiểm tra backend.';
      } else {
        message = error.message;
      }
    }

    return Promise.reject(new Error(message));
  }
);
