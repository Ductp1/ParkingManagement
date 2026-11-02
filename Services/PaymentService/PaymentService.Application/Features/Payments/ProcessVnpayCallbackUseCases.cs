using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Payments;

#region DTOs và Hợp đồng Use Case

/// <summary>
/// DTO chứa kết quả thanh toán trả về cho giao diện Frontend khi người dùng quay lại từ VNPAY (vnpay-return).
/// </summary>
/// <param name="IsSuccess">Trạng thái giao dịch có thành công hay không.</param>
/// <param name="Message">Thông điệp chi tiết thông báo cho người dùng.</param>
/// <param name="PaymentCode">Mã định danh giao dịch thanh toán nội bộ (PAY_...).</param>
/// <param name="BookingCode">Mã lượt đặt chỗ gửi xe (ví dụ: BK-0002).</param>
/// <param name="Amount">Số tiền thực tế đã thanh toán (VNĐ).</param>
/// <param name="Status">Trạng thái hiện tại của giao dịch thanh toán trong hệ thống.</param>
public sealed record VnpayReturnResultDto(
    bool IsSuccess,
    string Message,
    string PaymentCode,
    string? BookingCode,
    decimal Amount,
    PaymentStatus Status);

/// <summary>
/// DTO chuẩn phản hồi cho máy chủ VNPAY khi nhận webhook ngầm (vnpay-ipn).
/// </summary>
/// <param name="RspCode">Mã phản hồi theo quy định của VNPAY (00: Thành công, 97: Sai chữ ký, 01: Không tìm thấy đơn, 02: Đơn đã xác nhận, 04: Sai số tiền).</param>
/// <param name="Message">Mô tả tương ứng với mã phản hồi.</param>
public sealed record VnpayIpnResponseDto(
    string RspCode,
    string Message);

/// <summary>
/// Hợp đồng Use Case xử lý kết quả khi khách hàng quay về hệ thống từ cổng VNPAY qua đường dẫn Return URL (US-033).
/// </summary>
public interface IProcessVnpayReturnUseCase
{
    /// <summary>
    /// Tiếp nhận và thẩm định kết quả thanh toán từ các tham số URL khi trình duyệt chuyển hướng về.
    /// </summary>
    /// <param name="queryParams">Tập hợp các tham số nhận được trên Query String.</param>
    /// <param name="sourceIp">Địa chỉ IP của thiết bị khách hàng.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>Đối tượng VnpayReturnResultDto để hiển thị cho người dùng.</returns>
    Task<VnpayReturnResultDto> ExecuteAsync(
        IReadOnlyDictionary<string, string> queryParams, 
        string? sourceIp, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hợp đồng Use Case xử lý thông báo kết quả tức thì (IPN Webhook) gửi ngầm từ máy chủ VNPAY sang (US-033).
/// </summary>
public interface IProcessVnpayIpnUseCase
{
    /// <summary>
    /// Tiếp nhận, thẩm định chữ ký số và cập nhật giao dịch khi nhận tín hiệu webhook server-to-server từ VNPAY.
    /// </summary>
    /// <param name="queryParams">Tập hợp các tham số VNPAY truyền sang qua Query String.</param>
    /// <param name="sourceIp">Địa chỉ IP máy chủ của VNPAY gọi sang.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>Đối tượng VnpayIpnResponseDto chứa RspCode và Message theo chuẩn VNPAY.</returns>
    Task<VnpayIpnResponseDto> ExecuteAsync(
        IReadOnlyDictionary<string, string> queryParams, 
        string? sourceIp, 
        CancellationToken cancellationToken = default);
}

#endregion

#region Use Case 1: Xử lý Return URL (Khách hàng quay về từ trình duyệt)

/// <summary>
/// Use Case xử lý kết quả khi khách hàng quay về hệ thống từ cổng VNPAY qua đường dẫn Return URL (US-033).
/// </summary>
public sealed class ProcessVnpayReturnUseCase(
    IPaymentRepository paymentRepository,
    IVnpayGateway vnpayGateway) : IProcessVnpayReturnUseCase
{
    public async Task<VnpayReturnResultDto> ExecuteAsync(
        IReadOnlyDictionary<string, string> queryParams, 
        string? sourceIp, 
        CancellationToken cancellationToken = default)
    {
        // 1. Thẩm định tính hợp lệ của chữ ký số bảo mật HMAC-SHA512
        var isSignatureValid = vnpayGateway.ValidateCallbackSignature(queryParams);
        var paymentCode = queryParams.GetValueOrDefault("vnp_TxnRef") ?? string.Empty;
        var responseCode = queryParams.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        var transactionNo = queryParams.GetValueOrDefault("vnp_TransactionNo");
        var rawPayload = JsonSerializer.Serialize(queryParams);

        if (!isSignatureValid)
        {
            var invalidSigLog = new PaymentCallbackLog
            {
                Provider = PaymentMethod.Vnpay,
                ProviderTransactionId = transactionNo,
                RawPayload = rawPayload,
                SignatureValid = false,
                IsDuplicate = false,
                ResultCode = responseCode,
                ProcessingError = "Chữ ký số không hợp lệ (Mã lỗi 70 hoặc bị can thiệp dữ liệu).",
                SourceIp = sourceIp
            };
            await paymentRepository.SaveCallbackResultAsync(null, invalidSigLog, null, cancellationToken);

            return new VnpayReturnResultDto(
                false, 
                "Chữ ký bảo mật không hợp lệ. Giao dịch có thể đã bị can thiệp.", 
                paymentCode, 
                null, 
                0, 
                PaymentStatus.Failed);
        }

        // 2. Tra cứu thông tin đơn thanh toán trong Database
        var payment = await paymentRepository.GetByCodeAsync(paymentCode, cancellationToken);
        if (payment == null)
        {
            return new VnpayReturnResultDto(
                false, 
                "Không tìm thấy thông tin giao dịch trong hệ thống.", 
                paymentCode, 
                null, 
                0, 
                PaymentStatus.Failed);
        }

        // 3. Nếu giao dịch đã hoàn tất trước đó (do IPN xử lý trước), trả về kết quả thành công ngay (Idempotent UI)
        if (payment.Status == PaymentStatus.Succeeded)
        {
            return new VnpayReturnResultDto(
                true, 
                "Thanh toán thành công.", 
                payment.Code, 
                payment.BookingCode, 
                payment.Amount, 
                PaymentStatus.Succeeded);
        }

        // 4. Đọc mã phản hồi của VNPAY (00 là thành công)
        var isSuccess = responseCode == "00";
        payment.ProviderResponseCode = responseCode;
        payment.ProviderTransactionId = transactionNo;
        payment.RawCallbackJson = rawPayload;

        OutboxMessage? outboxMessage = null;

        if (isSuccess)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.PaidAtUtc = DateTime.UtcNow;

            // Tạo sự kiện tích hợp PaymentSucceeded để báo cho BookingService giữ chỗ
            outboxMessage = new OutboxMessage
            {
                EventType = nameof(PaymentSucceeded),
                PayloadJson = JsonSerializer.Serialize(new PaymentSucceeded(
                    payment.Id,
                    payment.BookingId,
                    payment.ParkingSessionId,
                    payment.Amount,
                    "VNPAY")),
                OccurredAtUtc = DateTime.UtcNow
            };
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            outboxMessage = new OutboxMessage
            {
                EventType = nameof(PaymentFailed),
                PayloadJson = JsonSerializer.Serialize(new PaymentFailed(
                    payment.Id,
                    payment.BookingId,
                    $"Giao dịch VNPAY thất bại với mã phản hồi {responseCode}")),
                OccurredAtUtc = DateTime.UtcNow
            };
        }

        // 5. Ghi nhật ký callback vào database
        var callbackLog = new PaymentCallbackLog
        {
            PaymentId = payment.Id,
            Provider = PaymentMethod.Vnpay,
            ProviderTransactionId = transactionNo,
            RawPayload = rawPayload,
            SignatureValid = true,
            IsDuplicate = false,
            ResultCode = responseCode,
            SourceIp = sourceIp
        };

        await paymentRepository.SaveCallbackResultAsync(payment, callbackLog, outboxMessage, cancellationToken);

        var message = isSuccess ? "Thanh toán thành công." : $"Giao dịch không thành công (Mã lỗi VNPAY: {responseCode}).";
        return new VnpayReturnResultDto(
            isSuccess, 
            message, 
            payment.Code, 
            payment.BookingCode, 
            payment.Amount, 
            payment.Status);
    }
}

#endregion

#region Use Case 2: Xử lý Webhook IPN (Máy chủ VNPAY gọi ngầm)

/// <summary>
/// Use Case xử lý thông báo kết quả tức thì (IPN Webhook) gửi ngầm từ máy chủ VNPAY sang (US-033).
/// </summary>
public sealed class ProcessVnpayIpnUseCase(
    IPaymentRepository paymentRepository,
    IVnpayGateway vnpayGateway) : IProcessVnpayIpnUseCase
{
    public async Task<VnpayIpnResponseDto> ExecuteAsync(
        IReadOnlyDictionary<string, string> queryParams, 
        string? sourceIp, 
        CancellationToken cancellationToken = default)
    {
        var rawPayload = JsonSerializer.Serialize(queryParams);
        var paymentCode = queryParams.GetValueOrDefault("vnp_TxnRef") ?? string.Empty;
        var responseCode = queryParams.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        var transactionNo = queryParams.GetValueOrDefault("vnp_TransactionNo");

        // BƯỚC 1: Kiểm tra chữ ký số HMAC-SHA512
        if (!vnpayGateway.ValidateCallbackSignature(queryParams))
        {
            var invalidSigLog = new PaymentCallbackLog
            {
                Provider = PaymentMethod.Vnpay,
                ProviderTransactionId = transactionNo,
                RawPayload = rawPayload,
                SignatureValid = false,
                IsDuplicate = false,
                ResultCode = responseCode,
                ProcessingError = "Sai chữ ký số (Invalid Checksum).",
                SourceIp = sourceIp
            };
            await paymentRepository.SaveCallbackResultAsync(null, invalidSigLog, null, cancellationToken);

            return new VnpayIpnResponseDto("97", "Invalid Checksum");
        }

        // BƯỚC 2: Kiểm tra sự tồn tại của đơn hàng
        var payment = await paymentRepository.GetByCodeAsync(paymentCode, cancellationToken);
        if (payment == null)
        {
            var notFoundLog = new PaymentCallbackLog
            {
                Provider = PaymentMethod.Vnpay,
                ProviderTransactionId = transactionNo,
                RawPayload = rawPayload,
                SignatureValid = true,
                IsDuplicate = false,
                ResultCode = responseCode,
                ProcessingError = $"Không tìm thấy đơn hàng với mã: {paymentCode}.",
                SourceIp = sourceIp
            };
            await paymentRepository.SaveCallbackResultAsync(null, notFoundLog, null, cancellationToken);

            return new VnpayIpnResponseDto("01", "Order not found");
        }

        // BƯỚC 3: Kiểm tra tính khớp của số tiền thanh toán (VNPAY gửi số tiền đã nhân 100)
        if (long.TryParse(queryParams.GetValueOrDefault("vnp_Amount"), out var vnpAmount))
        {
            var expectedVnpAmount = (long)(payment.Amount * 100);
            if (vnpAmount != expectedVnpAmount)
            {
                var invalidAmountLog = new PaymentCallbackLog
                {
                    PaymentId = payment.Id,
                    Provider = PaymentMethod.Vnpay,
                    ProviderTransactionId = transactionNo,
                    RawPayload = rawPayload,
                    SignatureValid = true,
                    IsDuplicate = false,
                    ResultCode = responseCode,
                    ProcessingError = $"Số tiền không khớp. Thực tế: {vnpAmount}, Kỳ vọng: {expectedVnpAmount}.",
                    SourceIp = sourceIp
                };
                await paymentRepository.SaveCallbackResultAsync(payment, invalidAmountLog, null, cancellationToken);

                return new VnpayIpnResponseDto("04", "Invalid amount");
            }
        }
        else
        {
            // Trường hợp vnp_Amount rỗng hoặc không phải số hợp lệ
            var invalidAmountFormatLog = new PaymentCallbackLog
            {
                PaymentId = payment.Id,
                Provider = PaymentMethod.Vnpay,
                ProviderTransactionId = transactionNo,
                RawPayload = rawPayload,
                SignatureValid = true,
                IsDuplicate = false,
                ResultCode = responseCode,
                ProcessingError = "Định dạng số tiền vnp_Amount không hợp lệ.",
                SourceIp = sourceIp
            };
            await paymentRepository.SaveCallbackResultAsync(payment, invalidAmountFormatLog, null, cancellationToken);

            return new VnpayIpnResponseDto("04", "Invalid amount");
        }

        // BƯỚC 4: Kiểm tra trạng thái đơn hàng (Chống xử lý trùng lặp - Idempotency)
        if (payment.Status != PaymentStatus.Pending)
        {
            var duplicateLog = new PaymentCallbackLog
            {
                PaymentId = payment.Id,
                Provider = PaymentMethod.Vnpay,
                ProviderTransactionId = transactionNo,
                RawPayload = rawPayload,
                SignatureValid = true,
                IsDuplicate = true,
                ResultCode = responseCode,
                ProcessingError = "Đơn hàng đã được xác nhận trước đó.",
                SourceIp = sourceIp
            };
            await paymentRepository.SaveCallbackResultAsync(payment, duplicateLog, null, cancellationToken);

            return new VnpayIpnResponseDto("02", "Order already confirmed");
        }

        // BƯỚC 5: Đọc mã kết quả giao dịch và cập nhật trạng thái
        var isSuccess = responseCode == "00";
        payment.ProviderResponseCode = responseCode;
        payment.ProviderTransactionId = transactionNo;
        payment.RawCallbackJson = rawPayload;

        OutboxMessage? outboxMessage = null;

        if (isSuccess)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.PaidAtUtc = DateTime.UtcNow;

            // Bắn sự kiện tích hợp PaymentSucceeded vào Outbox
            outboxMessage = new OutboxMessage
            {
                EventType = nameof(PaymentSucceeded),
                PayloadJson = JsonSerializer.Serialize(new PaymentSucceeded(
                    payment.Id,
                    payment.BookingId,
                    payment.ParkingSessionId,
                    payment.Amount,
                    "VNPAY")),
                OccurredAtUtc = DateTime.UtcNow
            };
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            outboxMessage = new OutboxMessage
            {
                EventType = nameof(PaymentFailed),
                PayloadJson = JsonSerializer.Serialize(new PaymentFailed(
                    payment.Id,
                    payment.BookingId,
                    $"Giao dịch VNPAY thất bại với mã phản hồi {responseCode}")),
                OccurredAtUtc = DateTime.UtcNow
            };
        }

        var callbackLog = new PaymentCallbackLog
        {
            PaymentId = payment.Id,
            Provider = PaymentMethod.Vnpay,
            ProviderTransactionId = transactionNo,
            RawPayload = rawPayload,
            SignatureValid = true,
            IsDuplicate = false,
            ResultCode = responseCode,
            SourceIp = sourceIp
        };

        await paymentRepository.SaveCallbackResultAsync(payment, callbackLog, outboxMessage, cancellationToken);

        return new VnpayIpnResponseDto("00", "Confirm Success");
    }
}

#endregion
