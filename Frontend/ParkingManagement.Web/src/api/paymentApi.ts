import { apiClient } from './client';
import type {
  AppliedPromotionDto,
  ApplyPromotionRequest,
  CreateVietQrPaymentRequest,
  CreateVnpayPaymentRequest,
  InvoiceDto,
  PaymentDto,
  VietQrPaymentDto,
  VnpayPaymentUrlDto,
  VnpayReturnResultDto,
} from '@/types/payment';

/**
 * API thanh toán, mã khuyến mãi & hóa đơn điện tử — route qua Gateway :5000 → PaymentService :5105.
 */
export const paymentApi = {
  applyPromotion: async (request: ApplyPromotionRequest): Promise<AppliedPromotionDto> => {
    const response = await apiClient.post<AppliedPromotionDto>('/api/v1/promotions/apply', request);
    return response.data;
  },

  createVnpayPayment: async (request: CreateVnpayPaymentRequest): Promise<VnpayPaymentUrlDto> => {
    const response = await apiClient.post<VnpayPaymentUrlDto>('/api/v1/payments/vnpay', request);
    return response.data;
  },

  processVnpayReturn: async (queryString: string): Promise<VnpayReturnResultDto> => {
    const cleanQuery = queryString.startsWith('?') ? queryString : `?${queryString}`;
    const response = await apiClient.get<VnpayReturnResultDto>(
      `/api/v1/payments/vnpay-return${cleanQuery}`
    );
    return response.data;
  },

  createVietQrPayment: async (request: CreateVietQrPaymentRequest): Promise<VietQrPaymentDto> => {
    const response = await apiClient.post<VietQrPaymentDto>('/api/v1/payments/vietqr', request);
    return response.data;
  },

  listByBooking: async (bookingId: number): Promise<PaymentDto[]> => {
    const response = await apiClient.get<PaymentDto[]>('/api/v1/payments', {
      params: { bookingId },
    });
    return response.data;
  },

  getInvoiceByBooking: async (bookingId: number): Promise<InvoiceDto> => {
    const response = await apiClient.get<InvoiceDto>(`/api/v1/invoices/by-booking/${bookingId}`);
    return response.data;
  },

  getInvoiceByPayment: async (paymentId: number): Promise<InvoiceDto> => {
    const response = await apiClient.get<InvoiceDto>(`/api/v1/invoices/by-payment/${paymentId}`);
    return response.data;
  },
};
