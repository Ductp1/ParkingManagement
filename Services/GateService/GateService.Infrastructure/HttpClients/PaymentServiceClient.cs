using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GateService.Application.Features.GateConsole;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.HttpClients;

/// <summary>Client gọi PaymentService (TV4): chốt phí lượt gửi (UC-22) + ghi thanh toán tiền mặt tại cổng.</summary>
public sealed class PaymentServiceClient(HttpClient http, ILogger<PaymentServiceClient> logger)
    : IExitFeeCalculator, IGateSettlementRecorder
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

    public async Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/payments/gate-settlement",
            Serialize(request), cancellationToken, "ghi thanh toán tại cổng");
        EnsureSuccessAsync(response, "ghi thanh toán tại cổng");

        var dto = await response.Content.ReadFromJsonAsync<PaymentResponse>(Json, cancellationToken)
            ?? throw new DependencyUnavailableException("PaymentService trả dữ liệu rỗng khi ghi thanh toán tại cổng.");
        return new GateSettlementResult(dto.Code, dto.Status);
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
    private sealed record PaymentResponse(string Code, string Status);
}
