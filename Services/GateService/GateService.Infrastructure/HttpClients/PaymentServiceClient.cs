using System.Net;
using GateService.Application.Features.Shifts;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GateService.Application.Features.GateConsole;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.HttpClients;

/// <summary>Client gọi PaymentService (TV4): chốt phí lượt gửi (UC-22) + ghi thanh toán tiền mặt tại cổng.</summary>
public sealed class PaymentServiceClient(HttpClient http, ILogger<PaymentServiceClient> logger)
    : IExitFeeCalculator, IGateSettlementRecorder, IShiftCashSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static StringContent Serialize<T>(T value) where T : class
        => new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    public async Task<SessionFeeResult> CalculateAsync(SessionFeeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/pricing/session-fee",
            Serialize(request), cancellationToken, "chốt phí lượt gửi");
        EnsureSuccessAsync(response, "chốt phí lượt gửi");

        var dto = await response.Content.ReadFromJsonAsync<SessionFeeResponse>(Json, cancellationToken)
            ?? throw new DependencyUnavailableException("PaymentService trả dữ liệu rỗng khi chốt phí lượt gửi.");
        return new SessionFeeResult(dto.RateCardId, dto.DurationMinutes, dto.IsWithinBillingGrace, dto.BaseAmount,
            dto.Multiplier, dto.OverstayMinutes, dto.OverstayAmount, dto.FinalAmount,
            (dto.Lines ?? []).Select(l => new FeeLineDto(l.Description, l.Minutes, l.Amount)).ToList());
    }

    private const string SettlementsPath = "internal/v1/gate-settlements";

    public async Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, SettlementsPath,
            Serialize(request), cancellationToken, "ghi thanh toán tại cổng");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ConflictException("Lượt gửi này đã ghi một khoản thu khác số tiền.", "GATE_PAYMENT_EXISTS");
        EnsureSuccessAsync(response, "ghi thanh toán tại cổng");

        var dto = await response.Content.ReadFromJsonAsync<PaymentResponse>(Json, cancellationToken)
            ?? throw new DependencyUnavailableException("PaymentService trả dữ liệu rỗng khi ghi thanh toán tại cổng.");
        return new GateSettlementResult(dto.Code, dto.Status, dto.Amount);
    }

    public async Task<GateSettlementResult?> FindAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        const string action = "kiểm tra khoản thu của lượt gửi";
        using var response = await SendAsync(HttpMethod.Get, $"{SettlementsPath}/{Uri.EscapeDataString(sessionCode)}", null,
            cancellationToken, action);
        EnsureSuccessAsync(response, action);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        var dto = await response.Content.ReadFromJsonAsync<PaymentResponse>(Json, cancellationToken)
            ?? throw new DependencyUnavailableException($"PaymentService trả dữ liệu rỗng khi {action}.");
        return new GateSettlementResult(dto.Code, dto.Status, dto.Amount);
    }

    public async Task<decimal> CashTotalAsync(int shiftId, CancellationToken ct)
    {
        const string action = "lấy tổng tiền mặt của ca";
        using var response = await SendAsync(HttpMethod.Get, $"{SettlementsPath}?shiftId={shiftId}", null, ct, action);
        EnsureSuccessAsync(response, action);
        var dto = await response.Content.ReadFromJsonAsync<ShiftCashResponse>(Json, ct)
            ?? throw new DependencyUnavailableException($"PaymentService trả dữ liệu rỗng khi {action}.");
        return dto.CashTotal;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content,
        CancellationToken ct, string action)
    {
        using var requestMessage = new HttpRequestMessage(method, url) { Content = content };
        try
        {
            return await http.SendAsync(requestMessage, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "PaymentService không phản hồi khi {Action}", action);
            throw new DependencyUnavailableException("PaymentService chưa sẵn sàng, thử lại sau ít phút.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("PaymentService phản hồi quá chậm (timeout), thử lại sau ít phút.");
        }
    }

    private void EnsureSuccessAsync(HttpResponseMessage response, string action)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("PaymentService trả {Status} khi {Action}", (int)response.StatusCode, action);
            throw new DependencyUnavailableException($"PaymentService chưa sẵn sàng khi {action} (HTTP {(int)response.StatusCode}).");
        }
    }

    private sealed record SessionFeeResponse(int ParkingLotId, int RateCardId, string VehicleType, int DurationMinutes,
        bool IsWithinBillingGrace, decimal BaseAmount, decimal Multiplier, int OverstayMinutes, decimal OverstayAmount,
        decimal FinalAmount, IReadOnlyList<LineResponse>? Lines);
    private sealed record LineResponse(string Description, int Minutes, decimal Amount);
    private sealed record PaymentResponse(string Code, string Status, decimal Amount);
    private sealed record ShiftCashResponse(decimal CashTotal);
}
