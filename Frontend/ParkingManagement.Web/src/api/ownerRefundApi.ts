import { apiClient } from './client';

export interface PaymentDto {
  id: number;
  code: string;
  bookingId?: number | null;
  bookingCode?: string | null;
  method: string;
  status: string;
  amount: number;
  refundedAmount: number;
  providerTransactionId?: string | null;
  paidAtUtc?: string | null;
  invoiceNumber?: string | null;
}

export interface ProcessOwnerRefundCommand {
  paymentId: number;
  ownerProfileId: number;
  amount: number;
  reasonDescription: string;
  complaintId?: number | null;
}

export interface OwnerRefundResultDto {
  paymentId: number;
  refundId: number;
  refundAmount: number;
  status: string;
  reason: string;
  message: string;
}

/**
 * Service API hoàn tiền chủ động từ Chủ bãi cho khách hàng (UI-24 / US-041, UC-36).
 * Backend: PaymentService / RefundsController (/api/v1/refunds/owner-refund) & PaymentsController (/api/v1/payments)
 */
export const ownerRefundApi = {
  /**
   * Lấy danh sách giao dịch thanh toán của một lượt đặt chỗ (UC-12, UC-19).
   */
  getPaymentsByBooking: async (bookingId: number): Promise<PaymentDto[]> => {
    const response = await apiClient.get<PaymentDto[]>('/api/v1/payments', {
      params: { bookingId },
    });
    return response.data;
  },

  /**
   * Chủ bãi chủ động hoàn tiền bồi thường cho khách do sự cố tại bãi hoặc qua giải quyết khiếu nại (US-041).
   */
  processOwnerRefund: async (command: ProcessOwnerRefundCommand): Promise<OwnerRefundResultDto> => {
    const response = await apiClient.post<OwnerRefundResultDto>(
      '/api/v1/refunds/owner-refund',
      command
    );
    return response.data;
  },
};
