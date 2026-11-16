export interface ApplyPromotionRequest {
  code: string;
  parkingLotId: number;
  originalAmount: number;
}

export interface AppliedPromotionDto {
  promotionId: number;
  code: string;
  name: string;
  sponsor: string;
  discountType: string;
  originalAmount: number;
  discountAmount: number;
  finalAmount: number;
  message: string;
}

export interface CreateVnpayPaymentRequest {
  bookingId: number;
  bookingCode: string;
  parkingLotId: number;
  ownerProfileId: number;
  userId: number;
  amount: number;
}

export interface VnpayPaymentUrlDto {
  paymentCode: string;
  paymentUrl: string;
  expiresAtUtc: string;
}

export interface VnpayReturnResultDto {
  isSuccess: boolean;
  message: string;
  paymentCode: string;
  bookingCode?: string | null;
  amount: number;
  status: string;
}

export interface CreateVietQrPaymentRequest {
  bookingId?: number | null;
  bookingCode?: string | null;
  parkingSessionId?: number | null;
  parkingLotId: number;
  ownerProfileId: number;
  userId: number;
  amount: number;
  bankBin: string;
  bankAccountNumber: string;
  bankAccountName: string;
}

export interface VietQrPaymentDto {
  paymentCode: string;
  qrImageUrl: string;
  bankBin: string;
  bankAccountNumber: string;
  bankAccountName: string;
  amount: number;
  transferContent: string;
  expiresAtUtc: string;
}

export interface PaymentDto {
  id: number;
  paymentCode: string;
  bookingId?: number | null;
  bookingCode?: string | null;
  amount: number;
  method: string;
  status: string;
  paidAtUtc?: string | null;
  createdAtUtc: string;
}

export interface InvoiceDto {
  id: number;
  type: string;
  invoiceNumber: string;
  paymentId?: number | null;
  bookingId?: number | null;
  settlementId?: number | null;
  buyerName: string;
  buyerTaxCode?: string | null;
  buyerEmail?: string | null;
  subTotal: number;
  vatAmount: number;
  total: number;
  issuedAtUtc: string;
  pdfUrl?: string | null;
  status: string;
}
